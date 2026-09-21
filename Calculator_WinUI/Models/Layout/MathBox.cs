using System;
using System.Collections.Generic;

namespace Calculator_WinUI.Models.Layout
{
    // the layout result for one piece of a formula
    //
    // a box reports its width and its two reaches around the baseline rather than one height, which is
    // what lets a row place a fraction, a root and a digit beside each other and still know where their
    // common baseline runs
    public abstract class MathBox
    {
        // === measured ===

        public double Width { get; protected set; }
        public double Ascent { get; protected set; }
        public double Descent { get; protected set; }

        public double Height => Ascent + Descent;

        // how far this box rides above the baseline of the row it sits in; an operator uses it to sit on
        // the math axis instead of on the baseline
        public double Raise { get; internal set; }

        // space the row leaves in front of and behind this box; a box that wants air on both sides has to
        // ask for both, since the box after it may not exist
        public double LeadingGap { get; internal set; }
        public double TrailingGap { get; internal set; }


        // === placed ===

        // filled in by Place, in the coordinate space of the whole formula rather than of the parent, so a
        // hit test does not have to walk back up the tree to add offsets

        public double X { get; private set; }
        public double Baseline { get; private set; }

        public double Top => Baseline - Ascent;
        public double Bottom => Baseline + Descent;

        // the baseline handed in is the one of the surrounding row; a box that rides above it takes its
        // own raise off here, so everything downstream can read Baseline and ignore Raise
        public virtual void Place(double x, double baseline)
        {
            X = x;
            Baseline = baseline - Raise;
        }
    }


    // a run of glyphs measured and drawn as one piece
    //
    // digits arrive as one token each, so a multi digit number is several tokens and a single run;
    // measuring and drawing per character would accumulate the rounding of every advance width and read as
    // uneven spacing, so the run is the unit and the tokens it covers are kept for the caret and the hit
    // test that come later
    public sealed class TextRunBox : MathBox
    {
        public string Text { get; }
        public double FontSize { get; }
        public IReadOnlyList<MathToken> Tokens { get; }

        public TextRunBox(string text, double fontSize, TextMetrics metrics, IReadOnlyList<MathToken> tokens)
        {
            Text = text;
            FontSize = fontSize;
            Tokens = tokens;

            Width = metrics.Width;
            Ascent = metrics.Ascent;
            Descent = metrics.Descent;
        }
    }


    // a left to right sequence sharing one baseline
    public sealed class RowBox : MathBox
    {
        public IReadOnlyList<MathBox> Children { get; }

        public RowBox(IReadOnlyList<MathBox> children)
        {
            Children = children;

            foreach (MathBox child in children)
            {
                Width += child.LeadingGap + child.Width + child.TrailingGap;

                // a raised child reaches that much higher and hangs that much less low
                Ascent = Math.Max(Ascent, child.Ascent + child.Raise);
                Descent = Math.Max(Descent, child.Descent - child.Raise);
            }
        }

        // an empty row still has to stand as tall as the text that would fill it, or the display collapses
        // to nothing and a caret has nowhere to be
        public static RowBox Empty(TextMetrics strut)
        {
            RowBox row = new RowBox(new List<MathBox>());
            row.Ascent = strut.Ascent;
            row.Descent = strut.Descent;
            return row;
        }

        public override void Place(double x, double baseline)
        {
            base.Place(x, baseline);

            double pen = X;
            foreach (MathBox child in Children)
            {
                pen += child.LeadingGap;
                child.Place(pen, Baseline);
                pen += child.Width + child.TrailingGap;
            }
        }
    }
}
