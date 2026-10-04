using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                ScientificDisplayHeight = 212.5
            };
            saved.ConverterPairs["Currency"] = new UnitPair { From = "CHF", To = "JPY" };
            saved.CompactSizes["ScientificPage"] = new CompactSize { Width = 350, Height = 530 };

            var writer = new PersistenceService(_folder);
            writer.SavePageStateDebounced(saved);
            writer.FlushAll();

            PageStateData loaded = new PersistenceService(_folder).LoadPageState();

            Assert.Equal(212.5, loaded.ScientificDisplayHeight);
            Assert.Equal("CHF", loaded.ConverterPairs["Currency"].From);
            Assert.Equal("JPY", loaded.ConverterPairs["Currency"].To);
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
            Assert.Empty(service.LoadPageState().ConverterPairs);
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


        // === rates ===

        [Fact]
        public void TheRatesComeBackWithTheirDayAndAreWrittenRightAway()
        {
            var rates = new RateTable { Date = new DateOnly(2026, 9, 30) };
            rates.Rates["EUR"] = 1;
            rates.Rates["JPY"] = 162.31;

            new PersistenceService(_folder).SaveRates(rates);
            RateTable? loaded = new PersistenceService(_folder).LoadRates();

            Assert.NotNull(loaded);
            Assert.Equal(new DateOnly(2026, 9, 30), loaded!.Date);
            Assert.Equal(162.31, loaded.Rates["JPY"]);
        }

        [Theory]
        [InlineData("{ \"Rates\": {} }")]
        [InlineData("{ \"Rates\": null }")]
        public void ATableWithoutRatesCountsAsNone(string json)
        {
            WriteRaw(PersistenceService.RatesFileName, json);

            Assert.Null(new PersistenceService(_folder).LoadRates());
        }

        [Fact]
        public void NoRatesOnDiskGiveNone()
        {
            Assert.Null(new PersistenceService(_folder).LoadRates());
        }


        // === reset ===

        private PersistenceService ServiceWithAllThreeFiles()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Dark" });
            service.SaveWindowStatesDebounced(new Dictionary<string, WindowState> { ["Main"] = new WindowState { Width = 400 } });
            service.SavePageStateDebounced(new PageStateData { ScientificDisplayHeight = 100 });
            service.FlushAll();
            return service;
        }

        [Fact]
        public void ResettingTheSettingsLeavesTheWindowAndPageStatesAlone()
        {
            ServiceWithAllThreeFiles().ResetSettings();

            Assert.False(File.Exists(FilePath(PersistenceService.SettingsFileName)));
            Assert.True(File.Exists(FilePath(PersistenceService.WindowStateFileName)));
            Assert.True(File.Exists(FilePath(PersistenceService.PageStateFileName)));
        }

        [Fact]
        public void ResettingTheWindowAndPageStatesLeavesTheSettingsAlone()
        {
            ServiceWithAllThreeFiles().ResetWindowAndPageStates();

            Assert.True(File.Exists(FilePath(PersistenceService.SettingsFileName)));
            Assert.False(File.Exists(FilePath(PersistenceService.WindowStateFileName)));
            Assert.False(File.Exists(FilePath(PersistenceService.PageStateFileName)));
        }

        [Fact]
        public void AResetWritesWhatTheOtherGroupStillHadPending()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Dark" });
            service.SavePageStateDebounced(new PageStateData { ScientificDisplayHeight = 100 });

            service.ResetWindowAndPageStates();

            Assert.Equal("Dark", new PersistenceService(_folder).LoadSettings().AppTheme);
            Assert.False(File.Exists(FilePath(PersistenceService.PageStateFileName)));
        }

        [Fact]
        public void NothingIsWrittenAfterAReset()
        {
            PersistenceService service = ServiceWithAllThreeFiles();
            service.ResetAll();

            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Light" });
            service.SaveWindowStatesDebounced(new Dictionary<string, WindowState>());
            service.FlushAll();

            Assert.Empty(Directory.GetFiles(_folder, "*.json"));
        }


        // === backup ===

        private string ZipPath => Path.Combine(_folder, "backup.zip");

        [Fact]
        public void AnExportedBackupImportsBackOverChangedState()
        {
            ServiceWithAllThreeFiles().ExportBackup(ZipPath);

            var changed = new PersistenceService(_folder);
            changed.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Light" });
            changed.SavePageStateDebounced(new PageStateData { ScientificDisplayHeight = 200 });
            changed.FlushAll();

            Assert.True(new PersistenceService(_folder).ImportBackup(ZipPath));

            var loaded = new PersistenceService(_folder);
            Assert.Equal("Dark", loaded.LoadSettings().AppTheme);
            Assert.Equal(400, loaded.LoadWindowStates()["Main"].Width);
            Assert.Equal(100, loaded.LoadPageState().ScientificDisplayHeight);
        }

        [Fact]
        public void AnExportFlushesWhatIsStillPending()
        {
            var service = new PersistenceService(_folder);
            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Dark" });
            service.ExportBackup(ZipPath);

            Assert.True(new PersistenceService(_folder).ImportBackup(ZipPath));
            Assert.Equal("Dark", new PersistenceService(_folder).LoadSettings().AppTheme);
        }

        [Fact]
        public void AFileTheBackupLacksComesBackOnItsDefaults()
        {
            var early = new PersistenceService(_folder);
            early.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Dark" });
            early.ExportBackup(ZipPath);

            var later = new PersistenceService(_folder);
            later.SavePageStateDebounced(new PageStateData { ScientificDisplayHeight = 200 });
            later.FlushAll();

            Assert.True(new PersistenceService(_folder).ImportBackup(ZipPath));
            Assert.False(File.Exists(FilePath(PersistenceService.PageStateFileName)));
        }

        [Fact]
        public void NothingIsWrittenAfterAnImport()
        {
            ServiceWithAllThreeFiles().ExportBackup(ZipPath);

            var service = new PersistenceService(_folder);
            Assert.True(service.ImportBackup(ZipPath));

            service.SaveSettingsDebounced(new AppSettingsData { AppTheme = "Light" });
            service.FlushAll();

            Assert.Equal("Dark", new PersistenceService(_folder).LoadSettings().AppTheme);
        }

        [Theory]
        [InlineData("notes.txt", "hello")] // a file no backup has
        [InlineData(PersistenceService.SettingsFileName, "{ not json")]
        [InlineData(PersistenceService.SettingsFileName, "{ \"AngleMode\": \"Turns\" }")]
        public void ABackupWithAnUnknownOrBrokenEntryChangesNothing(string entryName, string content)
        {
            ServiceWithAllThreeFiles();
            using (ZipArchive zip = ZipFile.Open(ZipPath, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
                writer.Write(content);
            }

            var service = new PersistenceService(_folder);
            Assert.False(service.ImportBackup(ZipPath));

            Assert.Equal("Dark", service.LoadSettings().AppTheme);
            Assert.Equal(100, service.LoadPageState().ScientificDisplayHeight);
        }

        [Fact]
        public void TheRatesAreKeptButNeitherBackedUpNorReset()
        {
            PersistenceService service = ServiceWithAllThreeFiles();
            service.SaveRates(new RateTable { Rates = { ["EUR"] = 1, ["USD"] = 1.0842 } });

            service.ExportBackup(ZipPath);
            using (ZipArchive zip = ZipFile.OpenRead(ZipPath))
            {
                Assert.DoesNotContain(zip.Entries, entry => entry.FullName == PersistenceService.RatesFileName);
            }

            service.ResetAll();

            Assert.Equal(1.0842, new PersistenceService(_folder).LoadRates()!.Rates["USD"]);
        }

        [Fact]
        public void AFileThatIsNotAZipChangesNothing()
        {
            ServiceWithAllThreeFiles();
            File.WriteAllText(ZipPath, "not a zip");

            var service = new PersistenceService(_folder);
            Assert.False(service.ImportBackup(ZipPath));

            Assert.Equal("Dark", service.LoadSettings().AppTheme);
        }
    }
}
