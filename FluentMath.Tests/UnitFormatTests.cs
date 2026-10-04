using FluentMath.Models;
using FluentMath.Models.Converters;
using Xunit;

namespace FluentMath.Tests
{
    // the numbers a converter shows: the rounding to plain text, then the decimal mark and the grouping
    public class UnitFormatTests
    {
        private static readonly ConverterSettings Defaults = new ConverterSettings();

        private static ConverterSettings Precision(int currencyDecimals = 2, int unitDigits = 10)
        {
            return new ConverterSettings { CurrencyDecimals = currencyDecimals, UnitDigits = unitDigits };
        }


        // === money ===

        [Theory]
        [InlineData(1.5, "1.5")]
        [InlineData(2.0, "2")]
        [InlineData(1.23456, "1.23")]
        [InlineData(0.004, "0")]
        [InlineData(123456789.126, "123456789.13")]
        public void MoneyKeepsItsDecimalsAndDropsTrailingZeros(double value, string expected)
        {
            Assert.Equal(expected, UnitFormat.Amount(value, roundsToDecimals: true, Defaults));
        }

        [Fact]
        public void AHalfRoundsAwayFromZero()
        {
            Assert.Equal("3", UnitFormat.Amount(2.5, roundsToDecimals: true, Precision(currencyDecimals: 0)));
        }

        [Fact]
        public void TheCurrencyRateKeepsTwoDecimalsMoreThanAnAmount()
        {
            Assert.Equal("1.0843", UnitFormat.Rate(1.084271, roundsToDecimals: true, Defaults));
            Assert.Equal("1.084271", UnitFormat.Rate(1.084271, roundsToDecimals: true, Precision(currencyDecimals: 4)));
        }


        // === measures ===

        [Theory]
        [InlineData(1 / 0.3048, "3.280839895")]
        [InlineData(12.000000000000002, "12")]
        [InlineData(1234567890123.0, "1234567890000")]
        [InlineData(0.000123456789012, "0.000123456789")]
        [InlineData(0.0, "0")]
        public void AMeasureKeepsItsSignificantDigits(double value, string expected)
        {
            Assert.Equal(expected, UnitFormat.Amount(value, roundsToDecimals: false, Defaults));
        }

        [Theory]
        [InlineData(1e-12, "1e-12")]
        [InlineData(1.5e-12, "1.5e-12")]
        [InlineData(2e15, "2e15")]
        [InlineData(1.23456789012345e20, "1.23456789e20")]
        public void AMeasureOutsideThePlainRangeGetsAPowerOfTen(double value, string expected)
        {
            Assert.Equal(expected, UnitFormat.Amount(value, roundsToDecimals: false, Defaults));
        }

        [Fact]
        public void TheEdgesOfThePlainRange()
        {
            Assert.Equal("0.000000001", UnitFormat.Amount(1e-9, roundsToDecimals: false, Defaults));
            Assert.Equal("999999999999999", UnitFormat.Amount(999999999999999, roundsToDecimals: false, Precision(unitDigits: 15)));
        }

        [Fact]
        public void FewerDigitsRoundSooner()
        {
            Assert.Equal("3.28084", UnitFormat.Amount(1 / 0.3048, roundsToDecimals: false, Precision(unitDigits: 6)));
        }


        // === display ===

        [Theory]
        [InlineData("1234567.5", "1 234 567,5")]
        [InlineData("123", "123")]
        [InlineData("1234", "1 234")]
        [InlineData("0.", "0,")]
        [InlineData("1.5e-12", "1,5e-12")]
        [InlineData("12345e20", "12 345e20")]
        public void DisplayPutsInTheMarkAndTheGaps(string plain, string expected)
        {
            var numbers = new CalculatorSettings { DecimalMark = DecimalMark.Comma, GroupDigits = true };

            Assert.Equal(expected, UnitFormat.Display(plain, numbers));
        }

        [Fact]
        public void WithoutGroupingOnlyTheMarkChanges()
        {
            var numbers = new CalculatorSettings { DecimalMark = DecimalMark.Comma, GroupDigits = false };

            Assert.Equal("1234567,5", UnitFormat.Display("1234567.5", numbers));
        }

        [Fact]
        public void ADotSetupLeavesThePlainTextAsItIs()
        {
            var numbers = new CalculatorSettings { DecimalMark = DecimalMark.Dot, GroupDigits = false };

            Assert.Equal("1234567.5", UnitFormat.Display("1234567.5", numbers));
        }
    }
}
