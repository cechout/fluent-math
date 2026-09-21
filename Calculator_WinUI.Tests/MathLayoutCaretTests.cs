using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the caret
    //
    // the rule the whole of it reduces to is that it may not change anything around it, which the KaTeX
    // display got wrong three times in three separate sessions; these assertions are that rule written
    // down, so it cannot quietly come back
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

        private static RowBox WithCaret(List<MathToken> tokens, int index)
        {
            MathLayoutEngine engine = new MathLayoutEngine(
                new FakeMeasurer(), Style(), new CaretTarget(tokens, index));

            return engine.BuildRow(tokens);
        }

        private static RowBox WithoutCaret(List<MathToken> tokens)
        {
            return new MathLayoutEngine(new FakeMeasurer(), Style()).BuildRow(tokens);
        }

        private static MathToken Digit(string value) => new MathToken(TokenType.Number, value);

        private static List<MathToken> Number(string digits)
        {
            return digits.Select(character => Digit(character.ToString())).ToList();
        }


        // === the rule ===

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ACaretNeverChangesTheWidthOfTheRowItStandsIn(int position)
        {
            List<MathToken> tokens = Number("123");

            Assert.Equal(WithoutCaret(tokens).Width, WithCaret(tokens, position).Width);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ACaretNeverChangesTheHeightOfTheRowItStandsIn(int position)
        {
            List<MathToken> tokens = Number("12");
            RowBox plain = WithoutCaret(tokens);
            RowBox carried = WithCaret(tokens, position);

            Assert.Equal(plain.Ascent, carried.Ascent);
            Assert.Equal(plain.Descent, carried.Descent);
        }

        [Fact]
        public void ACaretHasNoWidthOfItsOwn()
        {
            RowBox row = WithCaret(Number("1"), 0);

            Assert.Equal(0, row.Children.OfType<CaretBox>().Single().Width);
        }

        [Fact]
        public void ACaretInsideAFractionLeavesTheFractionTheSizeItWas()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            List<MathToken> tokens = new List<MathToken> { fraction };

            MathLayoutEngine engine = new MathLayoutEngine(
                new FakeMeasurer(), Style(), new CaretTarget(fraction.DenominatorTokens, 0));

            FractionBox plain = (FractionBox)WithoutCaret(tokens).Children.Single();
            FractionBox carried = (FractionBox)engine.BuildRow(tokens).Children.Single();

            Assert.Equal(plain.Width, carried.Width);
            Assert.Equal(plain.Ascent, carried.Ascent);
            Assert.Equal(plain.Descent, carried.Descent);
        }


        // === where it lands ===

        [Fact]
        public void ACaretAtTheStartStandsBeforeEverythingElse()
        {
            RowBox row = WithCaret(Number("12"), 0);

            Assert.IsType<CaretBox>(row.Children[0]);
        }

        [Fact]
        public void ACaretAtTheEndStandsAfterEverythingElse()
        {
            RowBox row = WithCaret(Number("12"), 2);

            Assert.IsType<CaretBox>(row.Children[row.Children.Count - 1]);
        }

        [Fact]
        public void ACaretInsideANumberSplitsTheRunAndSitsBetweenTheHalves()
        {
            RowBox row = WithCaret(Number("123"), 1);

            Assert.Equal(3, row.Children.Count);
            Assert.Equal("1", ((TextRunBox)row.Children[0]).Text);
            Assert.IsType<CaretBox>(row.Children[1]);
            Assert.Equal("23", ((TextRunBox)row.Children[2]).Text);
        }

        [Fact]
        public void TheTwoHalvesOfASplitRunAddUpToTheWidthOfTheWholeOne()
        {
            List<MathToken> tokens = Number("123");
            RowBox row = WithCaret(tokens, 1);

            double left = row.Children[0].Width;
            double right = row.Children[2].Width;

            Assert.Equal(WithoutCaret(tokens).Width, left + right);
        }

        [Fact]
        public void ACaretMovingThroughANumberDoesNotShiftTheDigitsAroundIt()
        {
            List<MathToken> tokens = Number("1234");

            // the digit after the caret has to start in the same place wherever the caret came from
            RowBox afterFirst = WithCaret(tokens, 1);
            afterFirst.Place(0, 0);
            double reachedByOne = afterFirst.Children[0].Width;

            RowBox afterSecond = WithCaret(tokens, 2);
            afterSecond.Place(0, 0);
            double reachedByTwo = afterSecond.Children[0].Width;

            Assert.Equal(FontSize, reachedByOne);
            Assert.Equal(FontSize * 2, reachedByTwo);
        }

        [Fact]
        public void ACaretOnlyAppearsInTheListItBelongsTo()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            List<MathToken> tokens = new List<MathToken> { fraction };

            MathLayoutEngine engine = new MathLayoutEngine(
                new FakeMeasurer(), Style(), new CaretTarget(fraction.NumeratorTokens, 0));

            FractionBox box = (FractionBox)engine.BuildRow(tokens).Children.Single();

            Assert.Single(((RowBox)box.Numerator).Children.OfType<CaretBox>());
            Assert.Empty(((RowBox)box.Denominator).Children.OfType<CaretBox>());
        }

        [Fact]
        public void TwoEmptySlotsAreToldApartByIdentityRatherThanByContents()
        {
            FractionToken fraction = new FractionToken();
            List<MathToken> tokens = new List<MathToken> { fraction };

            // both slots are empty and equal by value; only the reference says which one the caret is in
            MathLayoutEngine engine = new MathLayoutEngine(
                new FakeMeasurer(), Style(), new CaretTarget(fraction.DenominatorTokens, 0));

            FractionBox box = (FractionBox)engine.BuildRow(tokens).Children.Single();

            Assert.IsType<PlaceholderBox>(box.Numerator);
            Assert.Single(((RowBox)box.Denominator).Children.OfType<CaretBox>());
        }


        // === standing alone ===

        [Fact]
        public void ARowHoldingNothingButACaretIsStillAsTallAsOneHoldingADigit()
        {
            List<MathToken> empty = new List<MathToken>();
            RowBox carried = WithCaret(empty, 0);

            Assert.Equal(FontSize * 0.75, carried.Ascent);
            Assert.Equal(FontSize * 0.25, carried.Descent);
            Assert.Equal(0, carried.Width);
        }
    }
}
