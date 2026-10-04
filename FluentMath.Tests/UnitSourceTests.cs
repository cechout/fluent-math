using System;
using System.Collections.Generic;
using System.Linq;
using FluentMath.Models;
using FluentMath.Models.Converters;
using Xunit;

namespace FluentMath.Tests
{
    // what the converters convert with: the length and volume tables and the currency rates
    public class UnitSourceTests
    {
        public static TheoryData<string> Tables => new TheoryData<string> { "Length", "Volume" };

        private static LinearUnitSource Table(string key) => key == "Length" ? LengthUnits.Create() : VolumeUnits.Create();

        private static void Close(double expected, double? actual)
        {
            Assert.NotNull(actual);
            Assert.True(Math.Abs(expected - actual!.Value) <= Math.Abs(expected) * 1e-12, $"{actual} is not {expected}");
        }


        // === tables ===

        [Theory]
        [MemberData(nameof(Tables))]
        public void EveryUnitRoundTripsThroughEveryOther(string key)
        {
            LinearUnitSource source = Table(key);

            foreach (UnitInfo from in source.Units)
            {
                foreach (UnitInfo to in source.Units)
                {
                    double? there = source.Convert(from.Id, to.Id, 7);
                    Close(7, source.Convert(to.Id, from.Id, there!.Value));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Tables))]
        public void IdsAndSymbolsAreUniqueAndTheDefaultsAreAmongThem(string key)
        {
            LinearUnitSource source = Table(key);

            Assert.Equal(source.Units.Count, source.Units.Select(u => u.Id).Distinct().Count());
            Assert.Equal(source.Units.Count, source.Units.Select(u => u.Symbol).Distinct().Count());
            Assert.Contains(source.Units, u => u.Id == source.DefaultFrom);
            Assert.Contains(source.Units, u => u.Id == source.DefaultTo);
        }

        [Theory]
        [InlineData("ft", "in", 12)]
        [InlineData("mi", "ft", 5280)]
        [InlineData("yd", "ft", 3)]
        [InlineData("km", "m", 1000)]
        [InlineData("nmi", "m", 1852)]
        [InlineData("m", "um", 1e6)]
        public void LengthsAgreeWithTheirDefinitions(string from, string to, double expected)
        {
            Close(expected, LengthUnits.Create().Convert(from, to, 1));
        }

        [Theory]
        [InlineData("gal_us", "in3", 231)]
        [InlineData("gal_us", "qt_us", 4)]
        [InlineData("qt_us", "pt_us", 2)]
        [InlineData("pt_us", "cup_us", 2)]
        [InlineData("cup_us", "floz_us", 8)]
        [InlineData("floz_us", "tbsp_us", 2)]
        [InlineData("tbsp_us", "tsp_us", 3)]
        [InlineData("gal_uk", "floz_uk", 160)]
        [InlineData("gal_uk", "qt_uk", 4)]
        [InlineData("pt_uk", "floz_uk", 20)]
        [InlineData("tbsp_uk", "tsp_uk", 3)]
        [InlineData("ft3", "in3", 1728)]
        [InlineData("yd3", "ft3", 27)]
        [InlineData("m3", "l", 1000)]
        [InlineData("l", "cm3", 1000)]
        public void VolumesAgreeWithTheirDefinitions(string from, string to, double expected)
        {
            Close(expected, VolumeUnits.Create().Convert(from, to, 1));
        }

        [Fact]
        public void AnUnknownIdConvertsToNothing()
        {
            Assert.Null(LengthUnits.Create().Convert("m", "parsec", 1));
        }


        // === currency ===

        private static RateTable Rates() => new RateTable
        {
            Date = new DateOnly(2026, 10, 2),
            Rates = new Dictionary<string, double> { ["EUR"] = 1, ["USD"] = 1.25, ["JPY"] = 160 }
        };

        [Fact]
        public void CurrenciesConvertThroughTheEuro()
        {
            var source = new CurrencyUnitSource(Rates);

            Close(12.5, source.Convert("EUR", "USD", 10));
            Close(1280, source.Convert("USD", "JPY", 10));
        }

        [Fact]
        public void TheUnitsAreWhateverTheRatesHold()
        {
            var source = new CurrencyUnitSource(Rates);

            Assert.Equal(new[] { "EUR", "USD", "JPY" }, source.Units.Select(u => u.Id));
            Assert.All(source.Units, u => Assert.Equal(u.Id, u.Symbol));
            Assert.NotEmpty(source.DateText);
        }

        [Fact]
        public void WithoutRatesThereIsNothingToConvert()
        {
            var source = new CurrencyUnitSource(() => null);

            Assert.Empty(source.Units);
            Assert.Equal("", source.DateText);
            Assert.Null(source.Convert("EUR", "USD", 1));
        }

        [Fact]
        public void ARefreshAsksForTheRatesAgain()
        {
            int calls = 0;
            var source = new CurrencyUnitSource(() => { calls++; return calls == 1 ? null : Rates(); });

            Assert.Empty(source.Units);

            source.Refresh();

            Assert.Equal(2, calls);
            Assert.Equal(3, source.Units.Count);
        }
    }
}
