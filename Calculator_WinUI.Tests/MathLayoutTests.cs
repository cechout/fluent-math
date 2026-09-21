using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // layout is arithmetic, so it is checked with numbers chosen here rather than with a screenshot
    //
    // the measurer below is what makes that possible: no font is involved anywhere in this file, and the
    // assertions are about how the engine combines what a measurer tells it, never about what a real face
    // happens to report
    public class MathLayoutTests
    {
        // === the fake ===

        // one unit of width per character per pixel of font size, and a box three quarters above the
        // baseline and one quarter below, so every expected number below is a short multiplication
        private sealed class FakeMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                return new TextMetrics(text.Length * fontSizePx, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        private static MathLayoutEngine Engine(MathLayoutStyle style = null)
        {
            return new MathLayoutEngine(new FakeMeasurer(), style ?? new MathLayoutStyle { FontSizePx = FontSize });
        }

        private static MathToken Digit(string value) => new MathToken(TokenType.Number, value);
        private static MathToken Operator(string value) => new MathToken(TokenType.Operator, value);


        // === rows ===

        [Fact]
        public void AnEmptyRowKeepsTheHeightOfADigitSoTheDisplayDoesNotCollapse()
        {
            RowBox row = Engine().BuildRow(new List<MathToken>());

            Assert.Empty(row.Children);
            Assert.Equal(0, row.Width);
            Assert.Equal(FontSize * 0.75, row.Ascent);
            Assert.Equal(FontSize * 0.25, row.Descent);
        }

        [Fact]
        public void ASingleDigitIsOneRunAsWideAsTheMeasurerSays()
        {
            RowBox row = Engine().BuildRow(new List<MathToken> { Digit("7") });

            TextRunBox run = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal("7", run.Text);
            Assert.Equal(FontSize, run.Width);
        }

        [Fact]
        public void ConsecutiveDigitsAreCoalescedIntoOneRunRatherThanMeasuredApart()
        {
            RowBox row = Engine().BuildRow(
                new List<MathToken> { Digit("1"), Digit("2"), Digit("3") });

            TextRunBox run = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal("123", run.Text);
            Assert.Equal(3, run.Tokens.Count);
            Assert.Equal(FontSize * 3, run.Width);
        }

        [Fact]
        public void ARunRemembersEveryTokenItCoversSoACaretCanLandInsideItLater()
        {
            List<MathToken> tokens = new List<MathToken> { Digit("4"), Digit("."), Digit("5") };

            RowBox row = Engine().BuildRow(tokens);

            TextRunBox run = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal("4.5", run.Text);
            Assert.Equal(tokens, run.Tokens);
        }

        [Fact]
        public void AnOperatorBreaksARunInTwo()
        {
            RowBox row = Engine().BuildRow(
                new List<MathToken> { Digit("1"), Digit("2"), Operator("+"), Digit("3") });

            Assert.Equal(3, row.Children.Count);
            Assert.Equal("12", ((TextRunBox)row.Children[0]).Text);
            Assert.Equal("+", ((TextRunBox)row.Children[1]).Text);
            Assert.Equal("3", ((TextRunBox)row.Children[2]).Text);
        }

        [Fact]
        public void ARowIsAsTallAsItsTallestChildInEachDirection()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, OperatorScale = 0.5 };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Digit("1"), Operator("+") });

            // the digit reaches higher than the half size operator even after the raise
            Assert.Equal(FontSize * 0.75, row.Ascent);
        }


        // === operators ===

        [Fact]
        public void AnOperatorIsSetAtItsOwnScaleRatherThanAtTheSizeOfTheRow()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, OperatorScale = 0.6 };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            TextRunBox op = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal(FontSize * 0.6, op.FontSize);
        }

        [Fact]
        public void AnOperatorAsksForTheSameGapOnBothSidesBecauseTheBoxAfterItMayNotExist()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorGap = 0.2
            };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            MathBox op = Assert.Single(row.Children);
            double expected = FontSize * 0.5 * 0.2; // em of the operator, not of the text around it
            Assert.Equal(expected, op.LeadingGap);
            Assert.Equal(expected, op.TrailingGap);
        }

        [Fact]
        public void TheGapsOfAnOperatorCountTowardsTheWidthOfTheRow()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorGap = 0.2
            };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            double operatorSize = FontSize * 0.5;
            double gap = operatorSize * 0.2;
            Assert.Equal(operatorSize + gap + gap, row.Width);
        }

        [Fact]
        public void AnOperatorRidesAboveTheBaselineByItsOwnRaise()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorRaise = 0.25
            };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            MathBox op = Assert.Single(row.Children);
            Assert.Equal(FontSize * 0.5 * 0.25, op.Raise);
        }

        [Fact]
        public void ARaisedOperatorReachesHigherAndHangsLessLow()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 1.0,  // same size as the text, so only the raise moves it
                OperatorGap = 0,
                OperatorRaise = 0.25
            };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            double raise = FontSize * 0.25;
            Assert.Equal(FontSize * 0.75 + raise, row.Ascent);
            Assert.Equal(FontSize * 0.25 - raise, row.Descent);
        }


        // === symbols ===

        [Theory]
        [InlineData("*", "×")]
        [InlineData("/", "÷")]
        [InlineData("-", "−")]
        [InlineData("+", "+")]
        public void AnOperatorIsDrawnAsItsMathSignRatherThanAsTheKeyItWasTypedWith(string typed, string drawn)
        {
            RowBox row = Engine().BuildRow(new List<MathToken> { Operator(typed) });

            Assert.Equal(drawn, ((TextRunBox)Assert.Single(row.Children)).Text);
        }

        [Fact]
        public void PiIsDrawnAsItsLetterAndAnsAsItsWord()
        {
            RowBox row = Engine().BuildRow(
                new List<MathToken> { new ConstantToken("pi"), new AnsToken() });

            Assert.Equal("π", ((TextRunBox)row.Children[0]).Text);
            Assert.Equal("Ans", ((TextRunBox)row.Children[1]).Text);
        }

        [Fact]
        public void EulersNumberKeepsItsOwnLetter()
        {
            RowBox row = Engine().BuildRow(new List<MathToken> { new ConstantToken("e") });

            Assert.Equal("e", ((TextRunBox)Assert.Single(row.Children)).Text);
        }


        // === placement ===

        [Fact]
        public void PlaceWalksTheChildrenLeftToRightFromTheOriginItIsGiven()
        {
            RowBox row = Engine().BuildRow(
                new List<MathToken> { Digit("1"), Digit("2"), new AnsToken() });

            row.Place(100, 50);

            Assert.Equal(100, row.Children[0].X);            // the two digit run
            Assert.Equal(100 + FontSize * 2, row.Children[1].X); // Ans starts after it
        }

        [Fact]
        public void PlaceCountsTheGapBeforeABoxAndTheOneBehindIt()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorGap = 0.2
            };

            RowBox row = Engine(style).BuildRow(
                new List<MathToken> { Digit("1"), Operator("+"), Digit("2") });

            row.Place(0, 0);

            double operatorSize = FontSize * 0.5;
            double gap = operatorSize * 0.2;

            Assert.Equal(0, row.Children[0].X);
            Assert.Equal(FontSize + gap, row.Children[1].X);
            Assert.Equal(FontSize + gap + operatorSize + gap, row.Children[2].X);
        }

        [Fact]
        public void APlacedBoxCarriesItsOwnBaselineWithTheRaiseAlreadyTakenOff()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 1.0,
                OperatorGap = 0,
                OperatorRaise = 0.25
            };

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Operator("+") });

            row.Place(0, 80);

            Assert.Equal(80 - FontSize * 0.25, row.Children.Single().Baseline);
        }

        [Fact]
        public void TopAndBottomFollowFromTheBaselineAndTheTwoReaches()
        {
            RowBox row = Engine().BuildRow(new List<MathToken> { Digit("5") });

            row.Place(0, 60);

            MathBox run = row.Children.Single();
            Assert.Equal(60 - FontSize * 0.75, run.Top);
            Assert.Equal(60 + FontSize * 0.25, run.Bottom);
        }
    }
}
