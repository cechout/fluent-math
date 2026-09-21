using System.Collections.Generic;
using System.Text;

namespace Calculator_WinUI.Models.Layout
{
    // turns a token list into a box tree
    //
    // it walks the lists MathInputManager owns, in the order MathInputManager.GetSlots returns them, so a
    // place in the box tree maps straight back onto a cursor position; that is what the hit test will read
    // later, and it is the reason nothing here reorders anything
    public sealed class MathLayoutEngine
    {
        // === fields ===

        private readonly ITextMeasurer _measurer;
        private readonly MathLayoutStyle _style;

        // the glyph an empty row takes its height from; a digit rather than a letter, because digits are
        // what the display is mostly made of
        private const string StrutText = "0";


        // === construction ===

        public MathLayoutEngine(ITextMeasurer measurer, MathLayoutStyle style)
        {
            _measurer = measurer;
            _style = style;
        }


        // === layout ===

        public RowBox BuildRow(IReadOnlyList<MathToken> tokens)
        {
            return BuildRow(tokens, _style.FontSizePx);
        }

        // the size comes in rather than off the style because a slot one level down is set smaller than
        // the row around it, which is what the structured tokens will need
        public RowBox BuildRow(IReadOnlyList<MathToken> tokens, double fontSize)
        {
            List<MathBox> children = new List<MathBox>();
            int index = 0;

            while (index < tokens.Count)
            {
                if (tokens[index].Type == TokenType.Number)
                {
                    children.Add(BuildNumberRun(tokens, ref index, fontSize));
                    continue;
                }

                if (tokens[index].Type == TokenType.Operator)
                {
                    children.Add(BuildOperator(tokens[index], fontSize));
                    index++;
                    continue;
                }

                children.Add(BuildAtom(tokens[index], fontSize));
                index++;
            }

            if (children.Count == 0) return RowBox.Empty(_measurer.Measure(StrutText, fontSize));

            return new RowBox(children);
        }


        // === pieces ===

        // consecutive digits and the decimal point become one run; a bracket or a constant stays on its
        // own, because a bracket has to scale with what it encloses as soon as it can
        private TextRunBox BuildNumberRun(IReadOnlyList<MathToken> tokens, ref int index, double fontSize)
        {
            StringBuilder text = new StringBuilder();
            List<MathToken> covered = new List<MathToken>();

            while (index < tokens.Count && tokens[index].Type == TokenType.Number)
            {
                text.Append(tokens[index].Value);
                covered.Add(tokens[index]);
                index++;
            }

            string run = text.ToString();
            return new TextRunBox(run, fontSize, _measurer.Measure(run, fontSize), covered);
        }

        private TextRunBox BuildOperator(MathToken token, double fontSize)
        {
            double operatorSize = fontSize * _style.OperatorScale;
            string symbol = OperatorSymbol(token.Value);

            TextRunBox box = new TextRunBox(
                symbol, operatorSize, _measurer.Measure(symbol, operatorSize), new[] { token });

            // both are em of the operator, so they shrink with it rather than with the text around it
            box.Raise = operatorSize * _style.OperatorRaise;
            box.LeadingGap = operatorSize * _style.OperatorGap;
            box.TrailingGap = box.LeadingGap;

            return box;
        }

        private TextRunBox BuildAtom(MathToken token, double fontSize)
        {
            string text = AtomText(token);
            return new TextRunBox(text, fontSize, _measurer.Measure(text, fontSize), new[] { token });
        }


        // === symbols ===

        // --- revisit: structured tokens ---
        // a fraction, a power, a root, a logarithm, a function, a postfix and a scientific token all fall
        // through to their bare value here, which is wrong on screen but never silently empty; each gets a
        // box of its own in the steps that follow, and this default goes with the last of them
        private static string AtomText(MathToken token)
        {
            return token.Type switch
            {
                TokenType.Constant => token.Value == "pi" ? "π" : token.Value,
                TokenType.Answer => "Ans",
                _ => token.Value
            };
        }

        // the three operators that are typed as one character and drawn as another
        private static string OperatorSymbol(string value)
        {
            return value switch
            {
                "*" => "⋅", // dot operator, the sign the latex path writes as \cdot
                "/" => "÷", // division sign
                "-" => "−", // real minus, which is wider and sits higher than a hyphen
                _ => value
            };
        }
    }
}
