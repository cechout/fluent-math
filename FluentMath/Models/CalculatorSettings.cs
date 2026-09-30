using System.Globalization;

namespace FluentMath.Models
{
    // the number format of a Casio setup:
    // - Norm 1 and 2 write up to twelve significant digits, with a power of ten from 1e12 up and below 0.01
    //   or 1e-9;
    // - Fix a fixed number of decimals;
    // - Sci a fixed number of significant digits over a power of ten
    public enum NumberNotation
    {
        Norm1,
        Norm2,
        Fix,
        Sci
    }

    public readonly struct NumberFormat
    {
        public NumberNotation Notation { get; }

        // the decimals of Fix or significant digits of Sci, 0 to 9; (Sci 0 is every digit, as on a Casio)
        public int Digits { get; }

        public NumberFormat(NumberNotation notation, int digits = 0)
        {
            Notation = notation;
            Digits = digits;
        }

        public static NumberFormat Default => new NumberFormat(NumberNotation.Norm2);
    }

    public enum DecimalMark
    {
        Region, // whatever the Windows region format uses
        Dot,
        Comma
    }


    // the calculator setup, one object for the whole app, since every page has its own ViewModel
    // (not persisted across a restart yet)
    public sealed class CalculatorSettings
    {
        // the unit the caret bar selector and the settings page both edit
        public AngleMode AngleMode { get; set; } = AngleMode.Degrees;

        // a result opens as its fraction when it has one, as in MathI/MathO on a Casio; else as its decimal
        public bool ExactFirst { get; set; } = true;

        // which of the two fraction forms comes first
        public bool MixedFirst { get; set; }

        // S⇔D passes a recurring decimal on its way to the decimal (2.3 with a bar for 7/3), if the period fits
        public bool RecurringDecimals { get; set; } = true;

        public NumberFormat NumberFormat { get; set; } = NumberFormat.Default;

        // a result in the ENG view is written with its decimal prefix, 1.234k rather than 1.234×10³
        public bool UsePrefixes { get; set; }

        // a thin gap every three digits of a whole part, in the input line and in the result
        public bool GroupDigits { get; set; }

        public DecimalMark DecimalMark { get; set; } = DecimalMark.Region;

        // the mark the display draws; the region is asked each time, and anything but a comma is a dot
        public string DecimalMarkText => DecimalMark switch
        {
            DecimalMark.Comma => ",",
            DecimalMark.Dot => ".",
            _ => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? "," : "."
        };
    }
}
