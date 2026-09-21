using System;
using System.Collections.Generic;

namespace Calculator_WinUI.Models.Layout
{
    // turns a point in the display back into a place in the token tree
    //
    // it walks the boxes rather than the tokens, because only the boxes know where anything ended up, and
    // it answers with the address MathInputManager.SetCursorPosition already parses; that is the whole
    // reason the layout carries addresses at all
    //
    // the answer is the nearest cursor position rather than the one under the finger: there is no formula
    // in which a click has no sensible answer, and landing one gap off is better than doing nothing
    public static class MathHitTest
    {
        // one place the caret could go, and the band of the row it belongs to
        private readonly struct Stop
        {
            public string Address { get; }
            public double X { get; }
            public double Top { get; }
            public double Bottom { get; }

            public Stop(string address, double x, double top, double bottom)
            {
                Address = address;
                X = x;
                Top = top;
                Bottom = bottom;
            }
        }

        // how much heavier a vertical miss counts than a horizontal one
        //
        // without it a click low under a fraction would answer with a position in the numerator whenever
        // that happened to be nearer sideways, which reads as the caret jumping to the wrong line
        private const double VerticalWeight = 3;

        public static string NearestAddress(MathBox root, double x, double y)
        {
            List<Stop> stops = new List<Stop>();
            Collect(root, stops);

            string best = null;
            double bestScore = double.MaxValue;

            foreach (Stop stop in stops)
            {
                if (stop.Address == null) continue;

                double dx = Math.Abs(stop.X - x);
                double dy = y < stop.Top ? stop.Top - y : (y > stop.Bottom ? y - stop.Bottom : 0);
                double score = dx + dy * VerticalWeight;

                if (score >= bestScore) continue;

                bestScore = score;
                best = stop.Address;
            }

            return best;
        }

        private static void Collect(MathBox box, List<Stop> stops)
        {
            switch (box)
            {
                case RowBox row:
                    foreach (MathBox child in row.Children)
                    {
                        CollectRun(child, row, stops);
                        Collect(child, stops);
                    }

                    // the position after the last token, which no child stands in front of
                    stops.Add(new Stop(row.EndAddress, row.X + row.Width, row.Top, row.Bottom));
                    break;

                case FractionBox fraction:
                    Collect(fraction.Numerator, stops);
                    Collect(fraction.Denominator, stops);
                    break;

                case RootBox root:
                    if (root.Index != null) Collect(root.Index, stops);
                    Collect(root.Radicand, stops);
                    break;

                case PlaceholderBox placeholder:
                    stops.Add(new Stop(placeholder.CursorAddress, placeholder.X, placeholder.Top, placeholder.Bottom));
                    break;
            }
        }

        // a run covers several tokens and therefore several positions; the gaps between its digits are
        // spread evenly across its width, which is close enough for a finger and avoids measuring every
        // prefix of every number on every click
        private static void CollectRun(MathBox child, RowBox row, List<Stop> stops)
        {
            if (child is TextRunBox run && run.TokenAddresses != null && run.TokenAddresses.Count > 1)
            {
                double step = run.Width / run.TokenAddresses.Count;
                for (int offset = 0; offset < run.TokenAddresses.Count; offset++)
                {
                    stops.Add(new Stop(run.TokenAddresses[offset], run.X + step * offset, row.Top, row.Bottom));
                }

                return;
            }

            stops.Add(new Stop(child.CursorAddress, child.X, row.Top, row.Bottom));
        }
    }
}
