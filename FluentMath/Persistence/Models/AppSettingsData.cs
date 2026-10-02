using FluentMath.Models;

namespace FluentMath.Persistence.Models
{
    // the page the app opens on; the names are the navigation tags
    public enum StartupPage
    {
        Standard,
        Scientific,
        Currency
    }


    // the settings as saved to settings.json:
    // the initial values are the defaults, for a fresh install, a key an older file lacks and every reset
    //
    // the calculator defaults are read off a fresh CalculatorSettings, which the tests construct directly,
    // so each default is still written in one place
    public class AppSettingsData
    {
        private static readonly CalculatorSettings CalculatorDefaults = new CalculatorSettings();

        // --- general ---
        public string AppTheme { get; set; } = "Default"; // Default, Light or Dark
        public StartupPage StartupPage { get; set; } = StartupPage.Standard;

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
    }
}
