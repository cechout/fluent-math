using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the caret
    //
    // the whole of it reduces to one rule: it may not move anything. Overlapping the digit beside it is
    // fine, shifting it by a pixel is not, and that is what most of this file asserts, by laying the same
    // formula out twice and comparing every box in the tree.
    //
    // the rule was got wrong three times while the display was KaTeX, and once more after that in the
    // native renderer, where the caret split a number into two runs: the layout widths added up exactly
    // and the drawn glyphs still moved, because two TextBlocks each bring the side bearings of their own
    // first glyph. Comparing widths is not enough, which is why this compares boxes.
    public class MathLayoutCaretTests
    {
        private sealed class FakeMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                return new TextMetrics(text.Length * fontSizePx, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        private static MathLayoutStyle Style() => new MathLayoutStyle { FontSizePx = FontSize };

        private static (RowBox Row, CaretPlacement? Caret) Lay(IReadOnlyList<MathToken> tokens,
            CaretTarget caret, MathLayoutStyle style = null)
        {
            MathLayoutEngine engine = new MathLayoutEngine(new FakeMeasurer(), style ?? Style(), caret);
            RowBox row = engine.BuildRow(tokens);
            row.Place(0, row.Ascent);

            return (row, engine.Caret);
        }

        private static (RowBox Row, CaretPlacement? Caret) WithCaret(IReadOnlyList<MathToken> tokens,
            int index, MathLayoutStyle style = null)
        {
            return Lay(tokens, new CaretTarget(tokens, index), style);
        }

        private static RowBox WithoutCaret(IReadOnlyList<MathToken> tokens) => Lay(tokens, default).Row;

        private static MathToken Digit(string value) => new MathToken(TokenType.Number, value);

        private static List<MathToken> Number(string digits)
        {
            return digits.Select(character => Digit(character.ToString())).ToList();
        }

        // every box of both trees, in the same order, so the two can be compared position by position
        private static List<MathBox> Flatten(MathBox box)
        {
            List<MathBox> all = new List<MathBox> { box };

            switch (box)
            {
                case RowBox row:
                    foreach (MathBox child in row.Children) all.AddRange(Flatten(child));
                    break;

                case FractionBox fraction:
                    all.AddRange(Flatten(fraction.Numerator));
                    all.AddRange(Flatten(fraction.Denominator));
                    break;

                case RootBox root:
                    if (root.Index != null) all.AddRange(Flatten(root.Index));
                    all.AddRange(Flatten(root.Radicand));
                    break;
            }

            return all;
        }

        private static void AssertLayoutIsUntouched(IReadOnlyList<MathToken> tokens, CaretTarget caret)
        {
            List<MathBox> plain = Flatten(WithoutCaret(tokens));
            List<MathBox> carried = Flatten(Lay(tokens, caret).Row);

            Assert.Equal(plain.Count, carried.Count);

            for (int i = 0; i < plain.Count; i++)
            {
                Assert.Equal(plain[i].GetType(), carried[i].GetType());
                Assert.Equal(plain[i].X, carried[i].X);
                Assert.Equal(plain[i].Width, carried[i].Width);
                Assert.Equal(plain[i].Ascent, carried[i].Ascent);
                Assert.Equal(plain[i].Descent, carried[i].Descent);
                Assert.Equal(plain[i].Baseline, carried[i].Baseline);
            }
        }


        // === the rule ===

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ACaretAnywhereInANumberLeavesEveryBoxExactlyWhereItWas(int position)
        {
            List<MathToken> tokens = Number("123");

            AssertLayoutIsUntouched(tokens, new CaretTarget(tokens, position));
        }

        [Fact]
        public void ACaretInAFractionLeavesEveryBoxExactlyWhereItWas()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            List<MathToken> tokens = new List<MathToken> { fraction };

            AssertLayoutIsUntouched(tokens, new CaretTarget(fraction.NumeratorTokens, 0));
            AssertLayoutIsUntouched(tokens, new CaretTarget(fraction.DenominatorTokens, 1));
        }

        [Fact]
        public void ACaretInAnEmptySlotLeavesEveryBoxExactlyWhereItWas()
        {
            // the slot that used to grow the moment the caret walked into it, because the caret was
            // wrapped into a row beside the placeholder and brought the metrics of a digit with it
            FractionToken fraction = new FractionToken();
            List<MathToken> tokens = new List<MathToken> { fraction };

            AssertLayoutIsUntouched(tokens, new CaretTarget(fraction.NumeratorTokens, 0));
            AssertLayoutIsUntouched(tokens, new CaretTarget(fraction.DenominatorTokens, 0));
        }

        [Fact]
        public void ACaretInAPowerOrAFunctionLeavesEveryBoxExactlyWhereItWas()
        {
            PowerToken power = new PowerToken();
            power.BaseTokens.Add(Digit("9"));
            power.ExponentTokens.Add(Digit("8"));

            FunctionToken function = new FunctionToken("sin");
            function.ParameterTokens.Add(Digit("0"));

            List<MathToken> tokens = new List<MathToken> { power, function };

            AssertLayoutIsUntouched(tokens, new CaretTarget(power.ExponentTokens, 0));
            AssertLayoutIsUntouched(tokens, new CaretTarget(function.ParameterTokens, 1));
            AssertLayoutIsUntouched(tokens, new CaretTarget(tokens, 1));
        }

        [Fact]
        public void ACaretIsNeverABoxInTheTree()
        {
            List<MathToken> tokens = Number("12");

            Assert.Equal(WithoutCaret(tokens).Children.Count, WithCaret(tokens, 1).Row.Children.Count);
        }

        [Fact]
        public void ANumberIsNeverSplitByTheCaretStandingInsideIt()
        {
            RowBox row = WithCaret(Number("123"), 1).Row;

            TextRunBox run = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal("123", run.Text);
        }


        // === where it lands ===

        [Fact]
        public void ACaretAtTheStartHangsOffTheFirstBox()
        {
            List<MathToken> tokens = Number("12");
            (RowBox row, CaretPlacement? caret) = WithCaret(tokens, 0);

            Assert.NotNull(caret);
            Assert.Same(row.Children[0], caret.Value.Box);
            Assert.Equal(0, caret.Value.Offset);
        }

        [Fact]
        public void ACaretAtTheEndHangsOffTheRowAtItsFullWidth()
        {
            List<MathToken> tokens = Number("12");
            (RowBox row, CaretPlacement? caret) = WithCaret(tokens, 2);

            Assert.Same(row, caret.Value.Box);
            Assert.Equal(row.Width, caret.Value.Offset);
        }

        [Fact]
        public void ACaretInsideANumberHangsOffTheRunAtTheWidthOfWhatPrecedesIt()
        {
            (RowBox row, CaretPlacement? caret) = WithCaret(Number("123"), 2);

            Assert.Same(row.Children[0], caret.Value.Box);
            Assert.Equal(FontSize * 2, caret.Value.Offset); // two digits of the fake measurer
        }

        [Fact]
        public void ACaretInAnEmptySlotHangsOffThePlaceholder()
        {
            FractionToken fraction = new FractionToken();
            fraction.DenominatorTokens.Add(Digit("2"));

            List<MathToken> tokens = new List<MathToken> { fraction };
            (RowBox row, CaretPlacement? caret) = Lay(tokens, new CaretTarget(fraction.NumeratorTokens, 0));

            FractionBox box = (FractionBox)row.Children.Single();
            Assert.Same(box.Numerator, caret.Value.Box);
            Assert.IsType<PlaceholderBox>(caret.Value.Box);
        }

        [Fact]
        public void TwoEmptySlotsAreToldApartByIdentityRatherThanByContents()
        {
            FractionToken fraction = new FractionToken();
            List<MathToken> tokens = new List<MathToken> { fraction };

            (RowBox row, CaretPlacement? caret) = Lay(tokens, new CaretTarget(fraction.DenominatorTokens, 0));
            FractionBox box = (FractionBox)row.Children.Single();

            Assert.Same(box.Denominator, caret.Value.Box);
            Assert.NotSame(box.Numerator, caret.Value.Box);
        }

        // === how high it stands ===

        // the caret hangs off the box it stands in front of, and an operator is not where its row is: it
        // rides above the baseline so it reads level with the digits, and a caret that took its baseline
        // from it would stand higher in front of a plus than in front of a digit
        [Fact]
        public void ACaretInFrontOfAnOperatorStandsOnTheBaselineOfItsRowRatherThanOnTheOperators()
        {
            List<MathToken> tokens = new List<MathToken>
            {
                Digit("1"), new MathToken(TokenType.Operator, "+"), Digit("1")
            };

            // the raise is pinned here rather than read off the app, because this is about where the
            // caret takes its baseline from and not about how high an operator happens to be tuned; at
            // a raise of zero the row and the operator stand in the same place and the test asserts
            // nothing at all
            MathLayoutStyle raised = Style();
            raised.OperatorRaise = 0.2;

            (RowBox row, CaretPlacement? beforeOperator) = WithCaret(tokens, 1, raised);
            CaretPlacement? beforeDigit = WithCaret(tokens, 2, raised).Caret;

            // the two hang off different boxes, and one of those sits higher than the other
            Assert.Same(row.Children[1], beforeOperator.Value.Box);
            Assert.NotEqual(row.Baseline, beforeOperator.Value.Box.Baseline);

            // and both stand at the same height all the same
            Assert.Same(row, beforeOperator.Value.Line);
            Assert.Equal(row.Baseline, beforeOperator.Value.Line.Baseline);
            Assert.Equal(beforeOperator.Value.Line.Baseline, beforeDigit.Value.Line.Baseline);
        }

        [Fact]
        public void ACaretInARaisedSlotRidesUpWithIt()
        {
            PowerToken power = new PowerToken();
            power.BaseTokens.Add(Digit("2"));
            power.ExponentTokens.Add(Digit("3"));

            List<MathToken> tokens = new List<MathToken> { power };
            (RowBox row, CaretPlacement? caret) = Lay(tokens, new CaretTarget(power.ExponentTokens, 1));

            // an exponent is a row of its own with a raise on it, so the line the caret reports is that
            // row and the caret is drawn as high as the digit beside it
            Assert.True(caret.Value.Line.Baseline < row.Baseline);
        }

        [Fact]
        public void ACaretInADeeperSlotIsDrawnAtTheSizeOfThatSlot()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));

            List<MathToken> tokens = new List<MathToken> { fraction };
            CaretPlacement? caret = Lay(tokens, new CaretTarget(fraction.NumeratorTokens, 0)).Caret;

            Assert.Equal(FontSize * Style().ScriptScale, caret.Value.FontSize);
        }

        [Fact]
        public void NoCaretIsReportedWhenNoneWasAskedFor()
        {
            Assert.Null(Lay(Number("12"), default).Caret);
        }

        [Fact]
        public void AnEmptyFormulaStillPlacesItsCaret()
        {
            List<MathToken> empty = new List<MathToken>();
            (RowBox row, CaretPlacement? caret) = WithCaret(empty, 0);

            Assert.Same(row, caret.Value.Box);
            Assert.Equal(0, caret.Value.Offset);
            Assert.Equal(FontSize * 0.75, row.Ascent); // still as tall as a digit
        }


        // === slots that only exist while the caret is in them ===

        [Fact]
        public void AnEmptyRootIndexAppearsOnlyWhileTheCaretStandsInIt()
        {
            RootToken root = new RootToken();
            root.RadicandTokens.Add(Digit("9"));

            List<MathToken> tokens = new List<MathToken> { root };

            RootBox without = (RootBox)WithoutCaret(tokens).Children.Single();
            Assert.Null(without.Index);

            (RowBox row, CaretPlacement? caret) = Lay(tokens, new CaretTarget(root.IndexTokens, 0));
            RootBox with = (RootBox)row.Children.Single();

            Assert.NotNull(with.Index);
            Assert.Same(with.Index, caret.Value.Box);
        }

        [Fact]
        public void AnEmptyLogarithmBaseAppearsOnlyWhileTheCaretStandsInIt()
        {
            LogarithmToken logarithm = new LogarithmToken();
            logarithm.ParameterTokens.Add(Digit("8"));

            List<MathToken> tokens = new List<MathToken> { logarithm };

            // name, open bracket, parameter, close bracket
            Assert.Equal(4, ((RowBox)WithoutCaret(tokens).Children.Single()).Children.Count);

            (RowBox row, CaretPlacement? caret) = Lay(tokens, new CaretTarget(logarithm.BaseTokens, 0));
            RowBox box = (RowBox)row.Children.Single();

            Assert.Equal(5, box.Children.Count);
            Assert.Same(box.Children[1], caret.Value.Box);
        }
    }
}
