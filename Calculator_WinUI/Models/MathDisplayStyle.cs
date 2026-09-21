namespace Calculator_WinUI.Models
{
    // every tunable of the formula display in one place
    //
    // the two display lines differ in base size and in nothing else, so both read out of here; their
    // colors are ThemeResources on the panels, and every number below reaches the screen through
    // ToLayoutStyle, which is where a knob has to arrive to do anything at all
    //
    // the scales below always size a whole structured token, never a single slot of one: a slot scaled on
    // its own keeps the offsets of the size around it and drifts away from the bar or the base it belongs
    // to
    public class MathDisplayStyle
    {
        // === display knobs ===

        public double FontSizePx { get; set; } = 36; // base size of the formula, every scale below is relative to it
        public double MinFitScale { get; set; } = 0.45; // how far a formula too tall for the box may be shrunk before it is clipped after all

        // size of a whole structured token, in em of the text around it; 1.0 keeps what KaTeX picks itself
        public double FractionScale { get; set; } = 1.0;
        public double PowerScale { get; set; } = 1.0;
        public double RootScale { get; set; } = 1.0;
        public double LogarithmScale { get; set; } = 1.0;
        public double FunctionScale { get; set; } = 1.0;

        public double FractionBarThickness { get; set; } = 0.04; // em, the KaTeX default

        // the one lever on how big the content of a fraction is, and it is a step rather than a factor
        //
        // KaTeX shrinks a numerator and a denominator itself, to script size, and writes the clearances
        // around the bar to match; false keeps that, true asks for a display style fraction instead,
        // where both halves stay at full text size and the bar takes the wide display clearances, which
        // is a far taller fraction
        public bool UseDisplayFractions { get; set; } = false;
        public bool UseSansSerif { get; set; } = true; // upright sans digits, the way a Casio display draws them

        // operators are sized and spaced on their own, because KaTeX draws one at full size with a fixed
        // space on either side and that reads far heavier than a pocket calculator does
        //
        // the gap and the raise are em of the operator, so they follow OperatorScale rather than the
        // text around them
        //
        // the raise exists because KaTeX centres + and - on the math axis, and the height of that axis
        // scales with the font size; shrinking an operator therefore also drops it, and this puts it
        // back up where it reads level with the digits
        // the weight carries the sign at a smaller size; KaTeX ships a bold cut of both faces in use,
        // so 600 and 700 pick a real one rather than letting the browser smear the regular
        public double OperatorScale { get; set; } = 0.6;
        public double OperatorGap { get; set; } = 0.1;
        public double OperatorRaise { get; set; } = 0.20; // em, higher lifts the operator further
        public int OperatorWeight { get; set; } = 600; // 400 normal, 600 semibold, 700 bold

        // the input caret, painted by css rather than by KaTeX so it shrinks together with a slot
        //
        // the bar stands on the baseline of whatever slot it is in and reaches the top of the digits
        // beside it; every value is em, so the whole caret scales with the slot and nothing else
        public double CursorWidth { get; set; } = 0.07; // em, drawn centred on the gap it marks
        public double CursorHeight { get; set; } = 0.71; // em, roughly the cap height of a digit
        public double CursorShift { get; set; } = -0.04; // em above the baseline, negative drops it below
        public double CursorCornerRadius { get; set; } = 0.03; // em, half the width rounds the ends off



        // === presets ===

        // the two display lines differ in base size and in nothing else any more; their colors are
        // ThemeResources on the panels themselves, which is what lets them follow a theme change without
        // anything here hearing about it
        public static MathDisplayStyle ForInputLine()
        {
            return new MathDisplayStyle { FontSizePx = 36 };
        }

        public static MathDisplayStyle ForHistoryLine()
        {
            return new MathDisplayStyle { FontSizePx = 18 };
        }


        // === output ===

        // the numbers the native renderer lays out with
        //
        // only the knobs that existed while the display was KaTeX travel here; everything MathLayoutStyle
        // adds on top of them, the script ratios, the math axis, the radical and the delimiters, has no
        // counterpart on this side and keeps its own default
        public Layout.MathLayoutStyle ToLayoutStyle()
        {
            return new Layout.MathLayoutStyle
            {
                FontSizePx = FontSizePx,
                MinFitScale = MinFitScale,
                FractionScale = FractionScale,
                PowerScale = PowerScale,
                RootScale = RootScale,
                LogarithmScale = LogarithmScale,
                FunctionScale = FunctionScale,
                FractionBarThickness = FractionBarThickness,
                UseDisplayFractions = UseDisplayFractions,
                OperatorScale = OperatorScale,
                OperatorGap = OperatorGap,
                OperatorRaise = OperatorRaise,
                OperatorWeight = OperatorWeight,
                CursorWidth = CursorWidth,
                CursorHeight = CursorHeight,
                CursorShift = CursorShift,
                CursorCornerRadius = CursorCornerRadius
            };
        }

    }
}
