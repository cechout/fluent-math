using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FluentMath.Models.Layout
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

        // where the caret ended up, filled in while the boxes are built and read once they are placed
        //
        // it deliberately is not a box: a caret that takes part in the layout moves its neighbours as it
        // travels, whatever its width, and that is the one mistake this display has to keep not making
        public CaretPlacement? Caret { get; private set; }

        // the glyph an empty row takes its height from; a digit rather than a letter, because digits are
        // what the display is mostly made of
        private const string StrutText = "0";

        // signs the display draws that no token carries as its value
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
            bool carriesCaret = CaretIsIn(tokens);

            List<MathBox> children = new List<MathBox>();
            MathBox caretBox = null;
            double caretOffset = 0;
            int index = 0;

            while (index < tokens.Count)
            {
                int start = index;
                MathBox box;

                if (tokens[index].Type == TokenType.Number)
                {
                    box = BuildNumberRun(tokens, ref index, fontSize, path);
                }
                else
                {
                    box = BuildToken(tokens[index], fontSize, scriptLevel, path, index);
                    box.CursorAddress = Address(path, index);
                    index++;
                }

                children.Add(box);

                // the caret hangs off the box it stands in front of, or inside the run it stands in
                if (carriesCaret && _caret.Index >= start && _caret.Index < index)
                {
                    caretBox = box;
                    caretOffset = box is TextRunBox run ? OffsetInRun(run, _caret.Index - start) : 0;
                }
            }

            StretchTypedBrackets(children, fontSize);

            RowBox row;
            if (children.Count == 0)
            {
                row = RowBox.Empty(_measurer.Measure(StrutText, fontSize));
                row.EndAddress = Address(path, 0);
            }
            else
            {
                row = new RowBox(children) { EndAddress = Address(path, tokens.Count) };
            }

            row.FontSize = fontSize;

            // past the last token there is no box to hang off, so it hangs off the row itself
            if (carriesCaret && _caret.Index >= tokens.Count)
            {
                caretBox = row;
                caretOffset = row.Width;
            }

            // reported once the row exists, because the row is what the caret takes its height from
            if (carriesCaret && caretBox != null)
            {
                Caret = new CaretPlacement(caretBox, caretOffset, row, fontSize);
            }

            return row;
        }

        private MathBox BuildToken(MathToken token, double fontSize, int scriptLevel,
            string path, int tokenIndex)
        {
            switch (token)
            {
                case FractionToken fraction: return BuildFraction(fraction, fontSize, scriptLevel, path, tokenIndex);
                case PowerToken power: return BuildPower(power, fontSize, scriptLevel, path, tokenIndex);
                case RootToken root: return BuildRoot(root, fontSize, scriptLevel, path, tokenIndex);
                case LogarithmToken logarithm: return BuildLogarithm(logarithm, fontSize, scriptLevel, path, tokenIndex);
                case FunctionToken function: return BuildFunction(function, fontSize, scriptLevel, path, tokenIndex);
                case PostfixToken postfix: return BuildPostfix(postfix, fontSize, scriptLevel);
            }

            if (token.Type == TokenType.Operator) return BuildOperator(token, fontSize);

            if (token.Type == TokenType.BracketOpen || token.Type == TokenType.BracketClose)
            {
                return BuildTypedBracket(token, fontSize);
            }

            return BuildAtom(token, fontSize);
        }


        // === leaves ===

        // consecutive digits and the decimal point become one run; a bracket or a constant stays on its
        // own, because a bracket has to scale with what it encloses as soon as it can
        private TextRunBox BuildNumberRun(IReadOnlyList<MathToken> tokens, ref int index, double fontSize,
            string path)
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

            return Addressed(new TextRunBox(run, fontSize, _measurer.Measure(run, fontSize), covered),
                path, start, fontSize);
        }

        // how far into a run the caret stands, when it stands between two digits of the same number
        //
        // the run itself is measured and drawn whole; only the caret is placed inside it. Splitting the
        // run instead would hand the text stack two pieces to set, each with the side bearings of its own
        // first glyph, and the digits either side of the caret would shift as it passed between them
        private static double OffsetInRun(TextRunBox run, int tokenOffset)
        {
            if (tokenOffset <= 0) return 0;
            if (run.TokenOffsets == null || tokenOffset >= run.TokenOffsets.Count) return run.Width;

            return run.TokenOffsets[tokenOffset];
        }

        // a run stands in front of several cursor positions at once, one per digit it covers, so a click
        // between two digits of the same number can land between them
        //
        // each of them is measured rather than assumed to be an equal share of the width: a decimal point
        // is far narrower than a digit, and the caret and a click have to agree on where the gap is
        //
        // the prefixes cost nothing after the first keystroke, since the measurer keeps every answer and a
        // formula asks for the same handful of runs again on every rebuild
        private TextRunBox Addressed(TextRunBox run, string path, int firstTokenIndex, double fontSize)
        {
            string[] addresses = new string[run.Tokens.Count];
            double[] offsets = new double[run.Tokens.Count];
            StringBuilder prefix = new StringBuilder();

            for (int offset = 0; offset < addresses.Length; offset++)
            {
                addresses[offset] = Address(path, firstTokenIndex + offset);
                offsets[offset] = offset == 0 ? 0 : _measurer.Measure(prefix.ToString(), fontSize).Width;

                prefix.Append(run.Tokens[offset].Value);
            }

            run.CursorAddress = addresses.Length > 0 ? addresses[0] : null;
            run.TokenAddresses = addresses;
            run.TokenOffsets = offsets;

            return run;
        }

        // the caret belongs to exactly one list, and it is that list by identity rather than by contents;
        // two empty slots are equal by value and only the reference tells them apart
        private bool CaretIsIn(IReadOnlyList<MathToken> tokens)
        {
            return _caret.Tokens != null && ReferenceEquals(tokens, _caret.Tokens);
        }

        private TextRunBox BuildOperator(MathToken token, double fontSize)
        {
            double operatorSize = fontSize * _style.OperatorScale;
            TextRunBox box = TextRun(OperatorSymbol(token.Value), operatorSize, token);

            // the raise is em of the operator, so it shrinks with it rather than with the text around
            // it; the axis on top of it is em of the text, which is what makes it the very lift the
            // fraction bar beside it gets
            box.Raise = operatorSize * _style.OperatorRaise + fontSize * _style.MathAxisRaise;

            // the gap follows the size only as far as OperatorGapScaling says, because a gap that is
            // fully proportional shrinks twice inside a fraction and closes up
            double gapSize = GapAt(operatorSize, _style.FontSizePx * _style.OperatorScale,
                _style.OperatorGapScaling);

            box.LeadingGap = gapSize * _style.OperatorGap;
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
                size * (_style.MathAxisHeight + _style.MathAxisRaise),
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

        private RootBox BuildRoot(RootToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.RootScale;

            // an empty index stays invisible rather than drawing a placeholder, so a square root looks
            // like one; it appears the moment the caret walks into it, which is the only way a slot that
            // shows nothing can be reached at all
            MathBox index = token.IndexTokens.Count == 0 && !CaretIsIn(token.IndexTokens)
                ? null
                : BuildSlot(token.IndexTokens, size * _style.ScriptScriptScale, scriptLevel + 2, SlotPath(path, tokenIndex, 0));

            // the air either side of the radicand follows the size only as far as RadicalPadScaling
            // says; fully proportional it closes up on a root set small inside a fraction, which reads
            // as the sign touching what it encloses
            double padSize = GapAt(size, _style.FontSizePx * _style.RootScale, _style.RadicalPadScaling);

            return new RootBox(
                index,
                BuildSlot(token.RadicandTokens, size, scriptLevel, SlotPath(path, tokenIndex, 1)),
                size * _style.RadicalHookWidth,
                size * _style.RadicalRuleThickness,
                size * _style.RadicalHookThickness,
                size * _style.RadicalVerticalGap,
                _style.RadicalIndexRaise,
                padSize * _style.RadicalLeadingPad,
                padSize * _style.RadicalTrailingPad);
        }

        private RowBox BuildLogarithm(LogarithmToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.LogarithmScale;
            List<MathBox> parts = new List<MathBox> { TextRun("log", size, token) };

            // an empty base stays invisible, the same way an empty root index does, and comes back the
            // same way too
            if (token.BaseTokens.Count > 0 || CaretIsIn(token.BaseTokens))
            {
                MathBox logBase = BuildSlot(token.BaseTokens, ScriptSize(size, scriptLevel), scriptLevel + 1, SlotPath(path, tokenIndex, 0));
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

            // the box is the same whether the caret stands in it or not, which is what keeps the slot
            // from changing size as the caret walks in and out of it
            PlaceholderBox placeholder = new PlaceholderBox(
                fontSize * _style.PlaceholderSize,
                fontSize * _style.PlaceholderThickness,
                _measurer.Measure(StrutText, fontSize));

            placeholder.CursorAddress = Address(path, 0);
            placeholder.FontSize = fontSize;

            if (CaretIsIn(tokens)) Caret = new CaretPlacement(placeholder, 0, placeholder, fontSize);

            return placeholder;
        }

        // a delimiter takes its height from what it encloses, which is why it is a box of its own rather
        // than a character inside a run
        private void AddDelimited(List<MathBox> parts, MathBox content,
            DelimiterKind open, DelimiterKind close, double fontSize)
        {
            double width = fontSize * _style.DelimiterWidth;
            double thickness = fontSize * _style.DelimiterThickness;

            (double ascent, double descent) = DelimiterReach(content.Ascent, content.Descent, fontSize);

            DelimiterBox opening = new DelimiterBox(open, width, ascent, descent, thickness);
            DelimiterBox closing = new DelimiterBox(close, width, ascent, descent, thickness);

            double air = fontSize * _style.DelimiterSidePadding;
            opening.TrailingGap = air;
            closing.LeadingGap = air;

            parts.Add(opening);
            parts.Add(content);
            parts.Add(closing);
        }

        // how far a delimiter reaches around a content of this size, for a function and for a typed
        // bracket alike, so the two can never end up shaped differently
        //
        // the floor is the strut rather than a number of its own: a bracket around nothing stands as
        // tall as one around a digit, which is the only sensible thing an empty pair can do
        private (double Ascent, double Descent) DelimiterReach(double ascent, double descent, double fontSize)
        {
            TextMetrics strut = _measurer.Measure(StrutText, fontSize);
            double padding = fontSize * _style.DelimiterPadding;

            return (Math.Max(ascent, strut.Ascent) * _style.DelimiterHeightScale + padding,
                Math.Max(descent, strut.Descent) * _style.DelimiterHeightScale + padding);
        }

        // a typed bracket is a delimiter and not a glyph, so it can grow with what it encloses the way
        // the bracket of a function does; it is built at the floor and stretched once the row is known
        private DelimiterBox BuildTypedBracket(MathToken token, double fontSize)
        {
            DelimiterKind kind = token.Type == TokenType.BracketOpen
                ? DelimiterKind.ParenthesisOpen
                : DelimiterKind.ParenthesisClose;

            (double ascent, double descent) = DelimiterReach(0, 0, fontSize);

            DelimiterBox bracket = new DelimiterBox(kind, fontSize * _style.DelimiterWidth,
                ascent, descent, fontSize * _style.DelimiterThickness);

            // the air goes on the side the content is on, which is the only side it has one
            double air = fontSize * _style.DelimiterSidePadding;
            if (kind == DelimiterKind.ParenthesisOpen) bracket.TrailingGap = air;
            else bracket.LeadingGap = air;

            return bracket;
        }

        // every typed bracket takes its height from what stands between it and its partner
        //
        // it runs over the boxes of the row and not over its tokens, because only a box knows how far it
        // reaches, and it runs before the RowBox is built, because a row works its own reach out of the
        // children it is handed
        //
        // the row stays flat and nothing is wrapped. The tokens between two typed brackets live in the
        // same list the brackets do, unlike the parameter of a function which is a list of its own; a
        // group around them would take every cursor position inside it out of the line a click is
        // resolved against
        //
        // a bracket with nothing to pair it off reaches to the end of the row, or back to the start for
        // a lone closing one. That is what keeps it growing while a formula is still being typed rather
        // than snapping to height the moment it is closed
        private void StretchTypedBrackets(List<MathBox> children, double fontSize)
        {
            List<int> open = new List<int>();

            for (int index = 0; index < children.Count; index++)
            {
                if (children[index] is not DelimiterBox delimiter) continue;

                if (delimiter.Kind == DelimiterKind.ParenthesisOpen)
                {
                    open.Add(index);
                    continue;
                }

                if (delimiter.Kind != DelimiterKind.ParenthesisClose) continue;

                if (open.Count == 0)
                {
                    StretchPair(children, -1, index, fontSize);
                    continue;
                }

                int start = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);

                StretchPair(children, start, index, fontSize);
            }

            // innermost first, so an outer bracket is measured against an inner one that already grew
            for (int index = open.Count - 1; index >= 0; index--)
            {
                StretchPair(children, open[index], children.Count, fontSize);
            }
        }

        private void StretchPair(List<MathBox> children, int openIndex, int closeIndex, double fontSize)
        {
            double ascent = 0;
            double descent = 0;

            for (int index = openIndex + 1; index < closeIndex; index++)
            {
                MathBox child = children[index];

                // the reach a row works out of its children, raise and all
                ascent = Math.Max(ascent, child.Ascent + child.Raise);
                descent = Math.Max(descent, child.Descent - child.Raise);
            }

            (double reachUp, double reachDown) = DelimiterReach(ascent, descent, fontSize);

            if (openIndex >= 0) ((DelimiterBox)children[openIndex]).Stretch(reachUp, reachDown);
            if (closeIndex < children.Count) ((DelimiterBox)children[closeIndex]).Stretch(reachUp, reachDown);
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

        // how wide a gap is once the piece it belongs to has been set smaller than the line it is in
        //
        // a gap written in em shrinks with its piece, and inside a structure that is itself set smaller
        // it shrinks twice over and closes up. 1 leaves it fully proportional, 0 keeps the gap the piece
        // would have at full size however small it ended up, and anything between splits the difference
        private static double GapAt(double size, double fullSize, double scaling)
        {
            return fullSize + (size - fullSize) * scaling;
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
                "*" => "×", // multiplication sign, the one a pocket calculator prints
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
