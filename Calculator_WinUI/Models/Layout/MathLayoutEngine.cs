using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private readonly CaretTarget _caret;

        // the glyph an empty row takes its height from; a digit rather than a letter, because digits are
        // what the display is mostly made of
        private const string StrutText = "0";

        // signs the display draws that no token carries as its value
        private const string TimesSign = "×"; // the scientific form
        private const string MinusOne = "−1"; // the reciprocal and the inverse hyperbolics


        // === construction ===

        // the caret is left out for the history line, which has none
        public MathLayoutEngine(ITextMeasurer measurer, MathLayoutStyle style, CaretTarget caret = default)
        {
            _measurer = measurer;
            _style = style;
            _caret = caret;
        }


        // === layout ===

        public RowBox BuildRow(IReadOnlyList<MathToken> tokens)
        {
            return BuildRow(tokens, _style.FontSizePx, 0, "");
        }

        // the size and the level both come in rather than off the style, because a slot one step further
        // in is set smaller than the row around it and has to know how much further it may still shrink
        // path is the address of this list, empty at the root; it is built exactly the way
        // LatexRenderContext builds it, so MathInputManager.SetCursorPosition parses what comes out
        public RowBox BuildRow(IReadOnlyList<MathToken> tokens, double fontSize, int scriptLevel,
            string path = "")
        {
            // the caret belongs to exactly one list, the one being written into, and it is that list by
            // identity rather than by contents
            bool carriesCaret = _caret.Tokens != null && ReferenceEquals(tokens, _caret.Tokens);

            List<MathBox> children = new List<MathBox>();
            int index = 0;

            while (index < tokens.Count)
            {
                if (carriesCaret && index == _caret.Index) children.Add(Caret(fontSize));

                if (tokens[index].Type == TokenType.Number)
                {
                    AddNumberRun(children, tokens, ref index, fontSize, carriesCaret, path);
                    continue;
                }

                MathBox box = BuildToken(tokens[index], fontSize, scriptLevel, path, index);
                box.CursorAddress = Address(path, index);
                children.Add(box);
                index++;
            }

            if (carriesCaret && _caret.Index >= tokens.Count) children.Add(Caret(fontSize));

            if (children.Count == 0)
            {
                RowBox empty = RowBox.Empty(_measurer.Measure(StrutText, fontSize));
                empty.EndAddress = Address(path, 0);

                return empty;
            }

            return new RowBox(children) { EndAddress = Address(path, tokens.Count) };
        }

        private MathBox BuildToken(MathToken token, double fontSize, int scriptLevel,
            string path, int tokenIndex)
        {
            switch (token)
            {
                case FractionToken fraction: return BuildFraction(fraction, fontSize, scriptLevel, path, tokenIndex);
                case PowerToken power: return BuildPower(power, fontSize, scriptLevel, path, tokenIndex);
                case ScientificToken scientific: return BuildScientific(scientific, fontSize, scriptLevel, path, tokenIndex);
                case RootToken root: return BuildRoot(root, fontSize, scriptLevel, path, tokenIndex);
                case LogarithmToken logarithm: return BuildLogarithm(logarithm, fontSize, scriptLevel, path, tokenIndex);
                case FunctionToken function: return BuildFunction(function, fontSize, scriptLevel, path, tokenIndex);
                case PostfixToken postfix: return BuildPostfix(postfix, fontSize, scriptLevel);
            }

            if (token.Type == TokenType.Operator) return BuildOperator(token, fontSize);

            return BuildAtom(token, fontSize);
        }


        // === leaves ===

        // consecutive digits and the decimal point become one run; a bracket or a constant stays on its
        // own, because a bracket has to scale with what it encloses as soon as it can
        private void AddNumberRun(List<MathBox> children, IReadOnlyList<MathToken> tokens, ref int index,
            double fontSize, bool carriesCaret, string path)
        {
            int start = index;
            StringBuilder text = new StringBuilder();
            List<MathToken> covered = new List<MathToken>();

            while (index < tokens.Count && tokens[index].Type == TokenType.Number)
            {
                text.Append(tokens[index].Value);
                covered.Add(tokens[index]);
                index++;
            }

            string run = text.ToString();
            TextMetrics whole = _measurer.Measure(run, fontSize);

            if (!carriesCaret || _caret.Index <= start || _caret.Index >= index)
            {
                children.Add(Addressed(new TextRunBox(run, fontSize, whole, covered), path, start));
                return;
            }

            // the caret can stand between two digits of the same number, and only then is the run split
            //
            // the second half is sized as the whole minus the first rather than measured on its own, so
            // the two always add up to the width the run has without a caret in it and nothing beside it
            // shifts as the caret travels through
            int split = _caret.Index - start;
            string left = string.Concat(covered.Take(split).Select(token => token.Value));
            double leftWidth = _measurer.Measure(left, fontSize).Width;

            children.Add(Addressed(new TextRunBox(left, fontSize,
                new TextMetrics(leftWidth, whole.Ascent, whole.Descent), covered.Take(split).ToList()),
                path, start));

            children.Add(Caret(fontSize));

            children.Add(Addressed(new TextRunBox(run.Substring(left.Length), fontSize,
                new TextMetrics(whole.Width - leftWidth, whole.Ascent, whole.Descent),
                covered.Skip(split).ToList()), path, _caret.Index));
        }

        // a run stands in front of several cursor positions at once, one per digit it covers, so a click
        // between two digits of the same number can land between them
        private static TextRunBox Addressed(TextRunBox run, string path, int firstTokenIndex)
        {
            string[] addresses = new string[run.Tokens.Count];
            for (int offset = 0; offset < addresses.Length; offset++)
            {
                addresses[offset] = Address(path, firstTokenIndex + offset);
            }

            run.CursorAddress = addresses.Length > 0 ? addresses[0] : null;
            run.TokenAddresses = addresses;

            return run;
        }

        private CaretBox Caret(double fontSize)
        {
            return new CaretBox(fontSize, _measurer.Measure(StrutText, fontSize));
        }

        private TextRunBox BuildOperator(MathToken token, double fontSize)
        {
            double operatorSize = fontSize * _style.OperatorScale;
            TextRunBox box = TextRun(OperatorSymbol(token.Value), operatorSize, token);

            // both are em of the operator, so they shrink with it rather than with the text around it
            box.Raise = operatorSize * _style.OperatorRaise;
            box.LeadingGap = operatorSize * _style.OperatorGap;
            box.TrailingGap = box.LeadingGap;

            return box;
        }

        private TextRunBox BuildAtom(MathToken token, double fontSize)
        {
            return TextRun(AtomText(token), fontSize, token);
        }


        // === structures ===

        private FractionBox BuildFraction(FractionToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.FractionScale;

            // both halves drop a level, unless a display fraction is asked for, which keeps them at full
            // size and takes the wider clearances with it
            bool display = _style.UseDisplayFractions;
            double innerSize = display ? size : ScriptSize(size, scriptLevel);
            int innerLevel = display ? scriptLevel : scriptLevel + 1;

            return new FractionBox(
                BuildSlot(token.NumeratorTokens, innerSize, innerLevel, SlotPath(path, tokenIndex, 0)),
                BuildSlot(token.DenominatorTokens, innerSize, innerLevel, SlotPath(path, tokenIndex, 1)),
                size * _style.FractionBarThickness,
                size * _style.MathAxisHeight,
                size * _style.FractionNumeratorGap,
                size * _style.FractionDenominatorGap,
                size * _style.FractionSidePadding);
        }

        // the base is a slot of its own rather than whatever atom happens to stand in front, which is the
        // whole difference to writing this as latex: nothing has to be braced and nothing can bind to the
        // wrong thing
        private RowBox BuildPower(PowerToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.PowerScale;

            MathBox baseBox = BuildSlot(token.BaseTokens, size, scriptLevel, SlotPath(path, tokenIndex, 0));
            MathBox exponent = BuildSlot(token.ExponentTokens, ScriptSize(size, scriptLevel), scriptLevel + 1, SlotPath(path, tokenIndex, 1));
            exponent.Raise = baseBox.Ascent * _style.SuperscriptShift;

            return new RowBox(new List<MathBox> { baseBox, exponent });
        }

        // the EXP key, drawn as the times ten to the n it stands for
        private RowBox BuildScientific(ScientificToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.PowerScale;
            double operatorSize = size * _style.OperatorScale;

            TextRunBox times = TextRun(TimesSign, operatorSize, token);
            times.Raise = operatorSize * _style.OperatorRaise;
            times.LeadingGap = operatorSize * _style.OperatorGap;
            times.TrailingGap = times.LeadingGap;

            TextRunBox ten = TextRun("10", size, token);

            MathBox exponent = BuildSlot(token.ExponentTokens, ScriptSize(size, scriptLevel), scriptLevel + 1, SlotPath(path, tokenIndex, 0));
            exponent.Raise = ten.Ascent * _style.SuperscriptShift;

            return new RowBox(new List<MathBox> { times, ten, exponent });
        }

        private RootBox BuildRoot(RootToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.RootScale;

            // an empty index stays invisible rather than drawing a placeholder, so a square root looks
            // like one; it only appears once the caret walks into it
            MathBox index = token.IndexTokens.Count == 0
                ? null
                : BuildRow(token.IndexTokens, size * _style.ScriptScriptScale, scriptLevel + 2, SlotPath(path, tokenIndex, 0));

            return new RootBox(
                index,
                BuildSlot(token.RadicandTokens, size, scriptLevel, SlotPath(path, tokenIndex, 1)),
                size * _style.RadicalHookWidth,
                size * _style.RadicalRuleThickness,
                size * _style.RadicalVerticalGap,
                _style.RadicalIndexRaise);
        }

        private RowBox BuildLogarithm(LogarithmToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.LogarithmScale;
            List<MathBox> parts = new List<MathBox> { TextRun("log", size, token) };

            // an empty base stays invisible, the same way an empty root index does
            if (token.BaseTokens.Count > 0)
            {
                MathBox logBase = BuildRow(token.BaseTokens, ScriptSize(size, scriptLevel), scriptLevel + 1, SlotPath(path, tokenIndex, 0));
                logBase.Raise = -size * _style.SubscriptShift;
                parts.Add(logBase);
            }

            AddDelimited(parts, BuildSlot(token.ParameterTokens, size, scriptLevel, SlotPath(path, tokenIndex, 1)),
                DelimiterKind.ParenthesisOpen, DelimiterKind.ParenthesisClose, size);

            return new RowBox(parts);
        }

        private RowBox BuildFunction(FunctionToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.FunctionScale;
            MathBox parameter = BuildSlot(token.ParameterTokens, size, scriptLevel, SlotPath(path, tokenIndex, 0));
            List<MathBox> parts = new List<MathBox>();

            if (token.DrawsAsBars)
            {
                AddDelimited(parts, parameter, DelimiterKind.Bar, DelimiterKind.Bar, size);
                return new RowBox(parts);
            }

            (string name, bool inverse) = FunctionParts(token.Value);
            TextRunBox nameRun = TextRun(name, size, token);
            parts.Add(nameRun);

            if (inverse)
            {
                TextRunBox raised = TextRun(MinusOne, ScriptSize(size, scriptLevel), token);
                raised.Raise = nameRun.Ascent * _style.SuperscriptShift;
                parts.Add(raised);
            }

            AddDelimited(parts, parameter,
                DelimiterKind.ParenthesisOpen, DelimiterKind.ParenthesisClose, size);

            return new RowBox(parts);
        }

        // the factorial and the percent stand behind their operand as plain signs; the reciprocal is a
        // raised minus one, the way a Casio prints it
        private MathBox BuildPostfix(PostfixToken token, double fontSize, int scriptLevel)
        {
            if (token.Value != "inv") return BuildAtom(token, fontSize);

            TextRunBox raised = TextRun(MinusOne, ScriptSize(fontSize, scriptLevel), token);

            // there is no base box to measure here, since the operand is whatever precedes this token in
            // the row, so the lift comes off a digit instead
            raised.Raise = _measurer.Measure(StrutText, fontSize).Ascent * _style.SuperscriptShift;

            return raised;
        }


        // === helpers ===

        // a slot that holds nothing still has to occupy space, or it cannot be seen and a caret cannot
        // stand in it
        private MathBox BuildSlot(IReadOnlyList<MathToken> tokens, double fontSize, int scriptLevel,
            string path)
        {
            if (tokens.Count > 0) return BuildRow(tokens, fontSize, scriptLevel, path);

            PlaceholderBox placeholder = new PlaceholderBox(
                fontSize * _style.PlaceholderSize, fontSize * _style.PlaceholderThickness);

            placeholder.CursorAddress = Address(path, 0);
            if (_caret.Tokens == null || !ReferenceEquals(tokens, _caret.Tokens)) return placeholder;

            // an empty slot keeps its box while the caret stands in it rather than letting the caret
            // replace it, or the slot would vanish the moment the caret moved on and there would be
            // nothing left to walk back into
            return new RowBox(new List<MathBox> { Caret(fontSize), placeholder })
            {
                EndAddress = Address(path, 0)
            };
        }

        // a delimiter takes its height from what it encloses, which is why it is a box of its own rather
        // than a character inside a run
        private void AddDelimited(List<MathBox> parts, MathBox content,
            DelimiterKind open, DelimiterKind close, double fontSize)
        {
            double padding = fontSize * _style.DelimiterPadding;
            double width = fontSize * _style.DelimiterWidth;
            double ascent = content.Ascent + padding;
            double descent = content.Descent + padding;

            double thickness = fontSize * _style.DelimiterThickness;

            parts.Add(new DelimiterBox(open, width, ascent, descent, thickness));
            parts.Add(content);
            parts.Add(new DelimiterBox(close, width, ascent, descent, thickness));
        }

        private TextRunBox TextRun(string text, double fontSize, MathToken token)
        {
            return new TextRunBox(text, fontSize, _measurer.Measure(text, fontSize), new[] { token });
        }

        // the size one step further in
        //
        // the two ratios are of the base size rather than of the level above, so the step from the second
        // level to the third is a factor of one and a deeply nested formula stops shrinking instead of
        // vanishing
        // one cursor position, and the address of one slot of one token; both are built exactly the way
        // LatexRenderContext builds them, which is what lets the input manager parse either of them
        private static string Address(string path, int cursorIndex)
        {
            return path + "@" + cursorIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string SlotPath(string path, int tokenIndex, int slotIndex)
        {
            string step = tokenIndex.ToString(CultureInfo.InvariantCulture)
                + "." + slotIndex.ToString(CultureInfo.InvariantCulture);

            return path.Length == 0 ? step : path + "/" + step;
        }

        private double ScriptSize(double fontSize, int fromLevel)
        {
            return fontSize * (LevelScale(fromLevel + 1) / LevelScale(fromLevel));
        }

        private double LevelScale(int level)
        {
            if (level <= 0) return 1.0;
            if (level == 1) return _style.ScriptScale;

            return _style.ScriptScriptScale;
        }


        // === symbols ===

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

        // the inverse hyperbolics print as the plain function carrying a raised minus one, the way a Casio
        // does; FunctionToken spells the same thing out for the latex path as sinh^{-1}, so the two lists
        // have to move together
        private static (string Name, bool Inverse) FunctionParts(string functionName)
        {
            return functionName switch
            {
                "arsinh" => ("sinh", true),
                "arcosh" => ("cosh", true),
                "artanh" => ("tanh", true),
                _ => (functionName, false)
            };
        }
    }
}
