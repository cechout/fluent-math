namespace Calculator_WinUI.Models.Layout
{
    // every knob the formula display has, in one place, so a size can be found and turned without first
    // working out which half of the display owns it
    //
    // no WinUI type appears here on purpose: everything under Layout has to build on the plain net8.0
    // runner the tests use, so the colors stay in the markup and only numbers live here
    //
    // every length here is em of the size the piece it belongs to is set at, never a pixel, so the whole
    // formula scales from FontSizePx alone
    public class MathLayoutStyle
    {
        // === the two display lines ===

        // the only thing the two lines differ in; everything below is shared
        public const double InputLineFontSize = 36;
        public const double HistoryLineFontSize = 18;

        public static MathLayoutStyle ForInputLine()
        {
            return new MathLayoutStyle { FontSizePx = InputLineFontSize };
        }

        public static MathLayoutStyle ForHistoryLine()
        {
            return new MathLayoutStyle { FontSizePx = HistoryLineFontSize };
        }


        // === text ===

        public double FontSizePx { get; set; } = InputLineFontSize; // every scale below is relative to it

        // both halves of a fraction stay at full size instead of dropping a level, which is a far taller
        // fraction; the evaluator reads it too, because it decides the shape a result comes back in
        public bool UseDisplayFractions { get; set; } = false;

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
        public double ScriptScriptScale { get; set; } = 0.4;


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
        public double OperatorScale { get; set; } = 0.9;
        public double OperatorGap { get; set; } = 0.10;
        public double OperatorRaise { get; set; } = 0.0;
        public int OperatorWeight { get; set; } = 500; // 400 normal, 600 semibold, 700 bold

        // how far the gap follows the size of the operator it belongs to
        //
        // at 1 it is fully proportional, and inside a fraction it then shrinks twice over, once with the
        // script level and once with the operator scale, which closes it up almost entirely; at 0 it
        // stays the gap of a full size operator however small this one is drawn
        public double OperatorGapScaling { get; set; } = 0.0;


        // === fractions ===

        // the math axis is the line a fraction bar rests on, em above the baseline
        public double MathAxisHeight { get; set; } = 0.25;

        // em, how much further the whole axis is lifted off the baseline
        //
        // a fraction bar and an operator glyph are the two things that hang from the axis, and they are
        // only ever looked at against each other: a minus beside a half reads as wrong the moment the
        // two are a pixel apart. So one number moves the pair, rather than two numbers in two units
        // that have to be converted into each other by hand every time
        //
        // MathAxisHeight and OperatorRaise are what line the two up in the first place, each for its
        // own side; this is what moves them once they are lined up
        public double MathAxisRaise { get; set; } = 0.1;

        public double FractionBarThickness { get; set; } = 0.04;
        public double FractionNumeratorGap { get; set; } = 0.04; // between the bar and the numerator
        public double FractionDenominatorGap { get; set; } = 0.03; // between the bar and the denominator
        public double FractionSidePadding { get; set; } = 0.05; // how far the bar reaches past its content


        // === scripts ===

        // of the ascent of whatever carries the exponent, so a tall base lifts it further
        public double SuperscriptShift { get; set; } = 0.55;

        // em below the baseline, for the base of a logarithm
        public double SubscriptShift { get; set; } = 0.2;


        // === roots ===

        public double RadicalHookWidth { get; set; } = 0.55; // em, the part in front of the radicand
        public double RadicalRuleThickness { get; set; } = 0.08; // em, the bar over the radicand

        // em, the stroke of the sign in front of that bar
        //
        // its own number and not the bars: the bar is a rule and the sign is a letter stroke, and a
        // face that happens to set the two at one weight is no reason for us to tie them together.
        // They are drawn as two paths that meet at the top of the hook, and the round joins there are
        // what keeps the step between two weights from showing
        public double RadicalHookThickness { get; set; } = 0.06;
        public double RadicalVerticalGap { get; set; } = -0.18; // em between that bar and the radicand

        // air between the sign and the radicand
        //
        // the sign fills the hook width to its last pixel and the radicand begins on the next one, so
        // without this the two touch; it is the only knob that moves the content off the sign without
        // making the sign itself wider
        public double RadicalLeadingPad { get; set; } = 0.08;

        // how far the bar reaches past the end of the radicand
        //
        // it is what a radical looks like in print, and it is load bearing besides: without it the end of
        // the radicand and the position behind the whole root are the same pixel, and crossing that place
        // costs a press of an arrow key that changes nothing on screen
        public double RadicalTrailingPad { get; set; } = 0.06;

        // how far the two pads above follow the size of the root they belong to
        //
        // at 1 they are em like everything else, so a root set small inside a fraction keeps its sign
        // exactly as close to its content as a full size one does, only smaller; at the sizes a nested
        // formula reaches that reads as touching, which is the whole reason this knob exists
        //
        // at 0 they stay the air a full size root keeps, whatever size this one ended up at
        public double RadicalPadScaling { get; set; } = 0.5;

        // where the index sits, as a fraction of the height of the sign it stands on
        public double RadicalIndexRaise { get; set; } = 0.25;


        // === delimiters ===

        // a delimiter takes its height from what it encloses, so these are only its width and the air it
        // keeps around the content
        public double DelimiterWidth { get; set; } = 0.3;
        public double DelimiterPadding { get; set; } = 0.05;
        public double DelimiterThickness { get; set; } = 0.06; // em of the stroke it is drawn with

        // em, the air a delimiter keeps between itself and what it encloses
        //
        // it rides on the gaps a row already honours, the same ones an operator asks for, so it costs
        // the layout nothing and reaches a typed bracket and the bracket of a function alike
        //
        // DelimiterWidth is the other horizontal number and a different one: that is how wide the
        // bracket itself is drawn, which is how far its curve bulges rather than how far it stands off
        public double DelimiterSidePadding { get; set; } = 0.0;

        // how much of the reach of what it encloses a delimiter takes
        //
        // a single digit hands over the whole line ascent of the face, the room it keeps for accents
        // and for the tallest letter it sets, and a bracket that takes all of it stands far taller than
        // the digit inside it. The growing past that was always right; the height it started from was
        // not, which is what this trims
        //
        // a bracket around nothing still stands as tall as one around a digit: the reach is floored at
        // the strut before it is scaled, or an empty pair would fall in on itself
        public double DelimiterHeightScale { get; set; } = 0.85;


        // === the caret ===

        // it stands on the baseline of whatever slot it is in and reaches the top of the digits beside
        // it; every value is em, so the caret scales with the slot and with nothing else
        public double CursorWidth { get; set; } = 0.07;
        public double CursorHeight { get; set; } = 0.71;
        public double CursorShift { get; set; } = -0.04; // em above the baseline, negative drops it below
        public double CursorCornerRadius { get; set; } = 0.03;

        // room kept free right of the last position of the formula, where half the caret stands once it
        // is at the end of the line
        //
        // it is reserved whether the caret stands there or not: room that came and went with the caret
        // would shift the whole formula sideways as it reached the end, which is the one thing the caret
        // is never allowed to do
        public double CursorTrailingSpace { get; set; } = 0.06;


        // === empty slots ===

        // an empty slot draws a box rather than collapsing, so it can be seen and a caret has somewhere to
        // stand; a slot with no extent at all would be unreachable
        public double PlaceholderSize { get; set; } = 0.5;       // em, the side of the square
        public double PlaceholderThickness { get; set; } = 0.04; // em of its outline
    }
}
