using System.Collections.Generic;

namespace FluentMath.Models.Layout
{
    // what one run of text occupies, split at the baseline, so any boxes line up in a row
    public readonly struct TextMetrics
    {
        public double Width { get; }
        public double Ascent { get; }  // above the baseline
        public double Descent { get; } // below it

        public TextMetrics(double width, double ascent, double descent)
        {
            Width = width;
            Ascent = ascent;
            Descent = descent;
        }
    }


    // where the caret stands: the token list and the index in it
    // (matched by identity, which tells two empty slots apart)
    public readonly struct CaretTarget
    {
        public IReadOnlyList<MathToken> Tokens { get; }
        public int Index { get; }

        public CaretTarget(IReadOnlyList<MathToken> tokens, int index)
        {
            Tokens = tokens;
            Index = index;
        }
    }


    // where the caret ended up, once the layout knows; not a box, it hangs off the box it stands at
    // (the box says where it stands sideways, the line how high, since an operator rides above its baseline)
    public readonly struct CaretPlacement
    {
        public MathBox Box { get; }      // what it hangs off
        public double Offset { get; }    // from that boxes left edge, in its own coordinates
        public MathBox Line { get; }     // the row or slot it stands in, which owns the baseline
        public double FontSize { get; }  // the size it is drawn at

        public CaretPlacement(MathBox box, double offset, MathBox line, double fontSize)
        {
            Box = box;
            Offset = offset;
            Line = line;
            FontSize = fontSize;
        }
    }


    // measures a run of text; an interface, so a test can hand in numbers of its own
    // (the font family is carried by the implementation)
    public interface ITextMeasurer
    {
        TextMetrics Measure(string text, double fontSizePx);
    }
}
