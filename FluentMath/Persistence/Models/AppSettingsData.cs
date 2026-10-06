using FluentMath.Models;
using FluentMath.Models.Converters;

namespace FluentMath.Persistence.Models
{
    // the page the app opens on; the names are the navigation tags
    public enum StartupPage
    {
        Standard,
        Scientific,
        Currency,
        Volume,
        Length
    }


    // the settings as saved to settings.json:
    // the initial values are the defaults, for a fresh install, a key an older file lacks and every reset
    //
    // the calculator defaults are read off a fresh CalculatorSettings, which the tests construct directly,
    // so each default is still written in one place; the converter defaults off a fresh ConverterSettings
    public class AppSettingsData
    {
        private static readonly CalculatorSettings CalculatorDefaults = new CalculatorSettings();
        private static readonly ConverterSettings ConverterDefaults = new ConverterSettings();

        // --- general ---
        public string AppTheme { get; set; } = "Default"; // Default, Light or Dark
        public StartupPage StartupPage { get; set; } = StartupPage.Standard;

        // --- updates ---
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public string SkippedUpdateVersion { get; set; } = ""; // e.g. "2.5.0", empty = none

        // --- calculator ---
        // see CalculatorSettings; (the number format is kept flat)
        public AngleMode AngleMode { get; set; } = CalculatorDefaults.AngleMode;
        public bool ExactFirst { get; set; } = CalculatorDefaults.ExactFirst;
        public bool MixedFirst { get; set; } = CalculatorDefaults.MixedFirst;
        public bool RecurringDecimals { get; set; } = CalculatorDefaults.RecurringDecimals;
        public NumberNotation NumberNotation { get; set; } = CalculatorDefaults.NumberFormat.Notation;
        public int NumberDigits { get; set; } = CalculatorDefaults.NumberFormat.Digits;
        public bool UsePrefixes { get; set; } = CalculatorDefaults.UsePrefixes;
        public bool GroupDigits { get; set; } = CalculatorDefaults.GroupDigits;
        public DecimalMark DecimalMark { get; set; } = CalculatorDefaults.DecimalMark;

        // --- converter ---
        // see ConverterSettings
        public int CurrencyDecimals { get; set; } = ConverterDefaults.CurrencyDecimals;
        public int UnitDigits { get; set; } = ConverterDefaults.UnitDigits;
    }
}
