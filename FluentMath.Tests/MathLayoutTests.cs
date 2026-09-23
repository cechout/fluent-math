using FluentMath.Models;
using FluentMath.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluentMath.Tests
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

        // the same rule the air around a radicand follows, and the reason it is written once: a gap in em
        // shrinks with its own piece, and inside a structure that is set smaller it shrinks twice over
        [Theory]
        [InlineData(1.0, 0.5)]  // fully proportional: half the size, half the gap
        [InlineData(0.0, 1.0)]  // held: the gap of a full size operator, whatever size this one is
        [InlineData(0.5, 0.75)] // half way between the two
        public void TheGapOfAnOperatorFollowsItsSizeOnlyAsFarAsItsScalingSays(
            double scaling, double expectedShare)
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorGap = 0.2,
                OperatorGapScaling = scaling
            };

            // set at half the size of the line it is in, the way one inside a fraction is
            RowBox row = Engine(style).BuildRow(
                new List<MathToken> { Operator("+") }, FontSize * 0.5, 1);

            double fullGap = FontSize * 0.5 * 0.2;
            Assert.Equal(fullGap * expectedShare, row.Children.Single().LeadingGap, 9);
        }

        [Fact]
        public void AnOperatorRidesAboveTheBaselineByItsOwnRaise()
        {
            MathLayoutStyle style = new MathLayoutStyle
            {
                FontSizePx = FontSize,
                OperatorScale = 0.5,
                OperatorRaise = 0.25,
                MathAxisRaise = 0
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
                OperatorRaise = 0.25,
                MathAxisRaise = 0
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
                OperatorRaise = 0.25,
                MathAxisRaise = 0
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


        // === numbers ===

        [Fact]
        public void DrawsTheDecimalPointAsTheMarkTheStyleAsksFor()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, DecimalMark = "," };
            RowBox row = Engine(style).BuildRow(new List<MathToken> { Digit("1"), Digit("."), Digit("5") });

            TextRunBox run = Assert.IsType<TextRunBox>(Assert.Single(row.Children));
            Assert.Equal("1,5", run.Text);
            Assert.Equal(3, run.Tokens.Count);
        }

        // a comma between two values would read as a decimal comma, so it turns into a semicolon
        [Fact]
        public void SeparatesTwoArgumentsWithASemicolonBesideADecimalComma()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, DecimalMark = "," };
            FunctionToken gcd = new FunctionToken("gcd");
            gcd.Arguments[0].Add(Digit("4"));
            gcd.Arguments[1].Add(Digit("6"));

            RowBox function = (RowBox)Engine(style).BuildRow(new List<MathToken> { gcd }).Children.Single();
            RowBox arguments = (RowBox)function.Children[2];

            Assert.Equal(";", ((TextRunBox)arguments.Children[1]).Text);
        }

        [Fact]
        public void GroupsTheWholePartInThreesAndLeavesTheDecimalsAlone()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, GroupDigits = true };
            List<MathToken> tokens = "1234567.8912".Select(character => Digit(character.ToString())).ToList();

            RowBox row = Engine(style).BuildRow(tokens);

            Assert.Equal(new[] { "1", "234", "567.8912" }, row.Children.Cast<TextRunBox>().Select(run => run.Text));
            Assert.Equal(FontSize * 0.2 * 2 + 12 * FontSize, row.Width, 9);
        }

        // the place between two groups is the middle of the gap, where the caret stands and a click lands
        [Fact]
        public void PutsThePlaceBetweenTwoGroupsInTheMiddleOfTheGap()
        {
            MathLayoutStyle style = new MathLayoutStyle { FontSizePx = FontSize, GroupDigits = true };
            List<MathToken> tokens = "1234".Select(character => Digit(character.ToString())).ToList();

            MathLayoutEngine engine = new MathLayoutEngine(new FakeMeasurer(), style, new CaretTarget(tokens, 1));
            RowBox row = engine.BuildRow(tokens);
            row.Place(0, row.Ascent);

            double middle = FontSize + FontSize * 0.1;
            Assert.Equal(middle, engine.Caret.Value.Box.X + engine.Caret.Value.Offset, 9);
            Assert.Equal("@1", MathHitTest.NearestAddress(row, middle + 1, row.Baseline));
        }
    }
}
