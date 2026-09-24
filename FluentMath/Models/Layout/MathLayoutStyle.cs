namespace FluentMath.Models.Layout
{
    // every number that shapes the formula display, in one place
    //
    // no WinUI types here: the tests build this folder on plain net8.0, so the colors stay in the markup
    // and only numbers live here
    //
    // lengths are in em: 1.0 is the font size of the part they belong to, so 0.5 is half of it; nothing is
    // in pixels, so the whole formula scales with FontSizePx
    public class MathLayoutStyle
    {
        // === the two display lines ===

        // the two lines only differ in their font size
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

        // true keeps both halves of a fraction at full size instead of the smaller script size, which makes
        // a fraction much taller; the evaluator reads it too
        public bool UseDisplayFractions { get; set; } = false;

        // the smallest a formula that is too tall for its line is shrunk to, 0.45 being 45 percent; below
        // that it is cut off instead
        //
        // it is needed: a stacked fraction at the default size is about twice as tall as the input line
        public double MinFitScale { get; set; } = 0.45;


        // === numbers ===

        // the mark between the whole part and the decimals; the tokens always hold a dot and only the
        // drawing changes it
        public string DecimalMark { get; set; } = ".";

        // with a decimal comma, a comma cannot separate two arguments any more, so a semicolon does, the way
        // a Casio with a decimal comma writes it
        public string ListSeparator => DecimalMark == "," ? ";" : ",";

        // a gap every three digits of the whole part, 1 234 567; the gap is empty space and not a
        // character, so the caret still moves one digit at a time
        public bool GroupDigits { get; set; } = false;
        public double DigitGroupGap { get; set; } = 0.2; // width of that gap; the caret between two groups stands in its middle


        // === script sizes ===

        // numerators, denominators, exponents and bounds are drawn smaller: one level in at ScriptScale,
        // two or more levels in at ScriptScriptScale, and never smaller than that
        //
        // both are compared to the full text size, not to the level above, so deep nesting stops shrinking
        public double ScriptScale { get; set; } = 0.7;
        public double ScriptScriptScale { get; set; } = 0.4;


        // === structures ===

        // the size of a whole structure compared to the text around it; 1.0 keeps it the same
        public double FractionScale { get; set; } = 1.0;
        public double PowerScale { get; set; } = 1.0;
        public double RootScale { get; set; } = 1.0;
        public double LogarithmScale { get; set; } = 1.0;
        public double FunctionScale { get; set; } = 1.0;

        public double ArgumentSeparatorGap { get; set; } = 0.15; // space after the comma between two arguments, as in GCD(12, 18)


        // === operators ===

        // plus, minus, times and divided by are drawn a little smaller and with less space than full size
        // signs, which looks more like a pocket calculator
        //
        // OperatorGap and OperatorRaise are in em of the operator, so they follow OperatorScale
        public double OperatorScale { get; set; } = 0.9; // size of the sign compared to the text
        public double OperatorGap { get; set; } = 0.10;  // space on each side of the sign

        // moves the sign up, a negative value moves it down; a smaller sign also sits a little lower, which
        // this can make up for
        public double OperatorRaise { get; set; } = 0.0;
        public int OperatorWeight { get; set; } = 500; // 400 normal, 600 semibold, 700 bold

        // how much the space around a sign shrinks when the sign is drawn smaller, inside a fraction for
        // example: 1 shrinks it just as much as the sign, 0 keeps the full size space
        //
        // at 1 the space nearly disappears inside a fraction, because it shrinks twice there
        public double OperatorGapScaling { get; set; } = 0.0;


        // === fractions ===

        // the math axis is the height the fraction bars and the middle of plus and minus sit at; it lies
        // MathAxisHeight plus MathAxisRaise above the baseline
        public double MathAxisHeight { get; set; } = 0.25;

        // moves the fraction bars and the operators up together, so they stay lined up with each other; a
        // negative value moves both down
        public double MathAxisRaise { get; set; } = 0.1;

        public double FractionBarThickness { get; set; } = 0.04;
        public double FractionNumeratorGap { get; set; } = 0.04;   // space between the bar and the numerator above it
        public double FractionDenominatorGap { get; set; } = 0.01; // space between the bar and the denominator below it
        public double FractionSidePadding { get; set; } = 0.05;    // how far the bar reaches past the numerator and the denominator on each side
        public double MixedFractionGap { get; set; } = 0.1;        // space between the whole part of a mixed number and its fraction


        // === Σ and Π ===

        // a big Σ or Π, the upper bound above it, the lower bound below it with x= in front, and the body in
        // brackets on the right
        //
        // all in em of the text around it; the bounds are measured from the baseline of the Σ to the
        // baseline of the bound, the line the digits stand on, and a bound taller than a digit, a fraction
        // for example, moves further away by the extra height so it never runs into the Σ
        public double SumSignScale { get; set; } = 1.4;        // size of the Σ or Π compared to the text
        public double SumSignRaise { get; set; } = -0.14;      // moves the Σ or Π up, a negative value moves it down
        public double SumUpperBoundRaise { get; set; } = 1.2; // how far above the Σ the upper bound sits; bigger moves it up
        public double SumLowerBoundDrop { get; set; } = 0.7;  // how far below the Σ the lower bound sits; bigger moves it down
        public double SumGap { get; set; } = 0.1;              // space between the Σ and the opening bracket


        // === integral ===

        // a big ∫, the upper bound at its top right, the lower bound at its bottom right, the integrand and
        // dx at the end; all in em of the text around it, the bounds measured the way they are for Σ
        public double IntegralSignScale { get; set; } = 1.4;        // size of the ∫ compared to the text
        public double IntegralSignRaise { get; set; } = -0.14;      // moves the ∫ up, a negative value moves it down
        public double IntegralUpperBoundRaise { get; set; } = 0.7;  // how far above the ∫ the upper bound sits; bigger moves it up
        public double IntegralLowerBoundDrop { get; set; } = 0.28;  // how far below the ∫ the lower bound sits; bigger moves it down
        public double IntegralGap { get; set; } = 0.1;              // space between the bounds and the integrand
        public double IntegralDxGap { get; set; } = 0.15;           // space between the integrand and the dx


        // === derivative ===

        // d/dx, the function in brackets, a bar, and the point at the bottom right of the bar with x= in
        // front; all in em of the text around it
        public double DerivativePointDrop { get; set; } = 0.3; // how far below the baseline the point sits; bigger moves it down

        // space after the point; it is needed, because without it the end of the point and the place after
        // the whole derivative would be the same pixel, and an arrow press between the two would look like
        // nothing happened
        public double DerivativePointPad { get; set; } = 0.06;


        // === recurring decimals ===

        // the bar over the repeating digits; the gap is negative for the same reason as over a root, the box
        // of the digits reaches well above the digits themselves, and closer to 0 moves the bar up
        public double RecurringBarThickness { get; set; } = 0.05;
        public double RecurringBarGap { get; set; } = -0.18;


        // === exponents and subscripts ===

        // how high an exponent sits, as a share of the height of what it stands on, so a tall base lifts it
        // further; the raised −1 of the reciprocal and of the inverse functions uses it too, and bigger
        // moves them up
        public double SuperscriptShift { get; set; } = 0.55;

        // how far below the baseline the base of a logarithm sits; bigger moves it down
        public double SubscriptShift { get; set; } = 0.2;


        // === roots ===

        public double RadicalHookWidth { get; set; } = 0.55;     // width of the √ sign in front of the radicand
        public double RadicalRuleThickness { get; set; } = 0.08; // thickness of the bar over the radicand

        // thickness of the √ sign itself; a number of its own, so the sign and the bar can differ, and the
        // round joins where the two meet hide the step between them
        public double RadicalHookThickness { get; set; } = 0.06;

        // space between the bar and the radicand below it; negative, because the box of the digits reaches
        // well above the digits themselves and the bar is pulled down into that empty room, and closer to 0
        // moves the bar up
        public double RadicalVerticalGap { get; set; } = -0.18;

        // space between the √ sign and the radicand, so the two do not touch
        public double RadicalLeadingPad { get; set; } = 0.08;

        // how far the bar reaches past the end of the radicand; it is needed besides looking right, because
        // without it the end of the radicand and the place after the root would be the same pixel
        public double RadicalTrailingPad { get; set; } = 0.06;

        // how much the two pads above shrink with a smaller root, inside a fraction for example: 1 shrinks
        // them just as much as the root, 0 keeps the full size space; at 1 a small root looks like it
        // touches its radicand
        public double RadicalPadScaling { get; set; } = 0.5;

        // how high the index sits, the 3 of a cube root, as a share of the height of the √ sign
        public double RadicalIndexRaise { get; set; } = 0.25;


        // === brackets and bars ===

        // brackets, the bars of |x| and the floor and ceiling signs grow with what is inside them, so only
        // their width, their stroke and the space around them are set here
        public double DelimiterWidth { get; set; } = 0.3;        // width of the bracket itself, how far its curve bulges
        public double DelimiterPadding { get; set; } = 0.05;     // how far the bracket reaches above and below what is inside
        public double DelimiterThickness { get; set; } = 0.06;   // thickness of the stroke
        public double DelimiterSidePadding { get; set; } = 0.05; // space between the bracket and what is inside

        // how much of the height of the content a bracket takes, before DelimiterPadding is added
        //
        // the box of a digit is much taller than the digit, so a bracket around one looks too tall at 1.0;
        // an empty pair of brackets is as tall as a pair around a digit
        public double DelimiterHeightScale { get; set; } = 0.85;


        // === the caret ===

        // the caret stands on the baseline of the slot it is in and reaches the top of the digits; it is in
        // em of that slot, so it is smaller in an exponent
        public double CursorWidth { get; set; } = 0.07;
        public double CursorHeight { get; set; } = 0.71;
        public double CursorShift { get; set; } = -0.04; // moves the caret up, a negative value moves it down
        public double CursorCornerRadius { get; set; } = 0.03;

        // empty room kept right of the formula, for the half of the caret that sticks out at the end
        //
        // it is always kept, caret or not, so the formula never shifts sideways when the caret gets there
        public double CursorTrailingSpace { get; set; } = 0.06;


        // === empty slots ===

        // an empty slot draws a small square, so it can be seen and the caret has a place to stand
        public double PlaceholderSize { get; set; } = 0.5;       // side of the square
        public double PlaceholderThickness { get; set; } = 0.04; // thickness of its outline

        // how far above the baseline the middle of the square sits; bigger moves it up, and 0.35 is the
        // middle of a digit, so the square stands where the digit typed into it will
        public double PlaceholderRaise { get; set; } = 0.35;
    }
}
