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
        // the math axis, an exponent to sit above it, and a logarithm base takes a negative one to drop
        // below it
        public double Raise { get; internal set; }

        // space the row leaves in front of and behind this box; a box that wants air on both sides has to
        // ask for both, since the box after it may not exist
        public double LeadingGap { get; internal set; }
        public double TrailingGap { get; internal set; }

        // the cursor position immediately in front of this box, in the form MathInputManager parses, so a
        // click can be turned back into a place in the tree; null on a box that is not a token, such as
        // the caret itself or a drawn delimiter
        public string CursorAddress { get; internal set; }


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

        // one cursor address per token the run covers, so a click between two digits of the same number
        // lands between them rather than at one end of the whole run
        public IReadOnlyList<string> TokenAddresses { get; internal set; }

        // how far into the run each of those tokens begins, measured rather than spread evenly over the
        // width: these are the very numbers the caret is placed at, so a click answers with the position
        // the caret would be drawn in instead of with one a few pixels off
        //
        // written in the same step as the addresses; neither is ever set without the other
        public IReadOnlyList<double> TokenOffsets { get; internal set; }

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
    //
    // a power, a logarithm, a function and a scientific token are all rows rather than boxes of their own:
    // an exponent is a child with a positive raise and a logarithm base one with a negative raise, which
    // is the whole of what those constructions need
    public sealed class RowBox : MathBox
    {
        public IReadOnlyList<MathBox> Children { get; }

        // the position after the last token, which no child stands in front of
        public string EndAddress { get; internal set; }

        // the size a caret standing in this row is drawn at
        //
        // a row is the thing that owns a baseline, so it is also the thing that knows how tall the bar
        // in it should be; only a row the cursor can stand in carries it, which is the same set that
        // carries an EndAddress
        public double FontSize { get; internal set; }

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


    // a numerator over a denominator with a bar between them
    //
    // the bar rests on the math axis rather than on the baseline, which is what keeps a fraction level
    // with the operators beside it however tall its two halves turn out
    public sealed class FractionBox : MathBox
    {
        public MathBox Numerator { get; }
        public MathBox Denominator { get; }

        public double BarThickness { get; }
        public double BarAxis { get; }  // em above the baseline, to the centre of the bar

        private readonly double _numeratorGap;
        private readonly double _denominatorGap;

        public FractionBox(MathBox numerator, MathBox denominator, double barThickness, double barAxis,
            double numeratorGap, double denominatorGap, double sidePadding)
        {
            Numerator = numerator;
            Denominator = denominator;
            BarThickness = barThickness;
            BarAxis = barAxis;

            _numeratorGap = numeratorGap;
            _denominatorGap = denominatorGap;

            double half = barThickness / 2;

            Width = Math.Max(numerator.Width, denominator.Width) + sidePadding * 2;
            Ascent = barAxis + half + numeratorGap + numerator.Height;
            Descent = denominatorGap + denominator.Height - (barAxis - half);
        }

        // where the bar is drawn, once the box has been placed
        public double BarTop => Baseline - BarAxis - BarThickness / 2;

        public override void Place(double x, double baseline)
        {
            base.Place(x, baseline);

            double half = BarThickness / 2;
            double centre = X + Width / 2;

            // the numerator hangs above the bar by its own descent plus the gap, the denominator below it
            // by its own ascent plus the gap
            Numerator.Place(centre - Numerator.Width / 2,
                Baseline - BarAxis - half - _numeratorGap - Numerator.Descent);

            Denominator.Place(centre - Denominator.Width / 2,
                Baseline - BarAxis + half + _denominatorGap + Denominator.Ascent);
        }
    }


    // a radical sign with an optional index, drawn rather than set from a glyph so it scales to whatever
    // it encloses
    public sealed class RootBox : MathBox
    {
        public MathBox Index { get; }  // null when the root carries none, which is the usual case
        public MathBox Radicand { get; }

        public double HookWidth { get; }
        public double RuleThickness { get; }   // the bar over the radicand
        public double HookThickness { get; }   // the sign in front of it

        // how tall the sign itself is, which is less than Ascent whenever an index reaches higher
        public double SignAscent { get; }

        public double IndexWidth => Index?.Width ?? 0;

        private readonly double _indexRaise;
        private readonly double _leadingPad;

        public RootBox(MathBox index, MathBox radicand, double hookWidth, double ruleThickness,
            double hookThickness, double verticalGap, double indexRaise, double leadingPad,
            double trailingPad)
        {
            Index = index;
            Radicand = radicand;
            HookWidth = hookWidth;
            RuleThickness = ruleThickness;
            HookThickness = hookThickness;

            // the bar is what stands on top, so it is the bar that sets how high the sign reaches
            SignAscent = radicand.Ascent + verticalGap + ruleThickness;

            // the bar runs the full width of the box, so it covers both pads: the one that keeps the
            // radicand off the sign and the one that carries the bar past its last glyph
            _leadingPad = leadingPad;
            Width = IndexWidth + hookWidth + leadingPad + radicand.Width + trailingPad;
            Descent = radicand.Descent;

            // the index may stand higher than the sign it sits on, and then it is what sets the height
            double indexTop = index == null ? 0 : SignAscent * indexRaise + index.Height;
            Ascent = Math.Max(SignAscent, indexTop);

            _indexRaise = indexRaise;
        }

        // the bar over the radicand, once the box has been placed
        public double RuleTop => Baseline - SignAscent;

        public override void Place(double x, double baseline)
        {
            base.Place(x, baseline);

            Index?.Place(X, Baseline - SignAscent * _indexRaise - Index.Descent);
            Radicand.Place(X + IndexWidth + HookWidth + _leadingPad, Baseline);
        }
    }


    // a bracket or a bar that takes its height from what it stands beside
    public enum DelimiterKind
    {
        ParenthesisOpen,
        ParenthesisClose,
        Bar
    }

    public sealed class DelimiterBox : MathBox
    {
        public DelimiterKind Kind { get; }
        public double Thickness { get; }

        public DelimiterBox(DelimiterKind kind, double width, double ascent, double descent, double thickness)
        {
            Kind = kind;
            Thickness = thickness;
            Width = width;
            Ascent = ascent;
            Descent = descent;
        }

        // a typed bracket is built before the row around it is, since only the row knows what stands
        // between it and its partner; the reach is filled in once that is known
        //
        // a delimiter belonging to a function needs none of this, because there the content is built
        // first and handed over whole
        internal void Stretch(double ascent, double descent)
        {
            Ascent = ascent;
            Descent = descent;
        }
    }


    // what an empty slot draws, so it can be seen and a caret can stand in it
    public sealed class PlaceholderBox : MathBox
    {
        public double Side { get; }
        public double Thickness { get; }

        // a slot is a line of its own with one position in it, so it carries the caret size the same
        // way a row does
        public double FontSize { get; internal set; }

        // the two reaches are those of the text that would fill the slot, not those of the square
        //
        // an empty slot has to occupy exactly what a filled one does, or a fraction is half height until
        // the first digit arrives and jumps the moment it does
        public PlaceholderBox(double side, double thickness, TextMetrics strut)
        {
            Side = side;
            Thickness = thickness;

            Width = side;
            Ascent = strut.Ascent;
            Descent = strut.Descent;
        }
    }
}
