using System;
using System.Collections.Generic;

namespace FluentMath.Models.Layout
{
    // turns a point in the display back into a place in the token tree, as the address
    // MathInputManager.SetCursorPosition parses
    // (the line first, then the position in it; scored together, a click on the base of a power could
    // land in front of the whole power)
    public static class MathHitTest
    {
        // one token list as it was laid out: the box that holds it, how many lists it sits inside, and
        // every place the caret could stand in it
        private sealed class Line
        {
            public MathBox Box { get; }
            public int Depth { get; }
            public List<Stop> Stops { get; } = new List<Stop>();

            public Line(MathBox box, int depth)
            {
                Box = box;
                Depth = depth;
            }
        }

        // one place the caret could go, at the gap it stands in
        private readonly struct Stop
        {
            public string Address { get; }
            public double X { get; }

            public Stop(string address, double x)
            {
                Address = address;
                X = x;
            }
        }

        // two lines that end within this much of each other end in the same place
        // (a floating point epsilon; the edges are sums of the same lengths in a different order)
        private const double SamePlace = 1e-9;

        public static string NearestAddress(MathBox root, double x, double y)
        {
            Line line = Target(root, ref x, ref y);

            return line == null ? null : NearestStop(line, x)?.Address;
        }

        // the same walk, answering with where the caret would be drawn, as the CaretPlacement of the real one
        public static CaretPlacement? NearestCaret(MathBox root, double x, double y)
        {
            Line line = Target(root, ref x, ref y);
            if (line == null) return null;

            if (NearestStop(line, x) is not Stop stop) return null;

            double fontSize = line.Box switch
            {
                RowBox row => row.FontSize,
                PlaceholderBox slot => slot.FontSize,
                _ => 0
            };

            return new CaretPlacement(line.Box, stop.X - line.Box.X, line.Box, fontSize);
        }

        // the line a point was aimed at, with the point clamped into the formula on the way
        private static Line Target(MathBox root, ref double x, ref double y)
        {
            if (root == null) return null;

            // a point outside the formula aims at the nearest edge
            x = Math.Clamp(x, root.X, root.X + root.Width);
            y = Math.Clamp(y, root.Top, root.Bottom);

            List<Line> lines = new List<Line>();
            Collect(root, 0, lines);

            // clamped into the root, so only the lines the point fell in stay in the running
            double nearest = double.MaxValue;
            foreach (Line line in lines)
            {
                nearest = Math.Min(nearest, Misses(line.Box, x, y));
            }

            // the innermost of them, a numerator over the row of its fraction
            // (on an edge two slots share, a base and its exponent, the nearer baseline wins)
            Line target = null;
            double targetGap = 0;

            foreach (Line line in lines)
            {
                if (Misses(line.Box, x, y) > nearest + SamePlace) continue;

                double gap = Math.Abs(line.Box.Baseline - y);
                if (target != null && line.Depth < target.Depth) continue;
                if (target != null && line.Depth == target.Depth && gap >= targetGap) continue;

                target = line;
                targetGap = gap;
            }

            return target;
        }

        private static void Collect(MathBox box, int depth, List<Line> lines)
        {
            Line line = LineOf(box, depth);
            if (line != null)
            {
                lines.Add(line);
                depth++; // whatever is nested in this list sits one line deeper
            }

            switch (box)
            {
                case RowBox row:
                    foreach (MathBox child in row.Children) Collect(child, depth, lines);
                    break;

                case FractionBox fraction:
                    Collect(fraction.Numerator, depth, lines);
                    Collect(fraction.Denominator, depth, lines);
                    break;

                case RootBox root:
                    if (root.Index != null) Collect(root.Index, depth, lines);
                    Collect(root.Radicand, depth, lines);
                    break;

                case StackBox stack:
                    foreach (MathBox child in stack.Children) Collect(child, depth, lines);
                    break;
            }
        }

        // a row with an end address is a token list the cursor can stand in; one without is a construction
        // such as a power, with no position of its own
        private static Line LineOf(MathBox box, int depth)
        {
            if (box is PlaceholderBox placeholder)
            {
                Line slot = new Line(placeholder, depth);
                slot.Stops.Add(new Stop(placeholder.CursorAddress, placeholder.X));

                return slot;
            }

            if (box is not RowBox row || row.EndAddress == null) return null;

            Line line = new Line(row, depth);
            foreach (MathBox child in row.Children)
            {
                // a run covers several positions, each at its measured offset
                if (child is TextRunBox run && run.TokenAddresses != null && run.TokenOffsets != null)
                {
                    for (int offset = 0; offset < run.TokenAddresses.Count; offset++)
                    {
                        line.Stops.Add(new Stop(run.TokenAddresses[offset], run.X + run.TokenOffsets[offset]));
                    }

                    continue;
                }

                line.Stops.Add(new Stop(child.CursorAddress, child.X));
            }

            // the position after the last token, which no child stands in front of
            line.Stops.Add(new Stop(row.EndAddress, row.X + row.Width));

            return line;
        }

        // how far the point lies outside this box, zero when it fell inside it
        private static double Misses(MathBox box, double x, double y)
        {
            double dx = x < box.X ? box.X - x : Math.Max(0, x - box.X - box.Width);
            double dy = y < box.Top ? box.Top - y : Math.Max(0, y - box.Bottom);

            return dx + dy;
        }

        private static Stop? NearestStop(Line line, double x)
        {
            Stop? best = null;
            double bestDistance = double.MaxValue;

            foreach (Stop stop in line.Stops)
            {
                if (stop.Address == null) continue;

                double distance = Math.Abs(stop.X - x);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = stop;
            }

            return best;
        }
    }
}
