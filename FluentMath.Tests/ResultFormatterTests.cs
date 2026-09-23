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

        // === exact forms ===

        private static MathValue Exact(ExactValue value) => new MathValue(0, value);

        private static ExactValue Root(long radicand) => ExactValue.SquareRoot(ExactValue.FromInteger(radicand))!;

        private static ExactValue Fraction(long numerator, long denominator)
        {
            return ExactValue.FromRational(new Rational(numerator, denominator));
        }

        // twelve characters written as a mixed number, sign and separators included
        [Fact]
        public void ShowsAFractionOnlyWithinTheBudget()
        {
            Assert.True(ResultFormatter.TryFraction(Exact(Fraction(1, 12345)), out long numerator, out long denominator));
            Assert.Equal(1, numerator);
            Assert.Equal(12345, denominator);

            Assert.True(ResultFormatter.HasFractionForm(Exact(Fraction(-1, 123456789))));
            Assert.True(ResultFormatter.HasFractionForm(Exact(Fraction(12345678, 89))));

            Assert.False(ResultFormatter.HasFractionForm(Exact(Fraction(-1, 1234567890))));
            Assert.False(ResultFormatter.HasFractionForm(Exact(Fraction(123456789, 89))));
        }

        // an exact value knows it is irrational, and a value without one is still searched numerically
        [Fact]
        public void TakesTheFractionFromTheExactValueWhereThereIsOne()
        {
            Assert.False(ResultFormatter.HasFractionForm(Exact(Root(2))));
            Assert.True(ResultFormatter.HasExactForm(Exact(Root(2))));

            Assert.True(ResultFormatter.TryFraction(0.75, out long numerator, out long denominator));
            Assert.Equal(3, numerator);
            Assert.Equal(4, denominator);
        }

        [Fact]
        public void WritesARootFormOverOneDenominator()
        {
            ExactValue sine15 = ExactValue.Multiply(ExactValue.Subtract(Root(6), Root(2)), Fraction(1, 4))!;
            Assert.Equal("\\frac{\\sqrt{6}-\\sqrt{2}}{4}", ResultFormatter.ToLatex(Exact(sine15), AnswerForm.Improper, false));

            ExactValue half = ExactValue.Add(Fraction(1, 2), Root(3))!;
            Assert.Equal("\\dfrac{1+2\\sqrt{3}}{2}", ResultFormatter.ToLatex(Exact(half), AnswerForm.Mixed, true));

            // a single term keeps its sign in front of the bar
            ExactValue negative = ExactValue.Multiply(Root(2), Fraction(-1, 2))!;
            Assert.Equal("-\\frac{\\sqrt{2}}{2}", ResultFormatter.ToLatex(Exact(negative), AnswerForm.Improper, false));
        }

        // the ranges of the Casio manual: coefficients and denominator below 100, radicands below 1000
        [Fact]
        public void ShowsNoRootFormOutsideTheRanges()
        {
            Assert.False(ResultFormatter.HasExactForm(Exact(ExactValue.Multiply(Root(2), ExactValue.FromInteger(100))!)));
            Assert.False(ResultFormatter.HasExactForm(Exact(Root(1003))));
            Assert.False(ResultFormatter.HasExactForm(Exact(ExactValue.Multiply(Root(2), Fraction(1, 100))!)));

            Assert.True(ResultFormatter.HasExactForm(Exact(ExactValue.Multiply(Root(2), Fraction(1, 99))!)));
        }

        [Fact]
        public void WritesTheDecimalOfAFormThatIsAskedFor()
        {
            Assert.StartsWith("1.41421356237", ResultFormatter.ToLatex(new MathValue(0, Root(2)), AnswerForm.Decimal, false));
        }


        // === recurring decimals ===

        [Theory]
        [InlineData(7, 3, "2.", "3")]
        [InlineData(-7, 3, "-2.", "3")]
        [InlineData(5, 12, "0.41", "6")]
        [InlineData(1, 6, "0.1", "6")]
        [InlineData(1, 7, "0.", "142857")]
        [InlineData(1, 17, "0.", "0588235294117647")]
        public void FindsThePeriodOfAFraction(long numerator, long denominator, string expectedLeading, string expectedPeriod)
        {
            Assert.True(ResultFormatter.TryRecurring(Exact(Fraction(numerator, denominator)), out string leading, out string period));
            Assert.Equal(expectedLeading, leading);
            Assert.Equal(expectedPeriod, period);
        }

        // a decimal that ends has no period, and one that takes more than sixteen digits to repeat has
        // none shown; a whole part counts towards them
        [Fact]
        public void FindsNoPeriodWhereThereIsNoneToShow()
        {
            Assert.False(ResultFormatter.HasRecurringForm(Exact(Fraction(1, 4))));
            Assert.False(ResultFormatter.HasRecurringForm(Exact(Fraction(1, 97))));
            Assert.False(ResultFormatter.HasRecurringForm(Exact(Fraction(100, 17))));
            Assert.False(ResultFormatter.HasRecurringForm(Exact(ExactValue.FromInteger(5))));
            Assert.False(ResultFormatter.HasRecurringForm(Exact(Root(2))));
        }

        [Fact]
        public void WritesThePeriodUnderABar()
        {
            Assert.Equal("0.41\\overline{6}", ResultFormatter.ToLatex(Exact(Fraction(5, 12)), AnswerForm.Recurring, false));
        }

        [Fact]
        public void FallsBackToTheDecimalWhereThereIsNoFraction()
        {
            double value = Math.Sqrt(2);

            Assert.Equal(ResultFormatter.ToLatex(value), ResultFormatter.ToLatex(value, AnswerForm.Improper, false));
            Assert.Equal(ResultFormatter.ToLatex(4), ResultFormatter.ToLatex(4, AnswerForm.Mixed, false));
        }


        // === number formats ===

        private static (string Digits, int? Exponent) Written(double value, NumberFormat format)
        {
            WrittenDecimal written = ResultFormatter.Write(value, format);
            return (written.Digits, written.Exponent);
        }

        // measured on the Casio: 1÷200 is 5×10⁻³ in Norm 1, the setting it ships with
        [Fact]
        public void WritesNorm1WithAPowerOfTenBelowAHundredth()
        {
            NumberFormat norm1 = new NumberFormat(NumberNotation.Norm1);

            Assert.Equal(("5", (int?)-3), Written(0.005, norm1));
            Assert.Equal(("0.01", (int?)null), Written(0.01, norm1));
            Assert.Equal(("0.005", (int?)null), Written(0.005, NumberFormat.Default));
        }

        [Fact]
        public void WritesAFixedNumberOfDecimals()
        {
            NumberFormat fix2 = new NumberFormat(NumberNotation.Fix, 2);

            Assert.Equal(("0.33", (int?)null), Written(1.0 / 3, fix2));
            Assert.Equal(("5.00", (int?)null), Written(5, fix2));
            Assert.Equal(("2.68", (int?)null), Written(2.675, fix2));
            Assert.Equal(("0", (int?)null), Written(0.4, new NumberFormat(NumberNotation.Fix, 0)));

            // too large for the display, and the mantissa keeps the decimals
            Assert.Equal(("1.23", (int?)15), Written(1.234e15, fix2));
        }

        // the digits carry the value on into the next calculation, so they cannot drop its sign
        [Fact]
        public void KeepsTheMinusOfANegativeNumberThatFixRoundsToNothing()
        {
            Assert.Equal(("-0.00", (int?)null), Written(-0.001, new NumberFormat(NumberNotation.Fix, 2)));
        }

        [Fact]
        public void WritesAFixedNumberOfSignificantDigits()
        {
            NumberFormat sci3 = new NumberFormat(NumberNotation.Sci, 3);

            Assert.Equal(("1.23", (int?)3), Written(1234, sci3));
            Assert.Equal(("1.00", (int?)0), Written(1, sci3));
            Assert.Equal(("1.00", (int?)1), Written(9.996, sci3));
            Assert.Equal(("-2.5", (int?)-4), Written(-0.00025, new NumberFormat(NumberNotation.Sci, 2)));

            // Sci 0 is every digit the display has
            Assert.Equal(("3.33333333333", (int?)-1), Written(1.0 / 3, new NumberFormat(NumberNotation.Sci, 0)));
        }

        [Fact]
        public void HandsBackTheNumberItWrote()
        {
            Assert.Equal(1230, ResultFormatter.RoundToFormat(1234, new NumberFormat(NumberNotation.Sci, 3)));
            Assert.Equal(1.23e15, ResultFormatter.RoundToFormat(1.234e15, new NumberFormat(NumberNotation.Fix, 2)));
        }


        // === engineering ===

        private static (string Digits, int? Exponent) Engineering(double value, int exponent, NumberFormat format)
        {
            WrittenDecimal written = ResultFormatter.Engineering(value, exponent, format, usePrefixes: false);
            return (written.Digits, written.Exponent);
        }

        // measured on the Casio: 1234 with ENG is 1.234×10³, ENG again 1234×10⁰; 123 with the shift of
        // ENG is 0.123×10³
        [Fact]
        public void WritesTheValueOverAPowerOfThree()
        {
            Assert.Equal(3, ResultFormatter.EngineeringExponent(1234));
            Assert.Equal(0, ResultFormatter.EngineeringExponent(123));
            Assert.Equal(-6, ResultFormatter.EngineeringExponent(0.0000456));

            Assert.Equal(("1.234", (int?)3), Engineering(1234, 3, NumberFormat.Default));
            Assert.Equal(("1234", (int?)0), Engineering(1234, 0, NumberFormat.Default));
            Assert.Equal(("0.123", (int?)3), Engineering(123, 3, NumberFormat.Default));
            Assert.Equal(("-45.6", (int?)-6), Engineering(-0.0000456, -6, NumberFormat.Default));
        }

        [Fact]
        public void WritesTheEngineeringMantissaInTheNumberFormat()
        {
            Assert.Equal(("1.23", (int?)3), Engineering(1234, 3, new NumberFormat(NumberNotation.Fix, 2)));
            Assert.Equal(("1230", (int?)0), Engineering(1234, 0, new NumberFormat(NumberNotation.Sci, 3)));
        }

        [Fact]
        public void StopsSteppingWhereTheMantissaWouldNeedAPowerOfItsOwn()
        {
            Assert.True(ResultFormatter.CanWriteEngineering(1234, -6));
            Assert.False(ResultFormatter.CanWriteEngineering(1234, -9));
            Assert.True(ResultFormatter.CanWriteEngineering(1234, 12));
            Assert.False(ResultFormatter.CanWriteEngineering(1234, 15));
            Assert.False(ResultFormatter.CanWriteEngineering(0, 0));
        }

        [Fact]
        public void WritesThePrefixInsteadOfThePowerWhenAskedTo()
        {
            WrittenDecimal kilo = ResultFormatter.Engineering(1234, 3, NumberFormat.Default, usePrefixes: true);
            Assert.Equal("1.234", kilo.Digits);
            Assert.Equal("kilo", kilo.Prefix);

            // the power 0 has no prefix and needs none
            WrittenDecimal plain = ResultFormatter.Engineering(1234, 0, NumberFormat.Default, usePrefixes: true);
            Assert.Equal("1234", plain.Digits);
            Assert.Null(plain.Exponent);

            // past exa there is none, and the power stays
            WrittenDecimal beyond = ResultFormatter.Engineering(1e21, 21, NumberFormat.Default, usePrefixes: true);
            Assert.Null(beyond.Prefix);
            Assert.Equal(21, beyond.Exponent);
        }
    }
}
