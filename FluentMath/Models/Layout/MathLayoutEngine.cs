using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FluentMath.Models.Layout
{
    // turns a token list into a box tree, in the slot order of MathInputManager.GetSlots, so a place in
    // the tree maps straight back onto a cursor position
    public sealed class MathLayoutEngine
    {
        // === fields ===

        private readonly ITextMeasurer _measurer;
        private readonly MathLayoutStyle _style;
        private readonly CaretTarget _caret;

        // where the caret ended up, read once the boxes are placed; (no box, or it would move its neighbours)
        public CaretPlacement? Caret { get; private set; }

        // the glyph an empty row takes its height from
        private const string StrutText = "0";

        // signs the display draws that no token carries as its value
        private const string MinusOne = "−1"; // the reciprocal and the inverse hyperbolics
        private const string VariableEquals = "x="; // in front of the lower bound of Σ and Π and the point of a derivative
        private const string Differential = "dx"; // behind an integrand, and under the d of a derivative


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

        // the size and script level come in, since a slot further in is set smaller
        // (path is the address of this list, empty at the root, built the way LatexRenderContext builds it)
        public RowBox BuildRow(IReadOnlyList<MathToken> tokens, double fontSize, int scriptLevel,
            string path = "")
        {
            bool carriesCaret = CaretIsIn(tokens);

            List<MathBox> children = new List<MathBox>();
            MathBox caretBox = null;
            double caretOffset = 0;
            int index = 0;

            // the caret hangs off the box it stands in front of, or inside the run it stands in
            void Add(MathBox box, int start, int end)
            {
                children.Add(box);

                if (carriesCaret && _caret.Index >= start && _caret.Index < end)
                {
                    caretBox = box;
                    caretOffset = box is TextRunBox run ? OffsetInRun(run, _caret.Index - start) : 0;
                }
            }

            while (index < tokens.Count)
            {
                if (tokens[index].Type == TokenType.Number)
                {
                    int runEnd = index;
                    while (runEnd < tokens.Count && tokens[runEnd].Type == TokenType.Number) runEnd++;

                    List<TextRunBox> groups = BuildNumberRun(tokens, index, runEnd, fontSize, path);
                    foreach (TextRunBox group in groups)
                    {
                        Add(group, index, index + group.Tokens.Count);
                        index += group.Tokens.Count;
                    }

                    continue;
                }

                MathBox box = BuildToken(tokens[index], fontSize, scriptLevel, path, index);
                box.CursorAddress = Address(path, index);
                Add(box, index, index + 1);
                index++;
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
                case MixedFractionToken mixed: return BuildMixedFraction(mixed, fontSize, scriptLevel, path, tokenIndex);
                case PowerToken power: return BuildPower(power, fontSize, scriptLevel, path, tokenIndex);
                case RootToken root: return BuildRoot(root, fontSize, scriptLevel, path, tokenIndex);
                case LogarithmToken logarithm: return BuildLogarithm(logarithm, fontSize, scriptLevel, path, tokenIndex);
                case FunctionToken function: return BuildFunction(function, fontSize, scriptLevel, path, tokenIndex);
                case LargeOperatorToken integral when integral.Kind == LargeOperatorKind.Integral:
                    return BuildIntegral(integral, fontSize, scriptLevel, path, tokenIndex);
                case LargeOperatorToken series: return BuildSeries(series, fontSize, scriptLevel, path, tokenIndex);
                case DerivativeToken derivative: return BuildDerivative(derivative, fontSize, scriptLevel, path, tokenIndex);
                case PostfixToken postfix: return BuildPostfix(postfix, fontSize, scriptLevel);
                case RecurringToken recurring: return BuildRecurring(recurring, fontSize);
            }

            if (token.Type == TokenType.Operator && token.Value == "÷R") return BuildRemainderDivision(token, fontSize);
            if (token.Type == TokenType.Operator) return BuildOperator(token, fontSize);

            if (token.Type == TokenType.BracketOpen || token.Type == TokenType.BracketClose)
            {
                return BuildTypedBracket(token, fontSize);
            }

            return BuildAtom(token, fontSize);
        }


        // === leaves ===

        // consecutive digits and the decimal point become one run
        // (with digit grouping, cut where a group of three begins; the cuts never depend on the caret)
        private List<TextRunBox> BuildNumberRun(IReadOnlyList<MathToken> tokens, int start, int end,
            double fontSize, string path)
        {
            List<TextRunBox> groups = new List<TextRunBox>();
            List<int> cuts = _style.GroupDigits ? GroupCuts(tokens, start, end) : new List<int>();
            cuts.Add(end);

            double gap = fontSize * _style.DigitGroupGap;
            int from = start;

            foreach (int cut in cuts)
            {
                StringBuilder text = new StringBuilder();
                List<MathToken> covered = new List<MathToken>();

                for (int index = from; index < cut; index++)
                {
                    text.Append(Drawn(tokens[index]));
                    covered.Add(tokens[index]);
                }

                // the place in front of a group is the middle of the gap before it
                bool afterGap = groups.Count > 0;
                string run = text.ToString();
                TextRunBox group = Addressed(new TextRunBox(run, fontSize, _measurer.Measure(run, fontSize), covered),
                    path, from, fontSize, afterGap ? -gap / 2 : 0);

                if (afterGap) group.LeadingGap = gap;

                groups.Add(group);
                from = cut;
            }

            return groups;
        }

        // where a group of three begins in each whole part of the run, counted from its last digit
        // (every stretch of digits on its own, since a pair writes two numbers into one run)
        private static List<int> GroupCuts(IReadOnlyList<MathToken> tokens, int start, int end)
        {
            List<int> cuts = new List<int>();
            int index = start;

            while (index < end)
            {
                if (!IsDigit(tokens[index]))
                {
                    index++;
                    continue;
                }

                int first = index;
                while (index < end && IsDigit(tokens[index])) index++;

                if (first > start && tokens[first - 1].Value == ".") continue;

                // the first group takes what is left over, one to three digits, and every cut after it
                // is three further on
                int length = index - first;
                for (int cut = first + (length - 1) % 3 + 1; cut < index; cut += 3) cuts.Add(cut);
            }

            return cuts;
        }

        private static bool IsDigit(MathToken token)
        {
            return token.Value.Length == 1 && char.IsAsciiDigit(token.Value[0]);
        }

        // what a character of a number run is drawn as: the decimal point as the mark the settings ask
        // for, and the comma between the two values of a pair as the separator that goes with it
        private string Drawn(MathToken token)
        {
            if (token.Value == ".") return _style.DecimalMark;
            if (token.Value == ",") return _style.ListSeparator;

            return token.Value;
        }

        // how far into a run the caret stands between two digits; the run stays whole, so no digit shifts
        private static double OffsetInRun(TextRunBox run, int tokenOffset)
        {
            if (tokenOffset <= 0) return run.TokenOffsets is { Count: > 0 } offsets ? offsets[0] : 0;
            if (run.TokenOffsets == null || tokenOffset >= run.TokenOffsets.Count) return run.Width;

            return run.TokenOffsets[tokenOffset];
        }

        // one cursor position per digit of the run, each measured, since a point is narrower than a digit
        // (the first at the left edge, or back in the gap before a digit group)
        private TextRunBox Addressed(TextRunBox run, string path, int firstTokenIndex, double fontSize,
            double firstOffset = 0)
        {
            string[] addresses = new string[run.Tokens.Count];
            double[] offsets = new double[run.Tokens.Count];
            StringBuilder prefix = new StringBuilder();

            for (int offset = 0; offset < addresses.Length; offset++)
            {
                addresses[offset] = Address(path, firstTokenIndex + offset);
                offsets[offset] = offset == 0 ? firstOffset : _measurer.Measure(prefix.ToString(), fontSize).Width;

                prefix.Append(Drawn(run.Tokens[offset]));
            }

            run.CursorAddress = addresses.Length > 0 ? addresses[0] : null;
            run.TokenAddresses = addresses;
            run.TokenOffsets = offsets;

            return run;
        }

        // the caret list by identity; two empty slots differ only by reference
        private bool CaretIsIn(IReadOnlyList<MathToken> tokens)
        {
            return _caret.Tokens != null && ReferenceEquals(tokens, _caret.Tokens);
        }

        private TextRunBox BuildOperator(MathToken token, double fontSize)
        {
            if (IsLetterOperator(token.Value))
            {
                TextRunBox letter = TextRun(token.Value, fontSize, token);

                double letterGap = GapAt(fontSize, _style.FontSizePx, _style.OperatorGapScaling) * _style.OperatorGap;
                letter.LeadingGap = letterGap;
                letter.TrailingGap = letterGap;

                return letter;
            }

            double operatorSize = fontSize * _style.OperatorScale;
            TextRunBox box = TextRun(OperatorSymbol(token.Value), operatorSize, token);

            // the raise in em of the operator, the axis in em of the text, the lift of the fraction bar
            box.Raise = operatorSize * _style.OperatorRaise + fontSize * _style.MathAxisRaise;

            // the gap follows the size only as far as OperatorGapScaling says
            double gapSize = GapAt(operatorSize, _style.FontSizePx * _style.OperatorScale,
                _style.OperatorGapScaling);

            box.LeadingGap = gapSize * _style.OperatorGap;
            box.TrailingGap = box.LeadingGap;

            return box;
        }

        // ÷R the way the Casio key prints it: the sign on the axis, the R on the baseline, the gaps around both
        private RowBox BuildRemainderDivision(MathToken token, double fontSize)
        {
            TextRunBox sign = BuildOperator(token, fontSize);
            double leading = sign.LeadingGap;
            double trailing = sign.TrailingGap;
            sign.LeadingGap = 0;
            sign.TrailingGap = 0;

            TextRunBox letter = TextRun("R", fontSize, token);

            return new RowBox(new List<MathBox> { sign, letter }) { LeadingGap = leading, TrailingGap = trailing };
        }

        private TextRunBox BuildAtom(MathToken token, double fontSize)
        {
            return TextRun(AtomText(token), fontSize, token);
        }

        // the period of a recurring decimal under a bar; the digits in front are an ordinary run
        private OverlineBox BuildRecurring(RecurringToken token, double fontSize)
        {
            return new OverlineBox(TextRun(token.Value, fontSize, token),
                fontSize * _style.RecurringBarThickness, fontSize * _style.RecurringBarGap);
        }


        // === structures ===

        private FractionBox BuildFraction(FractionToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            return BuildFractionBox(token.NumeratorTokens, token.DenominatorTokens, 0,
                fontSize, scriptLevel, path, tokenIndex);
        }

        // the whole part in front of an ordinary fraction, nothing drawn ahead of it (see
        // MathInputManager.BeginsWithItsFirstSlot); the gap keeps the two caret places apart
        private RowBox BuildMixedFraction(MixedFractionToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.FractionScale;

            MathBox whole = BuildSlot(token.WholeTokens, size, scriptLevel, SlotPath(path, tokenIndex, 0));
            whole.TrailingGap = size * _style.MixedFractionGap;

            FractionBox fraction = BuildFractionBox(token.NumeratorTokens, token.DenominatorTokens, 1,
                fontSize, scriptLevel, path, tokenIndex);

            return new RowBox(new List<MathBox> { whole, fraction });
        }

        // firstSlot is the slot index of the numerator, which the whole part of a mixed fraction moves on
        private FractionBox BuildFractionBox(IReadOnlyList<MathToken> numerator, IReadOnlyList<MathToken> denominator,
            int firstSlot, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.FractionScale;

            // both halves drop a level, unless a display fraction keeps them at full size
            bool display = _style.UseDisplayFractions;
            double innerSize = display ? size : ScriptSize(size, scriptLevel);
            int innerLevel = display ? scriptLevel : scriptLevel + 1;

            return new FractionBox(
                BuildSlot(numerator, innerSize, innerLevel, SlotPath(path, tokenIndex, firstSlot)),
                BuildSlot(denominator, innerSize, innerLevel, SlotPath(path, tokenIndex, firstSlot + 1)),
                size * _style.FractionBarThickness,
                size * (_style.MathAxisHeight + _style.MathAxisRaise),
                size * _style.FractionNumeratorGap,
                size * _style.FractionDenominatorGap,
                size * _style.FractionSidePadding);
        }

        // the base is a slot of its own, so nothing can bind to the wrong thing
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

            // an empty index stays invisible until the caret walks into it
            MathBox index = token.IndexTokens.Count == 0 && !CaretIsIn(token.IndexTokens)
                ? null
                : BuildSlot(token.IndexTokens, size * _style.ScriptScriptScale, scriptLevel + 2, SlotPath(path, tokenIndex, 0));

            // the air around the radicand follows the size only as far as RadicalPadScaling says
            double padSize = GapAt(size, _style.FontSizePx * _style.RootScale, _style.RadicalPadScaling);

            MathBox radicand = BuildSlot(token.RadicandTokens, size, scriptLevel, SlotPath(path, tokenIndex, 1));

            // the tip is measured from the baseline; only a radicand deeper than a digit takes it lower
            double signDescent = size * _style.RadicalBottomDrop
                + Math.Max(0, radicand.Descent - _measurer.Measure(StrutText, size).Descent);

            return new RootBox(
                index,
                radicand,
                size * _style.RadicalHookWidth,
                size * _style.RadicalRuleThickness,
                size * _style.RadicalHookThickness,
                size * _style.RadicalVerticalGap,
                signDescent,
                _style.RadicalIndexRaise,
                padSize * _style.RadicalLeadingPad,
                padSize * _style.RadicalTrailingPad);
        }

        private RowBox BuildLogarithm(LogarithmToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.LogarithmScale;
            List<MathBox> parts = new List<MathBox> { TextRun("log", size, token) };

            // an empty base stays invisible, like an empty root index
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
            MathBox parameter = BuildArguments(token, size, scriptLevel, path, tokenIndex);
            List<MathBox> parts = new List<MathBox>();

            switch (token.Shape)
            {
                case FunctionShape.Bars:
                    AddDelimited(parts, parameter, DelimiterKind.Bar, DelimiterKind.Bar, size);
                    return new RowBox(parts);

                case FunctionShape.Floor:
                    AddDelimited(parts, parameter, DelimiterKind.FloorOpen, DelimiterKind.FloorClose, size);
                    return new RowBox(parts);

                case FunctionShape.Ceiling:
                    AddDelimited(parts, parameter, DelimiterKind.CeilingOpen, DelimiterKind.CeilingClose, size);
                    return new RowBox(parts);
            }

            TextRunBox nameRun = TextRun(token.DisplayName, size, token);
            parts.Add(nameRun);

            if (token.IsInverse)
            {
                TextRunBox raised = TextRun(MinusOne, ScriptSize(size, scriptLevel), token);
                raised.Raise = nameRun.Ascent * _style.SuperscriptShift;
                parts.Add(raised);
            }

            AddDelimited(parts, parameter,
                DelimiterKind.ParenthesisOpen, DelimiterKind.ParenthesisClose, size);

            return new RowBox(parts);
        }

        // what stands between the brackets: the one slot, or the slots with the drawn separator between them
        // (which also keeps the end of one argument and the start of the next apart)
        private MathBox BuildArguments(FunctionToken token, double size, int scriptLevel, string path, int tokenIndex)
        {
            if (token.Arguments.Count == 1)
            {
                return BuildSlot(token.ParameterTokens, size, scriptLevel, SlotPath(path, tokenIndex, 0));
            }

            List<MathBox> arguments = new List<MathBox>();
            for (int index = 0; index < token.Arguments.Count; index++)
            {
                if (index > 0)
                {
                    TextRunBox separator = TextRun(_style.ListSeparator, size, token);
                    separator.TrailingGap = size * _style.ArgumentSeparatorGap;
                    arguments.Add(separator);
                }

                arguments.Add(BuildSlot(token.Arguments[index], size, scriptLevel, SlotPath(path, tokenIndex, index)));
            }

            return new RowBox(arguments);
        }

        // Σ and Π: bounds a level smaller above and below the sign, x= in front of the lower as on a Casio,
        // the body in brackets on the right; (the Σ and Π section of MathLayoutStyle)
        private RowBox BuildSeries(LargeOperatorToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double boundSize = ScriptSize(fontSize, scriptLevel);
            TextRunBox sign = BuildLargeSign(token, fontSize, _style.SumSignScale, _style.SumSignRaise);

            MathBox upper = BuildSlot(token.UpperTokens, boundSize, scriptLevel + 1, SlotPath(path, tokenIndex, 1));
            RowBox lower = new RowBox(new List<MathBox>
            {
                TextRun(VariableEquals, boundSize, token),
                BuildSlot(token.LowerTokens, boundSize, scriptLevel + 1, SlotPath(path, tokenIndex, 0))
            });

            PlaceBounds(upper, lower, sign, boundSize,
                fontSize * _style.SumUpperBoundRaise, fontSize * _style.SumLowerBoundDrop);

            double width = Math.Max(sign.Width, Math.Max(upper.Width, lower.Width));
            upper.LeadingGap = (width - upper.Width) / 2;
            sign.LeadingGap = (width - sign.Width) / 2;
            lower.LeadingGap = (width - lower.Width) / 2;

            List<MathBox> parts = new List<MathBox>
            {
                new StackBox(new List<MathBox> { upper, sign, lower }) { TrailingGap = fontSize * _style.SumGap }
            };

            AddDelimited(parts, BuildSlot(token.BodyTokens, fontSize, scriptLevel, SlotPath(path, tokenIndex, 2)),
                DelimiterKind.ParenthesisOpen, DelimiterKind.ParenthesisClose, fontSize);

            return new RowBox(parts);
        }

        // the integral: the sign with its bounds at the top and bottom right, the integrand, and dx, which
        // ends it without brackets; (the integral section of MathLayoutStyle)
        private RowBox BuildIntegral(LargeOperatorToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double boundSize = ScriptSize(fontSize, scriptLevel);
            TextRunBox sign = BuildLargeSign(token, fontSize, _style.IntegralSignScale, _style.IntegralSignRaise);

            MathBox lower = BuildSlot(token.LowerTokens, boundSize, scriptLevel + 1, SlotPath(path, tokenIndex, 0));
            MathBox upper = BuildSlot(token.UpperTokens, boundSize, scriptLevel + 1, SlotPath(path, tokenIndex, 1));

            PlaceBounds(upper, lower, sign, boundSize,
                fontSize * _style.IntegralUpperBoundRaise, fontSize * _style.IntegralLowerBoundDrop);

            TextRunBox differential = TextRun(Differential, fontSize, token);
            differential.LeadingGap = fontSize * _style.IntegralDxGap;

            return new RowBox(new List<MathBox>
            {
                sign,
                new StackBox(new List<MathBox> { upper, lower }) { TrailingGap = fontSize * _style.IntegralGap },
                BuildSlot(token.BodyTokens, fontSize, scriptLevel, SlotPath(path, tokenIndex, 2)),
                differential
            });
        }

        // Σ, Π or ∫ as a letter of the font, scale times the size of the text and moved up by raise em of
        // the text
        private TextRunBox BuildLargeSign(LargeOperatorToken token, double fontSize, double scale, double raise)
        {
            TextRunBox sign = TextRun(LargeSign(token.Kind), fontSize * scale, token);
            sign.Raise = fontSize * raise;

            return sign;
        }

        // puts the bounds upperRaise above and lowerDrop below the baseline of the sign, baseline to baseline
        // (a bound taller than a digit moves further out by the extra height)
        private void PlaceBounds(MathBox upper, MathBox lower, TextRunBox sign, double boundSize,
            double upperRaise, double lowerDrop)
        {
            TextMetrics digits = _measurer.Measure(StrutText, boundSize);

            upper.Raise = sign.Raise + upperRaise + Math.Max(0, upper.Descent - digits.Descent);
            lower.Raise = sign.Raise - lowerDrop - Math.Max(0, lower.Ascent - digits.Ascent);
        }

        // d/dx as a fraction, the function in brackets, a bar, and the point at its bottom right, as a
        // Casio writes it; (the derivative section of MathLayoutStyle)
        private RowBox BuildDerivative(DerivativeToken token, double fontSize, int scriptLevel, string path, int tokenIndex)
        {
            double size = fontSize * _style.FractionScale;
            double innerSize = _style.UseDisplayFractions ? size : ScriptSize(size, scriptLevel);

            List<MathBox> parts = new List<MathBox>
            {
                new FractionBox(
                    TextRun("d", innerSize, token),
                    TextRun(Differential, innerSize, token),
                    size * _style.FractionBarThickness,
                    size * (_style.MathAxisHeight + _style.MathAxisRaise),
                    size * _style.FractionNumeratorGap,
                    size * _style.FractionDenominatorGap,
                    size * _style.FractionSidePadding)
            };

            MathBox function = BuildSlot(token.FunctionTokens, fontSize, scriptLevel, SlotPath(path, tokenIndex, 0));
            AddDelimited(parts, function, DelimiterKind.ParenthesisOpen, DelimiterKind.ParenthesisClose, fontSize);

            // the bar is exactly as tall as the brackets in front of it
            (double ascent, double descent) = DelimiterReach(function.Ascent, function.Descent, fontSize);
            parts.Add(new DelimiterBox(DelimiterKind.Bar, fontSize * _style.DelimiterWidth, ascent, descent,
                fontSize * _style.DelimiterThickness));

            double pointSize = ScriptSize(fontSize, scriptLevel);
            RowBox point = new RowBox(new List<MathBox>
            {
                TextRun(VariableEquals, pointSize, token),
                BuildSlot(token.PointTokens, pointSize, scriptLevel + 1, SlotPath(path, tokenIndex, 1))
            });

            point.Raise = -fontSize * _style.DerivativePointDrop;
            point.TrailingGap = fontSize * _style.DerivativePointPad;
            parts.Add(point);

            return new RowBox(parts);
        }

        // the factorial and the percent stand behind their operand as plain signs; the reciprocal is a
        // raised minus one, the way a Casio prints it
        private MathBox BuildPostfix(PostfixToken token, double fontSize, int scriptLevel)
        {
            if (token.Value != "inv") return BuildAtom(token, fontSize);

            TextRunBox raised = TextRun(MinusOne, ScriptSize(fontSize, scriptLevel), token);

            // no base box to measure, so the lift comes off a digit
            raised.Raise = _measurer.Measure(StrutText, fontSize).Ascent * _style.SuperscriptShift;

            return raised;
        }


        // === helpers ===

        // an empty slot still occupies space, as a placeholder
        private MathBox BuildSlot(IReadOnlyList<MathToken> tokens, double fontSize, int scriptLevel,
            string path)
        {
            if (tokens.Count > 0) return BuildRow(tokens, fontSize, scriptLevel, path);

            // the same box with or without the caret, so the slot never changes size
            PlaceholderBox placeholder = new PlaceholderBox(
                fontSize * _style.PlaceholderSize,
                fontSize * _style.PlaceholderThickness,
                fontSize * _style.PlaceholderRaise,
                _measurer.Measure(StrutText, fontSize));

            placeholder.CursorAddress = Address(path, 0);
            placeholder.FontSize = fontSize;

            if (CaretIsIn(tokens)) Caret = new CaretPlacement(placeholder, 0, placeholder, fontSize);

            return placeholder;
        }

        // a delimiter takes its height from what it encloses, so it is a box of its own
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

        // how far a delimiter reaches around content of this size, for a function and a typed bracket alike
        // (at least the strut, so an empty pair is as tall as one around a digit)
        private (double Ascent, double Descent) DelimiterReach(double ascent, double descent, double fontSize)
        {
            TextMetrics strut = _measurer.Measure(StrutText, fontSize);
            double padding = fontSize * _style.DelimiterPadding;

            return (Math.Max(ascent, strut.Ascent) * _style.DelimiterHeightScale + padding,
                Math.Max(descent, strut.Descent) * _style.DelimiterHeightScale + padding);
        }

        // a typed bracket is a delimiter, so it grows with its content; built at the floor, stretched later
        private DelimiterBox BuildTypedBracket(MathToken token, double fontSize)
        {
            DelimiterKind kind = token.Type == TokenType.BracketOpen
                ? DelimiterKind.ParenthesisOpen
                : DelimiterKind.ParenthesisClose;

            (double ascent, double descent) = DelimiterReach(0, 0, fontSize);

            DelimiterBox bracket = new DelimiterBox(kind, fontSize * _style.DelimiterWidth,
                ascent, descent, fontSize * _style.DelimiterThickness);

            // the air on the content side
            double air = fontSize * _style.DelimiterSidePadding;
            if (kind == DelimiterKind.ParenthesisOpen) bracket.TrailingGap = air;
            else bracket.LeadingGap = air;

            return bracket;
        }

        // every typed bracket takes its height from what stands between it and its partner, before the
        // RowBox is built; the row stays flat, so every cursor position inside stays on its line
        // (a lone bracket reaches to the end of the row, or back to its start)
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

        // one cursor position, and the address of one slot of one token, built the way LatexRenderContext
        // builds them
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

        // how wide a gap is once its piece is set smaller: scaling 1 fully proportional, 0 the full size gap
        private static double GapAt(double size, double fullSize, double scaling)
        {
            return fullSize + (size - fullSize) * scaling;
        }

        // the size one step further in; (both ratios are of the base size, so deep nesting stops shrinking)
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
            return token switch
            {
                PostfixToken postfix => postfix.Symbol, // a prefix is written as its symbol, k rather than kilo
                _ when token.Type == TokenType.Constant => token.Value == "pi" ? "π" : token.Value,
                _ when token.Type == TokenType.Answer => "Ans",
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
                "÷R" => "÷", // the R is drawn beside it, see BuildRemainderDivision
                "-" => "−", // real minus, which is wider and sits higher than a hyphen
                _ => value
            };
        }

        // the Greek capitals a Casio prints for the sum and the product, and the integral sign
        private static string LargeSign(LargeOperatorKind kind)
        {
            return kind switch
            {
                LargeOperatorKind.Sum => "Σ",
                LargeOperatorKind.Product => "Π",
                _ => "∫"
            };
        }

        // nPr and nCr, a letter on the baseline rather than on the axis
        private static bool IsLetterOperator(string value)
        {
            return value == "P" || value == "C";
        }
    }
}
