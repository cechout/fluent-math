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
        public void APairIsBothValuesWithTheirNamesAsText()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(
                EvaluationResult.Pair(ResultKind.QuotientRemainder, 3, 2), AnswerForm.Decimal, false);

            Assert.Equal("Q=3, R=2", string.Concat(tokens.Select(token => token.Value)));
        }

        // real powers and real times signs, so seeding them carries the product on
        [Fact]
        public void ThePrimeFactorsArePowersJoinedByTimes()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(EvaluationResult.Success(1440), AnswerForm.PrimeFactors, false);

            PowerToken two = Assert.IsType<PowerToken>(tokens[0]);
            Assert.Equal("2", Digits(two.BaseTokens));
            Assert.Equal("5", Digits(two.ExponentTokens));
            Assert.Equal("*", tokens[1].Value);
            Assert.Equal("5", tokens.Last().Value);
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


        // === exact forms ===

        private static MathValue Exact(ExactValue value) => new MathValue(0, value);

        private static ExactValue Root(long radicand) => ExactValue.SquareRoot(ExactValue.FromInteger(radicand))!;

        [Fact]
        public void ARecurringDecimalIsItsDigitsAndThePeriodAsAToken()
        {
            MathValue sevenThirds = Exact(ExactValue.FromRational(new Rational(7, 3)));
            List<MathToken> tokens = ResultFormatter.ToTokens(sevenThirds, AnswerForm.Recurring, false);

            Assert.Equal("2.", Digits(tokens));

            RecurringToken period = Assert.IsType<RecurringToken>(tokens.Last());
            Assert.Equal("3", period.Value);
        }

        // real roots over a real bar, so seeding them carries the exact value on
        [Fact]
        public void ARootFormIsAFractionOverRealRoots()
        {
            ExactValue sine15 = ExactValue.Multiply(ExactValue.Subtract(Root(6), Root(2)),
                ExactValue.FromRational(new Rational(1, 4)))!;

            FractionToken fraction = Assert.IsType<FractionToken>(Assert.Single(
                ResultFormatter.ToTokens(Exact(sine15), AnswerForm.Improper, false)));

            Assert.Equal("6", Digits(Assert.IsType<RootToken>(fraction.NumeratorTokens[0]).RadicandTokens));
            Assert.Equal("-", fraction.NumeratorTokens[1].Value);
            Assert.Equal(TokenType.Operator, fraction.NumeratorTokens[1].Type);
            Assert.Equal("2", Digits(Assert.IsType<RootToken>(fraction.NumeratorTokens[2]).RadicandTokens));
            Assert.Equal("4", Digits(fraction.DenominatorTokens));
        }

        // the minus in front of the whole value is drawn as part of it and seeded as a sign
        [Fact]
        public void ALeadingMinusIsDrawnWithTheNumberAndSeededAsASign()
        {
            MathValue value = Exact(ExactValue.Subtract(Root(2), Root(3))!);

            MathToken drawn = ResultFormatter.ExactFormTokens(value, asInput: false)![0];
            Assert.Equal(TokenType.Number, drawn.Type);
            Assert.Equal(Minus, drawn.Value);

            MathToken seeded = ResultFormatter.ExactFormTokens(value, asInput: true)![0];
            Assert.Equal(TokenType.Operator, seeded.Type);
            Assert.Equal("-", seeded.Value);
        }

        [Fact]
        public void APiFormIsItsCoefficientAndTheConstant()
        {
            MathValue value = Exact(ExactValue.Multiply(ExactValue.Pi, ExactValue.FromRational(new Rational(2, 3)))!);
            List<MathToken> tokens = ResultFormatter.ToTokens(value, AnswerForm.Improper, false);

            Assert.IsType<FractionToken>(tokens[0]);
            Assert.Equal("pi", Assert.IsType<ConstantToken>(tokens[1]).Value);
        }


        // === sexagesimal ===

        // real markers between the digits, the minus drawn with the number and seeded as a sign
        [Fact]
        public void AnAngleIsItsDigitsWithTheRealMarkers()
        {
            List<MathToken> tokens = ResultFormatter.ToTokens(new MathValue(-2.2583), AnswerForm.Sexagesimal, false);

            Assert.Equal(Minus + "21529.88", Digits(tokens));
            Assert.Equal(new[] { "degrees", "minutes", "seconds" }, tokens.OfType<PostfixToken>().Select(marker => marker.Value));
            Assert.Equal(TokenType.Number, tokens[0].Type);

            MathToken seeded = ResultFormatter.SexagesimalTokens(-2.2583, asInput: true)![0];
            Assert.Equal(TokenType.Operator, seeded.Type);
            Assert.Equal("-", seeded.Value);
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
