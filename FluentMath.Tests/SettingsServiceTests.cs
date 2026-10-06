using FluentMath.Models;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;
using Xunit;

namespace FluentMath.Tests
{
    // the live settings against the file: what LoadFromData takes over and what it refuses
    // (only LoadFromData and ToData, which never save; nothing here has a folder to save to)
    public class SettingsServiceTests
    {
        private static SettingsService Service => SettingsService.Instance;

        [Fact]
        public void TheDefaultsOfTheFileAreThoseOfAFreshSetup()
        {
            var data = new AppSettingsData();
            var setup = new CalculatorSettings();

            Assert.Equal(setup.AngleMode, data.AngleMode);
            Assert.Equal(setup.ExactFirst, data.ExactFirst);
            Assert.Equal(setup.MixedFirst, data.MixedFirst);
            Assert.Equal(setup.RecurringDecimals, data.RecurringDecimals);
            Assert.Equal(setup.NumberFormat.Notation, data.NumberNotation);
            Assert.Equal(setup.NumberFormat.Digits, data.NumberDigits);
            Assert.Equal(setup.UsePrefixes, data.UsePrefixes);
            Assert.Equal(setup.GroupDigits, data.GroupDigits);
            Assert.Equal(setup.DecimalMark, data.DecimalMark);
        }

        [Fact]
        public void ALoadedFileReachesTheCalculatorSetupAndComesBackUnchanged()
        {
            var data = new AppSettingsData
            {
                AppTheme = "Light",
                StartupPage = StartupPage.Scientific,
                AngleMode = AngleMode.Radians,
                ExactFirst = false,
                MixedFirst = true,
                RecurringDecimals = false,
                NumberNotation = NumberNotation.Sci,
                NumberDigits = 6,
                UsePrefixes = true,
                GroupDigits = true,
                DecimalMark = DecimalMark.Dot
            };

            Service.LoadFromData(data);

            Assert.Equal("Light", Service.AppTheme);
            Assert.Equal(StartupPage.Scientific, Service.StartupPage);
            Assert.Equal(AngleMode.Radians, Service.Calculator.AngleMode);
            Assert.Equal(NumberNotation.Sci, Service.Calculator.NumberFormat.Notation);
            Assert.Equal(6, Service.Calculator.NumberFormat.Digits);
            Assert.Equal(DecimalMark.Dot, Service.Calculator.DecimalMark);

            AppSettingsData back = Service.ToData();

            Assert.Equal(data.AppTheme, back.AppTheme);
            Assert.Equal(data.StartupPage, back.StartupPage);
            Assert.Equal(data.AngleMode, back.AngleMode);
            Assert.Equal(data.ExactFirst, back.ExactFirst);
            Assert.Equal(data.MixedFirst, back.MixedFirst);
            Assert.Equal(data.RecurringDecimals, back.RecurringDecimals);
            Assert.Equal(data.NumberNotation, back.NumberNotation);
            Assert.Equal(data.NumberDigits, back.NumberDigits);
            Assert.Equal(data.UsePrefixes, back.UsePrefixes);
            Assert.Equal(data.GroupDigits, back.GroupDigits);
            Assert.Equal(data.DecimalMark, back.DecimalMark);
        }

        [Theory]
        [InlineData(42, 9)]
        [InlineData(-3, 0)]
        public void ADigitCountOutOfRangeIsClampedToIt(int saved, int expected)
        {
            Service.LoadFromData(new AppSettingsData { NumberNotation = NumberNotation.Fix, NumberDigits = saved });

            Assert.Equal(expected, Service.Calculator.NumberFormat.Digits);
        }

        [Fact]
        public void AValueNoEnumMemberStandsForFallsBackToItsDefault()
        {
            var defaults = new AppSettingsData();

            Service.LoadFromData(new AppSettingsData
            {
                StartupPage = (StartupPage)9,
                AngleMode = (AngleMode)7,
                NumberNotation = (NumberNotation)12,
                DecimalMark = (DecimalMark)5
            });

            Assert.Equal(defaults.StartupPage, Service.StartupPage);
            Assert.Equal(defaults.AngleMode, Service.Calculator.AngleMode);
            Assert.Equal(defaults.NumberNotation, Service.Calculator.NumberFormat.Notation);
            Assert.Equal(defaults.DecimalMark, Service.Calculator.DecimalMark);
        }

        [Theory]
        [InlineData("Purple")]
        [InlineData("")]
        [InlineData(null)]
        public void AnUnknownThemeFallsBackToTheDefault(string? theme)
        {
            Service.LoadFromData(new AppSettingsData { AppTheme = theme! });

            Assert.Equal("Default", Service.AppTheme);
        }

        [Fact]
        public void TheUpdateSettingsComeBackUnchanged()
        {
            Service.LoadFromData(new AppSettingsData { CheckUpdatesOnStartup = false, SkippedUpdateVersion = "2.5.0" });

            Assert.False(Service.CheckUpdatesOnStartup);
            Assert.Equal("2.5.0", Service.SkippedUpdateVersion);

            AppSettingsData back = Service.ToData();

            Assert.False(back.CheckUpdatesOnStartup);
            Assert.Equal("2.5.0", back.SkippedUpdateVersion);
        }

        // a file written before the update check, or edited by hand, may carry a null
        [Fact]
        public void AMissingSkippedVersionMeansNone()
        {
            Service.LoadFromData(new AppSettingsData { SkippedUpdateVersion = null! });

            Assert.Equal("", Service.SkippedUpdateVersion);
            Assert.True(new AppSettingsData().CheckUpdatesOnStartup);
        }
    }
}
