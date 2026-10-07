namespace FluentMath.Models.Layout
{
    // the formula layout knobs:
    // every number that shapes the formula display; lengths in em, 1.0 being the font size of the part
    // they belong to, so the formula scales with FontSizePx
    // (no WinUI types, the tests build this on plain net10.0; the colors stay in the markup)
    public class MathLayoutStyle
    {
        // === the two display lines ===

        // in px; the defaults (each calculator page sets its own pair)
        public const double InputLineFontSize = 30;
        public const double HistoryLineFontSize = 16;

        public static MathLayoutStyle ForInputLine()
        {
            return new MathLayoutStyle { FontSizePx = InputLineFontSize };
        }

        public static MathLayoutStyle ForHistoryLine()
        {
            return new MathLayoutStyle { FontSizePx = HistoryLineFontSize };
        }


        // === text ===

        public double FontSizePx { get; set; } = InputLineFontSize; // everything below scales with this

        // true keeps both halves of a fraction at full size, not script size; (the evaluator reads it too)
        public bool UseDisplayFractions { get; set; } = true;

        // the smallest a formula too tall for its line is shrunk to, 0.45 being 45 percent; below that it is cut off
        public double MinFitScale { get; set; } = 0.45;


        // === numbers ===

        // the mark between the whole part and the decimals; (the tokens always hold a dot)
        public string DecimalMark { get; set; } = ".";

        // a semicolon between two arguments with a decimal comma, as on a Casio
        public string ListSeparator => DecimalMark == "," ? ";" : ",";

        // a gap every three digits of the whole part, 1 234 567; (space, not a character, for the caret)
        public bool GroupDigits { get; set; } = false;
        public double DigitGroupGap { get; set; } = 0.2; // width of that gap


        // === script sizes ===

        // exponents, log bases, the raised −1 and the bounds of Σ, Π and ∫: one level in at ScriptScale, two
        // or more at ScriptScriptScale, both against the full text size
        // (fractions join them only while UseDisplayFractions is off)
        public double ScriptScale { get; set; } = 0.6;       // an exponent, 0.5 being half the text
        public double ScriptScriptScale { get; set; } = 0.5; // an exponent inside an exponent, and the index of a root


        // === structures ===

        // the size of a whole structure compared to the text around it; 1.0 keeps it the same
        public double FractionScale { get; set; } = 1.0;
        public double PowerScale { get; set; } = 1.0;
        public double RootScale { get; set; } = 1.0;
        public double LogarithmScale { get; set; } = 1.0;
        public double FunctionScale { get; set; } = 1.0;

        public double ArgumentSeparatorGap { get; set; } = 0.15; // space after the comma between two arguments, as in GCD(12, 18)


        // === operators ===

        // plus, minus, times and divided by, a little smaller than full size signs, like a pocket calculator
        // (OperatorGap and OperatorRaise are in em of the sign, so they follow OperatorScale)
        public double OperatorScale { get; set; } = 0.9; // size of the sign compared to the text
        public double OperatorGap { get; set; } = 0.10;  // space on each side of the sign

        // moves the sign up, a negative value moves it down
        public double OperatorRaise { get; set; } = 0.0;
        public int OperatorWeight { get; set; } = 500; // 400 normal, 600 semibold, 700 bold

        // how much the space around a smaller sign shrinks with it: 1 just as much as the sign, 0 not at all
        public double OperatorGapScaling { get; set; } = 0.0;


        // === fractions ===

        // the height of the fraction bars and the middle of plus and minus, MathAxisHeight plus
        // MathAxisRaise above the baseline
        public double MathAxisHeight { get; set; } = 0.25;

        // moves the fraction bars and the operators up together, a negative value moves both down
        public double MathAxisRaise { get; set; } = 0.1;

        public double FractionBarThickness { get; set; } = 0.04;
        public double FractionNumeratorGap { get; set; } = 0.04;   // space between the bar and the numerator above it
        public double FractionDenominatorGap { get; set; } = 0.01; // space between the bar and the denominator below it
        public double FractionSidePadding { get; set; } = 0.05;    // how far the bar reaches past the numerator and the denominator on each side
        public double MixedFractionGap { get; set; } = 0.1;        // space between the whole part of a mixed number and its fraction


        // === Σ and Π ===

        // a big Σ or Π with a bound above and below it (x= in front of the lower) and the body in brackets
        // (the bounds measured baseline to baseline; a taller bound moves further out by its extra height)
        public double SumSignScale { get; set; } = 1.4;        // size of the Σ or Π compared to the text
        public double SumSignRaise { get; set; } = -0.14;      // moves the Σ or Π up, a negative value moves it down
        public double SumUpperBoundRaise { get; set; } = 1.2; // how far above the Σ the upper bound sits; bigger moves it up
        public double SumLowerBoundDrop { get; set; } = 0.7;  // how far below the Σ the lower bound sits; bigger moves it down
        public double SumGap { get; set; } = 0.1;              // space between the Σ and the opening bracket


        // === integral ===

        // a big ∫ with its bounds at the top and bottom right, then the integrand and dx
        // (the bounds measured the way they are for Σ)
        public double IntegralSignScale { get; set; } = 1.4;        // size of the ∫ compared to the text
        public double IntegralSignRaise { get; set; } = -0.14;      // moves the ∫ up, a negative value moves it down
        public double IntegralUpperBoundRaise { get; set; } = 0.7;  // how far above the ∫ the upper bound sits; bigger moves it up
        public double IntegralLowerBoundDrop { get; set; } = 0.28;  // how far below the ∫ the lower bound sits; bigger moves it down
        public double IntegralGap { get; set; } = 0.1;              // space between the bounds and the integrand
        public double IntegralDxGap { get; set; } = 0.15;           // space between the integrand and the dx


        // === derivative ===

        // d/dx, the function in brackets, a bar, and the point at its bottom right with x= in front
        public double DerivativePointDrop { get; set; } = 0.3; // how far below the baseline the point sits; bigger moves it down

        // space after the point, so the caret at its end and after the derivative stand apart
        public double DerivativePointPad { get; set; } = 0.06;


        // === recurring decimals ===

        // the bar over the repeating digits; the gap is negative like over a root, closer to 0 moves the bar up
        public double RecurringBarThickness { get; set; } = 0.05;
        public double RecurringBarGap { get; set; } = -0.18;


        // === exponents and subscripts ===

        // how high an exponent and a raised −1 sit, as a share of the height of the base; bigger moves them up
        public double SuperscriptShift { get; set; } = 0.55;

        // how far below the baseline the base of a logarithm sits; bigger moves it down
        public double SubscriptShift { get; set; } = 0.2;


        // === roots ===

        public double RadicalHookWidth { get; set; } = 0.55;     // width of the √ sign in front of the radicand
        public double RadicalRuleThickness { get; set; } = 0.07; // thickness of the bar over the radicand

        // thickness of the √ sign itself, apart from the bar
        public double RadicalHookThickness { get; set; } = 0.05;

        // space between the bar and the radicand; negative, since the box of the digits reaches well above
        // them, and closer to 0 moves the bar up
        public double RadicalVerticalGap { get; set; } = -0.20;

        // how far below the baseline the tip of the √ reaches; bigger moves it down (a deeper radicand
        // takes it down with it)
        public double RadicalBottomDrop { get; set; } = 0.10;

        // space between the √ sign and the radicand
        public double RadicalLeadingPad { get; set; } = 0.08;

        // how far the bar reaches past the radicand, so the caret at its end and after the root stand apart
        public double RadicalTrailingPad { get; set; } = 0.06;

        // how much the two pads shrink with a smaller root: 1 just as much as the root, 0 not at all
        public double RadicalPadScaling { get; set; } = 0.5;

        // how high the index sits, the 3 of a cube root, as a share of the height of the √ sign
        public double RadicalIndexRaise { get; set; } = 0.25;


        // === brackets and bars ===

        // brackets, the bars of |x| and floor and ceiling grow with their content; only width, stroke and
        // space are set here
        public double DelimiterWidth { get; set; } = 0.3;        // width of the bracket itself, how far its curve bulges
        public double DelimiterPadding { get; set; } = 0.05;     // how far the bracket reaches above and below what is inside
        public double DelimiterThickness { get; set; } = 0.06;   // thickness of the stroke
        public double DelimiterSidePadding { get; set; } = 0.05; // space between the bracket and what is inside

        // the share of the content height a bracket takes, before DelimiterPadding (the box of a digit
        // is taller than the digit)
        public double DelimiterHeightScale { get; set; } = 0.85;


        // === the caret ===

        // the caret stands on the baseline of its slot and reaches the top of the digits; in em of that slot
        public double CursorWidth { get; set; } = 0.07;
        public double CursorHeight { get; set; } = 0.71;
        public double CursorShift { get; set; } = -0.04; // moves the caret up, a negative value moves it down
        public double CursorCornerRadius { get; set; } = 0.03;

        // room right of the formula for the half caret at the end; (always kept, so the formula never shifts)
        public double CursorTrailingSpace { get; set; } = 0.06;


        // === empty slots ===

        // an empty slot draws a small square, so it can be seen and the caret has a place to stand
        public double PlaceholderSize { get; set; } = 0.5;       // side of the square
        public double PlaceholderThickness { get; set; } = 0.04; // thickness of its outline

        // how far above the baseline the middle of the square sits; bigger moves it up (0.35 is the middle
        // of a digit)
        public double PlaceholderRaise { get; set; } = 0.35;
    }
}
