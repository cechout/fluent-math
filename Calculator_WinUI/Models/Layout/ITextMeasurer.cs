namespace Calculator_WinUI.Models.Layout
{
    // what one run of text occupies, split at the baseline rather than given as a single height
    //
    // that split is the whole reason a fraction, a root and a plain digit can sit in one row without a
    // special case for every pairing: a row takes the largest reach in each direction and knows where the
    // common baseline runs
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


    // the one thing the layout cannot work out by itself, and the reason it is an interface at all:
    // measuring real glyphs needs a text stack, while a test needs numbers it chose itself
    //
    // the font family is not a parameter because a whole formula is set in one family; an implementation
    // carries it
    public interface ITextMeasurer
    {
        TextMetrics Measure(string text, double fontSizePx);
    }
}
