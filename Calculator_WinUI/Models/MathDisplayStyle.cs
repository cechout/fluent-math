using Microsoft.UI.Xaml;
using System.Globalization;
using System.Text;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Calculator_WinUI.Models
{
    // every tunable of the formula display in one place
    //
    // the display is KaTeX inside a WebView2, so a knob here is really a css custom property; ToCssBlock
    // is the only place that knows the property names, which makes a further knob one property plus one
    // line there
    //
    // the scales below always size a whole structured token, never a single slot of one: KaTeX writes
    // the internal offsets of a fraction or a superscript as inline em values, so scaling the wrapper
    // moves content and alignment together, while scaling only a numerator would leave those offsets
    // sized for the old em and push the content further away from the bar
    public class MathDisplayStyle
    {
        // === theme colors ===

        // the WinUI text brushes written out as literals; resolving a ThemeResource from code needs the
        // explicit theme dictionary, and assigning one as a local value severs the markup expression
        private const string DarkPrimaryText = "#FFFFFF"; // input line, dark theme
        private const string DarkSecondaryText = "#C8C8C8"; // history line, dark theme
        private const string LightPrimaryText = "#1B1B1B"; // input line, light theme
        private const string LightSecondaryText = "#5E5E5E"; // history line, light theme


        // === display knobs ===

        public string TextColor { get; set; } = DarkPrimaryText;
        public double FontSizePx { get; set; } = 36; // base size of the formula, every scale below is relative to it
        public double LineHeight { get; set; } = 1.2; // lower tightens the line box, but KaTeX starts clipping tall structures
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
        public bool UseAccentCursor { get; set; } = true; // the Windows accent rather than the text color

        // filled in by ForInputLine, since the history line has no caret to color
        public string CursorColor { get; set; } = DarkPrimaryText;


        // === presets ===

        // the two display lines differ in color and base size only, so both of them read out of here

        public static MathDisplayStyle ForInputLine(ElementTheme theme)
        {
            return new MathDisplayStyle
            {
                TextColor = theme == ElementTheme.Light ? LightPrimaryText : DarkPrimaryText,
                CursorColor = ResolveAccentColor(theme),
                FontSizePx = 36
            };
        }

        public static MathDisplayStyle ForHistoryLine(ElementTheme theme)
        {
            return new MathDisplayStyle
            {
                TextColor = theme == ElementTheme.Light ? LightSecondaryText : DarkSecondaryText,
                FontSizePx = 18
            };
        }


        // the Windows accent in the variant WinUI picks for its own accent brushes, so the caret stays
        // readable on either background: a light theme takes the darkened accent, a dark one the
        // lightened one
        private static string ResolveAccentColor(ElementTheme theme)
        {
            UIColorType variant = theme == ElementTheme.Light ? UIColorType.AccentDark1 : UIColorType.AccentLight2;
            Color accent = new UISettings().GetColorValue(variant);

            return $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        }


        // === output ===

        // the css the page stamps into the template at load and pushes again after a theme change
        public string ToCssBlock()
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            var css = new StringBuilder();

            css.AppendLine(":root {");
            css.AppendLine($"    --math-color: {TextColor};");
            css.AppendLine($"    --math-size: {FontSizePx.ToString(invariant)}px;");
            css.AppendLine($"    --math-line-height: {LineHeight.ToString(invariant)};");
            css.AppendLine($"    --frac-scale: {FractionScale.ToString(invariant)}em;");
            css.AppendLine($"    --pow-scale: {PowerScale.ToString(invariant)}em;");
            css.AppendLine($"    --root-scale: {RootScale.ToString(invariant)}em;");
            css.AppendLine($"    --log-scale: {LogarithmScale.ToString(invariant)}em;");
            css.AppendLine($"    --func-scale: {FunctionScale.ToString(invariant)}em;");
            css.AppendLine($"    --frac-bar: {FractionBarThickness.ToString(invariant)}em;");
            css.AppendLine($"    --min-fit-scale: {MinFitScale.ToString(invariant)};");
            css.AppendLine($"    --op-scale: {OperatorScale.ToString(invariant)}em;");
            css.AppendLine($"    --op-gap: {OperatorGap.ToString(invariant)}em;");
            css.AppendLine($"    --op-raise: {OperatorRaise.ToString(invariant)}em;");
            css.AppendLine($"    --op-weight: {OperatorWeight.ToString(invariant)};");
            css.AppendLine($"    --cursor-width: {CursorWidth.ToString(invariant)}em;");
            css.AppendLine($"    --cursor-height: {CursorHeight.ToString(invariant)}em;");
            css.AppendLine($"    --cursor-shift: {CursorShift.ToString(invariant)}em;");

            // the one place the accent switch is read, so nothing else has to know about the fallback
            css.AppendLine($"    --cursor-color: {(UseAccentCursor ? CursorColor : TextColor)};");
            css.Append("}");

            return css.ToString();
        }

        // sans-serif is asked for in the formula rather than through a css font-family, because KaTeX
        // lays a formula out from the metrics of the font it believes it is using; \mathsf tells it,
        // while an override behind its back would leave radicals and brackets beside their content
        public string WrapLatex(string latex)
        {
            if (!UseSansSerif) return latex;

            return $"\\mathsf{{{latex}}}";
        }
    }
}
