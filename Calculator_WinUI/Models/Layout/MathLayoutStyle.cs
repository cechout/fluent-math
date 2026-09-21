namespace Calculator_WinUI.Models.Layout
{
    // the numbers the layout needs, kept apart from MathDisplayStyle because everything under Layout has
    // to stay free of WinUI: MathDisplayStyle resolves the accent through UISettings and branches on
    // ElementTheme, neither of which exists on the plain net8.0 runner the tests use
    //
    // MathDisplayStyle keeps the colors and the display knobs that only matter once something is drawn,
    // and hands one of these down
    public class MathLayoutStyle
    {
        // === text ===

        public double FontSizePx { get; set; } = 36; // base size, every scale below is relative to it


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
    }
}
