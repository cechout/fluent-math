using System;
using System.Collections.Generic;
using System.IO;
using FluentMath.Models;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;
using Xunit;

namespace FluentMath.Tests
{
    // the disk layer, each test in a folder of its own
    public class PersistenceServiceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "FluentMathTests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        private string FilePath(string name) => Path.Combine(_folder, name);

        private string QuarantineFolder => Path.Combine(_folder, "quarantine");

        private void WriteRaw(string name, string json)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath(name), json);
        }


        // === round trips ===

        [Fact]
        public void SettingsComeBackAsTheyWereSaved()
        {
            var saved = new AppSettingsData
            {
                AppTheme = "Dark",
                StartupPage = StartupPage.Currency,
                AngleMode = AngleMode.Gradians,
                ExactFirst = false,
                MixedFirst = true,
                RecurringDecimals = false,
                NumberNotation = NumberNotation.Fix,
                NumberDigits = 4,
                UsePrefixes = true,
                GroupDigits = true,
                DecimalMark = DecimalMark.Comma
            };

            var writer = new PersistenceService(_folder);
            writer.SaveSettingsDebounced(saved);
            writer.FlushAll();

            AppSettingsData loaded = new PersistenceService(_folder).LoadSettings();

            Assert.Equal("Dark", loaded.AppTheme);
            Assert.Equal(StartupPage.Currency, loaded.StartupPage);
            Assert.Equal(AngleMode.Gradians, loaded.AngleMode);
            Assert.False(loaded.ExactFirst);
            Assert.True(loaded.MixedFirst);
            Assert.False(loaded.RecurringDecimals);
            Assert.Equal(NumberNotation.Fix, loaded.NumberNotation);
            Assert.Equal(4, loaded.NumberDigits);
            Assert.True(loaded.UsePrefixes);
            Assert.True(loaded.GroupDigits);
            Assert.Equal(DecimalMark.Comma, loaded.DecimalMark);
        }

        [Fact]
        public void WindowStatesComeBackAsTheyWereSaved()
        {
            var saved = new Dictionary<string, WindowState>
            {
                ["Main"] = new WindowState { X = -1200, Y = 40, Width = 420, Height = 640, IsMaximized = true }
            };

            var writer = new PersistenceService(_folder);
            writer.SaveWindowStatesDebounced(saved);
            writer.FlushAll();

            WindowState loaded = new PersistenceService(_folder).LoadWindowStates()["Main"];

            Assert.Equal(-1200, loaded.X);
            Assert.Equal(40, loaded.Y);
            Assert.Equal(420, loaded.Width);
            Assert.Equal(640, loaded.Height);
            Assert.True(loaded.IsMaximized);
        }

        [Fact]
        public void PageStateComesBackAsItWasSaved()
        {
            var saved = new PageStateData
            {
                ScientificDisplayHeight = 212.5,
                CurrencyFrom = "CHF",
                CurrencyTo = "JPY"
            };
            saved.CompactSizes["ScientificPage"] = new CompactSize { Width = 350, Height = 530 };

            var writer = new PersistenceService(_folder);
            writer.SavePageStateDebounced(saved);
            writer.FlushAll();

            PageStateData loaded = new PersistenceService(_folder).LoadPageState();

            Assert.Equal(212.5, loaded.ScientificDisplayHeight);
            Assert.Equal("CHF", loaded.CurrencyFrom);
            Assert.Equal("JPY", loaded.CurrencyTo);
            Assert.Equal(350, loaded.CompactSizes["ScientificPage"].Width);
            Assert.Equal(530, loaded.CompactSizes["ScientificPage"].Height);
        }

        [Fact]
        public void EnumsAreWrittenByName()
        {
            var writer = new PersistenceService(_folder);
            writer.SaveSettingsDebounced(new AppSettingsData { AngleMode = AngleMode.Radians });
            writer.FlushAll();

            Assert.Contains("\"Radians\"", File.ReadAllText(FilePath(PersistenceService.SettingsFileName)));
        }


        // === defaults ===

        [Fact]
        public void AFolderWithNothingInItGivesTheDefaults()
        {
            var service = new PersistenceService(_folder);

            Assert.Equal("Default", service.LoadSettings().AppTheme);
            Assert.Empty(service.LoadWindowStates());
            Assert.Equal(PageStateData.DefaultCurrencyFrom, service.LoadPageState().CurrencyFrom);
            Assert.Null(service.LoadPageState().ScientificDisplayHeight);
        }

        [Fact]
        public void AKeyMissingFromTheFileKeepsItsDefault()
        {
            WriteRaw(PersistenceService.SettingsFileName, "{ \"AppTheme\": \"Light\" }");

            AppSettingsData loaded = new PersistenceService(_folder).LoadSettings();
            var defaults = new CalculatorSettings();

            Assert.Equal("Light", loaded.AppTheme);
            Assert.Equal(StartupPage.Standard, loaded.StartupPage);
            Assert.Equal(defaults.AngleMode, loaded.AngleMode);
            Assert.Equal(defaults.ExactFirst, loaded.ExactFirst);
            Assert.Equal(defaults.RecurringDecimals, loaded.RecurringDecimals);
            Assert.Equal(defaults.NumberFormat.Notation, loaded.NumberNotation);
        }


        // === broken files ===

        [Theory]
        [InlineData("{ this is not json")]
        [InlineData("{ \"AngleMode\": \"Turns\" }")] // an enum name no build knows
        [InlineData("{ \"NumberDigits\": \"four\" }")]
        public void ABrokenFileIsMovedToTheQuarantineAndLoadsAsDefaults(string json)
        {
            WriteRaw(PersistenceService.SettingsFileName, json);

            AppSettingsData loaded = new PersistenceService(_folder).LoadSettings();

            Assert.Equal(new AppSettingsData().AngleMode, loaded.AngleMode);
            Assert.False(File.Exists(FilePath(PersistenceService.SettingsFileName)));
            Assert.Single(Directory.GetFiles(QuarantineFolder));
        }

        [Fact]
        public void AQuarantinedFileIsKeptForAMonthAndThenRemoved()
        {
            Directory.CreateDirectory(QuarantineFolder);
            string old = Path.Combine(QuarantineFolder, "settings.json.corrupt-old");
            string recent = Path.Combine(QuarantineFolder, "settings.json.corrupt-recent");
            File.WriteAllText(old, "{");
            File.WriteAllText(recent, "{");
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-31));
            File.SetLastWriteTime(recent, DateTime.Now.AddDays(-29));

            _ = new PersistenceService(_folder);

            Assert.False(File.Exists(old));
            Assert.True(File.Exists(recent));
        }

        [Fact]
        public void TheRetentionCountsFromTheQuarantineNotFromTheLastWrite()
        {
            WriteRaw(PersistenceService.SettingsFileName, "{");
            File.SetLastWriteTime(FilePath(PersistenceService.SettingsFileName), DateTime.Now.AddDays(-90));

            new PersistenceService(_folder).LoadSettings();
            _ = new PersistenceService(_folder);

            Assert.Single(Directory.GetFiles(QuarantineFolder));
        }


        // === writing ===

        [Fact]
        public void ASaveWaitsForTheDebounceOrAFlush()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData());

            Assert.False(File.Exists(FilePath(PersistenceService.SettingsFileName)));

            service.FlushAll();

            Assert.True(File.Exists(FilePath(PersistenceService.SettingsFileName)));
        }

        [Fact]
        public void TheLastSaveBeforeAFlushIsTheOneOnDisk()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Light" });
            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Dark" });
            service.FlushAll();

            Assert.Equal("Dark", new PersistenceService(_folder).LoadSettings().AppTheme);
        }

        [Fact]
        public void AChangeAfterTheSaveDoesNotReachTheFile()
        {
            var data = new AppSettingsData { AppTheme = "Light" };

            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(data);
            data.AppTheme = "Dark";
            service.FlushAll();

            Assert.Equal("Light", new PersistenceService(_folder).LoadSettings().AppTheme);
        }

        [Fact]
        public void AFlushLeavesNoTempFileBehind()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData());
            service.SaveWindowStatesDebounced(new Dictionary<string, WindowState>());
            service.SavePageStateDebounced(new PageStateData());
            service.FlushAll();

            Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
            Assert.Equal(3, Directory.GetFiles(_folder, "*.json").Length);
        }
    }
}
