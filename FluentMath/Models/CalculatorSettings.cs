using System.Globalization;

namespace FluentMath.Models
{
    // how a decimal result is written, the number format of a Casio setup
    //
    // Norm 1 and Norm 2 write up to twelve significant digits and switch to a power of ten from 1e12 up,
    // and below 0.01 or 1e-9 respectively; Fix writes a fixed number of decimals, Sci a fixed number of
    // significant digits and always a power of ten
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

        // the decimals of Fix and the significant digits of Sci, 0 to 9; Sci 0 is every digit the display
        // has, the way Sci 0 is all ten on a Casio
        public int Digits { get; }

        public NumberFormat(NumberNotation notation, int digits = 0)
        {
            Notation = notation;
            Digits = digits;
        }

        // what the display did before there was a setting for it
        public static NumberFormat Default => new NumberFormat(NumberNotation.Norm2);
    }

    public enum DecimalMark
    {
        Region, // whatever the Windows region format uses
        Dot,
        Comma
    }


    // the calculator setup, one object for the whole app
    //
    // the standard page builds a new ViewModel on every navigation, so the settings live outside it and a
    // choice survives a trip to another page; nothing persists them across a restart yet
    public sealed class CalculatorSettings
    {
        // the unit the caret bar selector and the settings page both edit
        public AngleMode AngleMode { get; set; } = AngleMode.Degrees;

        // whether a result opens as its fraction when it has one, the way a Casio in MathI/MathO does, or
        // as its decimal
        public bool ExactFirst { get; set; } = true;

        // which of the two fraction forms comes first
        public bool MixedFirst { get; set; }

        // whether S to D shows a fraction as a recurring decimal on its way to the decimal, 2.3 with a bar
        // for 7/3, when the period fits
        public bool RecurringDecimals { get; set; } = true;

        public NumberFormat NumberFormat { get; set; } = NumberFormat.Default;

        // a result in the ENG view is written with its decimal prefix, 1.234k rather than 1.234×10³
        public bool UsePrefixes { get; set; }

        // a thin gap every three digits of a whole part, in the input line and in the result
        public bool GroupDigits { get; set; }

        public DecimalMark DecimalMark { get; set; } = DecimalMark.Region;

        // the mark the display draws; the region is asked each time rather than once, and anything it
        // answers that is not a comma draws as a dot
        public string DecimalMarkText => DecimalMark switch
        {
            DecimalMark.Comma => ",",
            DecimalMark.Dot => ".",
            _ => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? "," : "."
        };
    }
}
