using System;
using FluentMath.Models;
using Xunit;

namespace FluentMath.Tests
{
    public class ResultFormatterTests
    {
        // === plain output ===

        [Fact]
        public void RoundsTheFloatingPointNoiseAway()
        {
            Assert.Equal("0.3", ResultFormatter.ToPlainString(0.1 + 0.2));
        }

        [Fact]
        public void PrintsNegativeZeroAsZero()
        {
            Assert.Equal("0", ResultFormatter.ToPlainString(-0.0));
        }

        [Fact]
        public void KeepsAPlainNumberInsideTheWindow()
        {
            Assert.Equal("1234.5", ResultFormatter.ToLatex(1234.5));
        }

        [Fact]
        public void SwitchesToScientificOutsideTheWindow()
        {
            Assert.Contains("10^{13}", ResultFormatter.ToLatex(1e13));
            Assert.Contains("10^{-10}", ResultFormatter.ToLatex(1e-10));
        }

        [Fact]
        public void KeepsTheBoundsThemselvesPlain()
        {
            Assert.DoesNotContain("10^", ResultFormatter.ToLatex(999999999999.0));
            Assert.DoesNotContain("10^", ResultFormatter.ToLatex(1e-9));
        }

        // a small number needs more decimals than Math.Round takes, and still gets all twelve digits
        [Fact]
        public void KeepsTwelveDigitsOfASmallPlainNumber()
        {
            Assert.Equal("0.0000000123456789012", ResultFormatter.ToPlainString(1.2345678901234e-8));
            Assert.Equal("0.00000000123456789012", ResultFormatter.ToPlainString(1.2345678901234e-9));
        }

        [Fact]
        public void RoundsToSignificantDigitsAtAnyMagnitude()
        {
            Assert.Equal(1.23456789012346, ResultFormatter.RoundToSignificantDigits(1.234567890123456e-20, 15) * 1e20, 14);
            Assert.Equal(0.5, ResultFormatter.RoundToSignificantDigits(0.49999999999999994, 15));
            Assert.Equal(123460000, ResultFormatter.RoundToSignificantDigits(123456789, 5));
        }


        // === prime factors ===

        // measured on the Casio: 1440 is 2^5×3^2×5
        [Fact]
        public void TakesAWholeNumberApartIntoPrimePowers()
        {
            Assert.True(ResultFormatter.TryPrimeFactors(1440, out var factors));
            Assert.Equal(new (long, int)[] { (2, 5), (3, 2), (5, 1) }, factors);
        }

        // the Casio gives up on 2027×2029 and shows (4112783); here every twelve digit number comes apart
        [Fact]
        public void FindsLargePrimeFactorsAsWell()
        {
            Assert.True(ResultFormatter.TryPrimeFactors(4112783, out var pair));
            Assert.Equal(new (long, int)[] { (2027, 1), (2029, 1) }, pair);

            Assert.True(ResultFormatter.TryPrimeFactors(999999999989, out var prime));
            Assert.Equal(new (long, int)[] { (999999999989, 1) }, prime);
        }

        [Fact]
        public void FindsNoPrimeFactorsWhereThereAreNone()
        {
            Assert.True(ResultFormatter.TryPrimeFactors(1, out var none));
            Assert.Empty(none);

            Assert.False(ResultFormatter.TryPrimeFactors(0, out _));
            Assert.False(ResultFormatter.TryPrimeFactors(-6, out _));
            Assert.False(ResultFormatter.TryPrimeFactors(1.5, out _));
            Assert.False(ResultFormatter.TryPrimeFactors(1e12, out _));
        }

        // judged on the twelve digits the display shows, so a result with noise behind them still counts
        [Fact]
        public void JudgesTheNumberTheDisplayShows()
        {
            Assert.True(ResultFormatter.TryPrimeFactors(0.1 * 30, out var factors));
            Assert.Equal(new (long, int)[] { (3, 1) }, factors);
        }

        [Fact]
        public void WritesThePrimeFactorsAsPowersJoinedByTimes()
        {
            string latex = ResultFormatter.ToLatex(EvaluationResult.Success(1440), AnswerForm.PrimeFactors, false);

            Assert.StartsWith("2^{5}", latex);
            Assert.Contains("3^{2}", latex);
            Assert.EndsWith("5", latex);
        }


        // === pairs ===

        [Fact]
        public void WritesAPairWithTheNamesOfItsValues()
        {
            EvaluationResult division = EvaluationResult.Pair(ResultKind.QuotientRemainder, 3, 2);
            Assert.Equal("Q=3, R=2", ResultFormatter.ToLatex(division, AnswerForm.Decimal, false));

            EvaluationResult polar = EvaluationResult.Pair(ResultKind.Polar, 5, 0.5);
            Assert.Equal("r=5, θ=\\frac{1}{2}", ResultFormatter.ToLatex(polar, AnswerForm.Improper, false));

            EvaluationResult rectangular = EvaluationResult.Pair(ResultKind.Rectangular, -1, 2);
            Assert.Equal("x=-1, y=2", ResultFormatter.ToLatex(rectangular, AnswerForm.Decimal, false));
        }


        // === fractions ===

        [Theory]
        [InlineData(1.0 / 3.0, 1, 3)]
        [InlineData(1.25, 5, 4)]
        [InlineData(0.75, 3, 4)]
        [InlineData(-1.25, -5, 4)]
        [InlineData(0.1 + 0.2, 3, 10)]
        [InlineData(0.0, 0, 1)]
        [InlineData(7.0 / 3.0, 7, 3)]          // a whole part takes one of the twelve digits
        [InlineData(-22.0 / 7.0, -22, 7)]
        [InlineData(12346.0 / 9999.0, 12346, 9999)]
        public void FindsTheFractionBehindADecimal(double value, long expectedNumerator, long expectedDenominator)
        {
            Assert.True(ResultFormatter.TryToFraction(value, out long numerator, out long denominator));
            Assert.Equal(expectedNumerator, numerator);
            Assert.Equal(expectedDenominator, denominator);
        }

        [Fact]
        public void LeavesAnIrrationalWithoutAFraction()
        {
            Assert.False(ResultFormatter.HasFractionForm(Math.Sqrt(2)));
            Assert.False(ResultFormatter.HasFractionForm(Math.PI));
            Assert.False(ResultFormatter.HasFractionForm(Math.E));
        }

        [Fact]
        public void CountsAWholeNumberAsHavingNoFractionForm()
        {
            Assert.False(ResultFormatter.HasFractionForm(4));
            Assert.False(ResultFormatter.HasFractionForm(-12));
        }

        [Fact]
        public void OffersAMixedFormOnlyAboveOne()
        {
            Assert.True(ResultFormatter.HasMixedForm(1.25));
            Assert.True(ResultFormatter.HasMixedForm(-1.25));
            Assert.False(ResultFormatter.HasMixedForm(1.0 / 3.0));
        }

        [Fact]
        public void WritesAnImproperFraction()
        {
            Assert.Contains("frac{5}{4}", ResultFormatter.ToLatex(1.25, AnswerForm.Improper, false));
        }

        [Fact]
        public void WritesAMixedNumberWithTheSignOnTheWholePart()
        {
            string latex = ResultFormatter.ToLatex(-1.25, AnswerForm.Mixed, false);

            Assert.StartsWith("-1", latex);
            Assert.Contains("frac{1}{4}", latex);
        }

        [Fact]
        public void TakesTheDisplayStyleFractionWhenAskedFor()
        {
            Assert.Contains("dfrac", ResultFormatter.ToLatex(1.25, AnswerForm.Improper, true));
        }

        [Fact]
        public void FallsBackToTheDecimalWhereThereIsNoFraction()
        {
            double value = Math.Sqrt(2);

            Assert.Equal(ResultFormatter.ToLatex(value), ResultFormatter.ToLatex(value, AnswerForm.Improper, false));
            Assert.Equal(ResultFormatter.ToLatex(4), ResultFormatter.ToLatex(4, AnswerForm.Mixed, false));
        }
    }
}
