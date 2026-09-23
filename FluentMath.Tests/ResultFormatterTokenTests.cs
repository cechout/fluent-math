using FluentMath.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluentMath.Tests
{
    // the result as tokens, for the display that draws them rather than handing LaTeX to a browser
    //
    // ToTokens mirrors ToLatex arm for arm, so these check the same decisions ResultFormatterTests checks
    // on the string side; the two drifting apart is the thing to catch
    public class ResultFormatterTokenTests
    {
        private const string Minus = "−";

        private static string Digits(IEnumerable<MathToken> tokens)
        {
            return string.Concat(tokens.Where(token => token.Type == TokenType.Number).Select(token => token.Value));
        }


        // === plain numbers ===

        [Fact]
        public void AWholeNumberComesBackAsOneTokenPerDigit()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(125);

            Assert.Equal(3, tokens.Count);
            Assert.All(tokens, token => Assert.Equal(TokenType.Number, token.Type));
            Assert.Equal("125", Digits(tokens));
        }

        [Fact]
        public void ADecimalPointIsJustAnotherDigitToken()
        {
            Assert.Equal("1.5", Digits(ResultFormatter.ToTokens(1.5)));
        }

        [Fact]
        public void ANegativeResultCarriesARealMinusInsideTheNumberRatherThanAnOperator()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(-7);

            // an operator token would take the spacing that belongs between two operands
            Assert.All(tokens, token => Assert.Equal(TokenType.Number, token.Type));
            Assert.Equal(Minus + "7", Digits(tokens));
        }

        [Fact]
        public void ZeroStaysZero()
        {
            Assert.Equal("0", Digits(ResultFormatter.ToTokens(0)));
        }


        // === the fraction forms ===

        [Fact]
        public void TheImproperFormIsAFractionToken()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(0.75, AnswerForm.Improper, false);

            FractionToken fraction = Assert.IsType<FractionToken>(Assert.Single(tokens));
            Assert.Equal("3", Digits(fraction.NumeratorTokens));
            Assert.Equal("4", Digits(fraction.DenominatorTokens));
        }

        // the structure the mixed fraction key types, so seeding it puts back exactly what is drawn
        [Fact]
        public void TheMixedFormIsAMixedFractionToken()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(1.75, AnswerForm.Mixed, false);

            MixedFractionToken mixed = Assert.IsType<MixedFractionToken>(Assert.Single(tokens));
            Assert.Equal("1", Digits(mixed.WholeTokens));
            Assert.Equal("3", Digits(mixed.NumeratorTokens));
            Assert.Equal("4", Digits(mixed.DenominatorTokens));
        }

        [Fact]
        public void TheSignOfAMixedNumberRidesOnItsWholePart()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(-1.75, AnswerForm.Mixed, false);

            MixedFractionToken mixed = Assert.IsType<MixedFractionToken>(Assert.Single(tokens));
            Assert.Equal("−1", Digits(mixed.WholeTokens));
            Assert.Equal("3", Digits(mixed.NumeratorTokens));
        }

        [Fact]
        public void AValueWithNoFractionFallsBackToTheDecimal()
        {
            // the same fallback ToLatex makes: a value out of a root has no fraction to find
            List<MathToken> tokens = ResultFormatter.ToTokens(System.Math.Sqrt(2), AnswerForm.Improper, false);

            Assert.All(tokens, token => Assert.Equal(TokenType.Number, token.Type));
        }

        [Fact]
        public void AWholeNumberInFractionFormStaysAWholeNumber()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(4, AnswerForm.Improper, false);

            Assert.Equal("4", Digits(tokens));
        }


        // === scientific ===

        [Fact]
        public void AValueOutsideTheWindowIsSpelledOutAsAMultiplicationAndAPower()
        {
            // the same shape the EXP key produces, so a result and a typed formula are one thing rather
            // than two that happen to look alike
            List<MathToken> tokens = ResultFormatter.ToTokens(1.5e20);

            Assert.Equal("1.5", Digits(tokens.Take(tokens.Count - 2)));
            Assert.Equal(TokenType.Operator, tokens[tokens.Count - 2].Type);

            PowerToken power = Assert.IsType<PowerToken>(tokens.Last());
            Assert.Equal("10", Digits(power.BaseTokens));
            Assert.Equal("20", Digits(power.ExponentTokens));
        }

        [Fact]
        public void ANegativeExponentKeepsItsMinusInTheExponentSlot()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(1.5e-20);

            PowerToken power = Assert.IsType<PowerToken>(tokens.Last());
            Assert.Equal(Minus + "20", Digits(power.ExponentTokens));
        }


        // === the two shapes agree ===

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(-7)]
        [InlineData(1.5)]
        [InlineData(1e20)]
        [InlineData(1e-20)]
        public void WhereverToLatexWritesAFractionOrAPowerToTokensBuildsOne(double value)
        {
            string latex = ResultFormatter.ToLatex(value);
            List<MathToken> tokens = ResultFormatter.ToTokens(value);

            bool latexIsScientific = latex.Contains("10^");
            bool tokensAreScientific = tokens.Any(token => token is PowerToken);

            Assert.Equal(latexIsScientific, tokensAreScientific);
        }
    }
}
