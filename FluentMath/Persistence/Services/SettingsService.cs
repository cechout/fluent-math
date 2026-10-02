using System;
using FluentMath.Models;
using FluentMath.Persistence.Models;

namespace FluentMath.Persistence.Services
{
    // the live settings:
    // every change is saved through PersistenceService; LoadFromData fills them once at startup
    public class SettingsService
    {
        // === singleton instance ===

        // the defaults every field starts from; declared above the instance, whose field initializers read it
        private static readonly AppSettingsData Defaults = new AppSettingsData();

        public static SettingsService Instance { get; } = new SettingsService();


        // === fields ===

        // LoadFromData writes the calculator setup through its setters, which would each save
        private bool _isLoading;


        // === constructor ===

        private SettingsService()
        {
            Calculator.Changed += SaveDebounced;
        }


        // === public api ===

        // the calculator setup every page holds
        public CalculatorSettings Calculator { get; } = new CalculatorSettings();

        // Default, Light or Dark; MainWindow applies it
        private string _appTheme = Defaults.AppTheme;
        public string AppTheme
        {
            get => _appTheme;
            set
            {
                if (_appTheme == value) return;

                _appTheme = value;
                ThemeChanged?.Invoke(_appTheme);
                SaveDebounced();
            }
        }

        // read once at launch, so a change takes effect on the next start
        private StartupPage _startupPage = Defaults.StartupPage;
        public StartupPage StartupPage
        {
            get => _startupPage;
            set
            {
                if (_startupPage == value) return;

                _startupPage = value;
                SaveDebounced();
            }
        }


        // === persistence ===

        // fills every value without events and without a save, before any window exists
        // a value out of range, from a file edited by hand, falls back to its default
        public void LoadFromData(AppSettingsData data)
        {
            _isLoading = true;

            _appTheme = data.AppTheme is "Default" or "Light" or "Dark" ? data.AppTheme : Defaults.AppTheme;
            _startupPage = Defined(data.StartupPage, Defaults.StartupPage);

            Calculator.AngleMode = Defined(data.AngleMode, Defaults.AngleMode);
            Calculator.ExactFirst = data.ExactFirst;
            Calculator.MixedFirst = data.MixedFirst;
            Calculator.RecurringDecimals = data.RecurringDecimals;
            Calculator.NumberFormat = new NumberFormat(Defined(data.NumberNotation, Defaults.NumberNotation),
                Math.Clamp(data.NumberDigits, 0, NumberFormat.MaxDigits));
            Calculator.UsePrefixes = data.UsePrefixes;
            Calculator.GroupDigits = data.GroupDigits;
            Calculator.DecimalMark = Defined(data.DecimalMark, Defaults.DecimalMark);

            _isLoading = false;
        }

        // the live values as one plain object for the disk
        public AppSettingsData ToData()
        {
            return new AppSettingsData
            {
                AppTheme = _appTheme,
                StartupPage = _startupPage,

                AngleMode = Calculator.AngleMode,
                ExactFirst = Calculator.ExactFirst,
                MixedFirst = Calculator.MixedFirst,
                RecurringDecimals = Calculator.RecurringDecimals,
                NumberNotation = Calculator.NumberFormat.Notation,
                NumberDigits = Calculator.NumberFormat.Digits,
                UsePrefixes = Calculator.UsePrefixes,
                GroupDigits = Calculator.GroupDigits,
                DecimalMark = Calculator.DecimalMark
            };
        }

        private void SaveDebounced()
        {
            if (_isLoading) return;

            PersistenceService.Instance.SaveSettingsDebounced(ToData());
        }

        private static T Defined<T>(T value, T fallback) where T : struct, Enum
        {
            return Enum.IsDefined(value) ? value : fallback;
        }


        // === events ===

        public event Action<string>? ThemeChanged;
    }
}
