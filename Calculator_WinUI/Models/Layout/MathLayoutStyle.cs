namespace Calculator_WinUI.Models.Layout
{
    // the numbers the layout needs, kept apart from MathDisplayStyle because everything under Layout has
    // to stay free of WinUI: MathDisplayStyle resolves the accent through UISettings and branches on
    // ElementTheme, neither of which exists on the plain net8.0 runner the tests use
    //
    // MathDisplayStyle keeps the colors and the display knobs that only matter once something is drawn,
    // and hands one of these down
    //
    // every length here is em of the size the piece it belongs to is set at, never a pixel, so the whole
    // formula scales from FontSizePx alone
    public class MathLayoutStyle
    {
        // === text ===

        public double FontSizePx { get; set; } = 36; // base size, every scale below is relative to it

        // how far a formula taller than its box may be shrunk before it is clipped after all
        //
        // it is load bearing rather than a nicety: a stacked fraction at the default size needs roughly
        // twice the height the input line has
        public double MinFitScale { get; set; } = 0.45;


        // === script levels ===

        // a numerator, a denominator and an exponent are set at script size, and one step further in at
        // scriptscript; nothing goes smaller than that
        //
        // both are fractions of the base size rather than of the level above, which is why a third level
        // stays where the second one is instead of shrinking away to nothing
        public double ScriptScale { get; set; } = 0.7;
        public double ScriptScriptScale { get; set; } = 0.5;


        // === structures ===

        // the size of a whole structured token relative to the text around it; 1.0 leaves it alone
        public double FractionScale { get; set; } = 1.0;
        public double PowerScale { get; set; } = 1.0;
        public double RootScale { get; set; } = 1.0;
        public double LogarithmScale { get; set; } = 1.0;
        public double FunctionScale { get; set; } = 1.0;


        // === operators ===

        // operators are sized and spaced on their own, because a full size sign with the usual space on
        // either side reads far heavier than a pocket calculator does
        //
        // the gap and the raise are em of the operator, so they follow OperatorScale rather than the text
        // around them
        //
        // the raise exists because a plus and a minus are centred on the math axis, and the height of that
        // axis scales with the font size; shrinking an operator therefore also drops it, and this puts it
        // back up where it reads level with the digits
        public double OperatorScale { get; set; } = 0.6;
        public double OperatorGap { get; set; } = 0.1;
        public double OperatorRaise { get; set; } = 0.20;
        public int OperatorWeight { get; set; } = 600; // 400 normal, 600 semibold, 700 bold


        // === fractions ===

        // the math axis is the line a fraction bar rests on, em above the baseline
        public double MathAxisHeight { get; set; } = 0.25;

        public double FractionBarThickness { get; set; } = 0.04;
        public double FractionNumeratorGap { get; set; } = 0.12;   // between the bar and the numerator
        public double FractionDenominatorGap { get; set; } = 0.12; // between the bar and the denominator
        public double FractionSidePadding { get; set; } = 0.06;    // how far the bar reaches past its content

        // both halves stay at full size instead of dropping a level, which is a far taller fraction
        public bool UseDisplayFractions { get; set; } = false;


        // === scripts ===

        // of the ascent of whatever carries the exponent, so a tall base lifts it further
        public double SuperscriptShift { get; set; } = 0.55;

        // em below the baseline, for the base of a logarithm
        public double SubscriptShift { get; set; } = 0.2;


        // === roots ===

        public double RadicalHookWidth { get; set; } = 0.55;     // em, the part in front of the radicand
        public double RadicalRuleThickness { get; set; } = 0.05; // em, the bar over the radicand
        public double RadicalVerticalGap { get; set; } = 0.04;    // em between that bar and the radicand

        // where the index sits, as a fraction of the height of the sign it stands on
        public double RadicalIndexRaise { get; set; } = 0.6;


        // === delimiters ===

        // a delimiter takes its height from what it encloses, so these are only its width and the air it
        // keeps around the content
        public double DelimiterWidth { get; set; } = 0.3;
        public double DelimiterPadding { get; set; } = 0.05;
        public double DelimiterThickness { get; set; } = 0.06; // em of the stroke it is drawn with


        // === the caret ===

        // it stands on the baseline of whatever slot it is in and reaches the top of the digits beside
        // it; every value is em, so the caret scales with the slot and with nothing else
        public double CursorWidth { get; set; } = 0.07;
        public double CursorHeight { get; set; } = 0.71;
        public double CursorShift { get; set; } = -0.04; // em above the baseline, negative drops it below
        public double CursorCornerRadius { get; set; } = 0.03;


        // === empty slots ===

        // an empty slot draws a box rather than collapsing, so it can be seen and a caret has somewhere to
        // stand; a slot with no extent at all would be unreachable
        public double PlaceholderSize { get; set; } = 0.5;       // em, the side of the square
        public double PlaceholderThickness { get; set; } = 0.04; // em of its outline
    }
}
