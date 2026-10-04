using System;
using System.IO;
using FluentMath.Models;
using FluentMath.Models.Converters;
using FluentMath.Persistence.Services;
using FluentMath.ViewModels;
using Xunit;

namespace FluentMath.Tests
{
    // the folder the page state of these tests is saved to, instead of the one of the installed app
    public sealed class ConverterStateFolder : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "FluentMathTests", Guid.NewGuid().ToString("N"));

        public ConverterStateFolder()
        {
            PersistenceService.Initialize(_folder);
        }

        public void Dispose()
        {
            PersistenceService.Instance.FlushAll();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }
    }


    // the converter keypad: either line takes the input, every key converts, the pair is kept per converter
    // (PageStateService is one for the process, so every test brings a converter key of its own)
    public class ConverterViewModelTests : IClassFixture<ConverterStateFolder>
    {
        // === helpers ===

        // a: 1, b: 10, c: 100 of a base unit
        private static LinearUnitSource Source(string key) => new LinearUnitSource(key, "a", "b", new[]
        {
            ("a", "a", "A", 1.0),
            ("b", "b", "B", 10.0),
            ("c", "c", "C", 100.0)
        });

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static ConverterViewModel ViewModel(IUnitSource? source = null, CalculatorSettings? numbers = null,
            ConverterSettings? precision = null)
        {
            return new ConverterViewModel(source ?? Source(NewKey()),
                numbers ?? new CalculatorSettings { DecimalMark = DecimalMark.Dot },
                precision ?? new ConverterSettings());
        }

        // one character per key, like the keypad
        private static void Type(ConverterViewModel viewModel, string keys)
        {
            foreach (char key in keys)
            {
                if (key == '<') viewModel.BackspaceCommand.Execute(null);
                else viewModel.InputCommand.Execute(key.ToString());
            }
        }

        private static UnitInfo Unit(ConverterViewModel viewModel, string id)
        {
            foreach (UnitInfo unit in viewModel.Units)
                if (unit.Id == id) return unit;

            throw new ArgumentException(id);
        }


        // === input ===

        [Fact]
        public void EveryKeyConvertsAtOnce()
        {
            ConverterViewModel viewModel = ViewModel();

            Type(viewModel, "5");
            Assert.Equal("5", viewModel.TopText);
            Assert.Equal("0.5", viewModel.BottomText);

            Type(viewModel, "0");
            Assert.Equal("5", viewModel.BottomText);
        }

        [Fact]
        public void ASecondDecimalPointIsIgnored()
        {
            ConverterViewModel viewModel = ViewModel();

            Type(viewModel, "1.2.3");

            Assert.Equal("1.23", viewModel.TopText);
        }

        [Fact]
        public void BackspaceAndClearConvertToo()
        {
            ConverterViewModel viewModel = ViewModel();

            Type(viewModel, "25<");
            Assert.Equal("0.2", viewModel.BottomText);

            viewModel.ClearCommand.Execute(null);
            Assert.Equal("0", viewModel.TopText);
            Assert.Equal("0", viewModel.BottomText);
        }


        // === active line ===

        [Fact]
        public void TheOtherLineTakesOverWithTheValueItShows()
        {
            ConverterViewModel viewModel = ViewModel();
            Type(viewModel, "30");

            viewModel.ActivateLine(top: false);

            Assert.True(viewModel.IsBottomActive);
            Assert.Equal("3", viewModel.BottomText);
            Assert.Equal("30", viewModel.TopText);
        }

        [Fact]
        public void TheFirstKeyAfterTheSwitchReplacesTheValue()
        {
            ConverterViewModel viewModel = ViewModel();
            Type(viewModel, "30");
            viewModel.ActivateLine(top: false);

            Type(viewModel, "7");

            Assert.Equal("7", viewModel.BottomText);
            Assert.Equal("70", viewModel.TopText);
        }

        [Fact]
        public void BackspaceAfterTheSwitchEditsTheValue()
        {
            ConverterViewModel viewModel = ViewModel();
            Type(viewModel, "125");
            viewModel.ActivateLine(top: false);

            Type(viewModel, "<");
            Assert.Equal("12.", viewModel.BottomText);

            Type(viewModel, "<");
            Assert.Equal("12", viewModel.BottomText);
            Assert.Equal("120", viewModel.TopText);
        }

        [Fact]
        public void TappingTheActiveLineChangesNothing()
        {
            ConverterViewModel viewModel = ViewModel();
            Type(viewModel, "4");

            viewModel.ActivateLine(top: true);
            Type(viewModel, "2");

            Assert.Equal("42", viewModel.TopText);
        }


        // === pickers ===

        [Fact]
        public void APickerChangeKeepsTheActiveNumberAndRecomputesTheOther()
        {
            ConverterViewModel viewModel = ViewModel();
            viewModel.ActivateLine(top: false);
            Type(viewModel, "2");

            viewModel.BottomUnit = Unit(viewModel, "c");
            Assert.Equal("2", viewModel.BottomText);
            Assert.Equal("200", viewModel.TopText);

            viewModel.TopUnit = Unit(viewModel, "b");
            Assert.Equal("2", viewModel.BottomText);
            Assert.Equal("20", viewModel.TopText);
        }

        [Fact]
        public void TheRateLineFollowsThePickers()
        {
            ConverterViewModel viewModel = ViewModel();
            Assert.Equal("1 a = 0.1 b", viewModel.RateText);

            viewModel.TopUnit = Unit(viewModel, "c");
            Assert.Equal("1 c = 10 b", viewModel.RateText);
        }

        [Fact]
        public void AConverterWithoutARefreshSaysSo()
        {
            ConverterViewModel viewModel = ViewModel();

            Assert.False(viewModel.CanRefresh);
            Assert.Equal("", viewModel.DateText);
        }


        // === saved pair ===

        [Fact]
        public void ThePairIsKeptPerConverter()
        {
            string key = NewKey();
            ConverterViewModel first = ViewModel(Source(key));
            first.TopUnit = Unit(first, "c");
            first.BottomUnit = Unit(first, "a");

            ConverterViewModel again = ViewModel(Source(key));
            ConverterViewModel other = ViewModel(Source(NewKey()));

            Assert.Equal("c", again.TopUnit!.Id);
            Assert.Equal("a", again.BottomUnit!.Id);
            Assert.Equal("a", other.TopUnit!.Id);
            Assert.Equal("b", other.BottomUnit!.Id);
        }

        [Fact]
        public void ASavedIdTheUnitsLackFallsBackToTheDefault()
        {
            string key = NewKey();
            PageStateService.Instance.SetConverterPair(key, "gone", "c");

            ConverterViewModel viewModel = ViewModel(Source(key));

            Assert.Equal("a", viewModel.TopUnit!.Id);
            Assert.Equal("c", viewModel.BottomUnit!.Id);
        }


        // === settings ===

        [Fact]
        public void ASettingsChangeRedrawsWithoutTouchingTheInput()
        {
            var numbers = new CalculatorSettings { DecimalMark = DecimalMark.Dot };
            var precision = new ConverterSettings { UnitDigits = 10 };
            ConverterViewModel viewModel = ViewModel(numbers: numbers, precision: precision);
            viewModel.BottomUnit = Unit(viewModel, "c");
            Type(viewModel, "1234.5");

            numbers.DecimalMark = DecimalMark.Comma;
            numbers.GroupDigits = true;

            Assert.Equal(",", viewModel.DecimalMarkLabel);
            Assert.Equal("1 234,5", viewModel.TopText);
            Assert.Equal("12,345", viewModel.BottomText);

            precision.UnitDigits = 6;
            Type(viewModel, "67");

            Assert.Equal("12,3457", viewModel.BottomText);
        }

        [Fact]
        public void MoneyRoundsToTheCurrencyDecimals()
        {
            var rates = new RateTable { Rates = { ["EUR"] = 1, ["USD"] = 1.08427 } };
            var precision = new ConverterSettings();
            ConverterViewModel viewModel = ViewModel(new CurrencyUnitSource(() => rates), precision: precision);

            Type(viewModel, "10");

            Assert.Equal("10.84", viewModel.BottomText);
            Assert.Equal("1 EUR = 1.0843 USD", viewModel.RateText);

            precision.CurrencyDecimals = 0;
            Assert.Equal("11", viewModel.BottomText);
        }

        [Fact]
        public void WithoutRatesTheRateLineSaysSo()
        {
            ConverterViewModel viewModel = ViewModel(new CurrencyUnitSource(() => null));

            Type(viewModel, "5");

            Assert.Equal("No rates available", viewModel.RateText);
            Assert.Equal("0", viewModel.BottomText);
        }
    }
}
