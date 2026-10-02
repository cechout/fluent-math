using System;
using System.Collections.Generic;
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
        public const int MaxDigits = 9;

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
    // (every setter reports a change through Changed, which is what saves it)
    public sealed class CalculatorSettings
    {
        // raised once per value that actually changed
        public event Action? Changed;

        // the unit the caret bar selector and the settings page both edit
        private AngleMode _angleMode = AngleMode.Degrees;
        public AngleMode AngleMode { get => _angleMode; set => Set(ref _angleMode, value); }

        // a result opens as its fraction when it has one, as in MathI/MathO on a Casio; else as its decimal
        private bool _exactFirst = true;
        public bool ExactFirst { get => _exactFirst; set => Set(ref _exactFirst, value); }

        // which of the two fraction forms comes first
        private bool _mixedFirst;
        public bool MixedFirst { get => _mixedFirst; set => Set(ref _mixedFirst, value); }

        // S⇔D passes a recurring decimal on its way to the decimal (2.3 with a bar for 7/3), if the period fits
        private bool _recurringDecimals = true;
        public bool RecurringDecimals { get => _recurringDecimals; set => Set(ref _recurringDecimals, value); }

        private NumberFormat _numberFormat = NumberFormat.Default;
        public NumberFormat NumberFormat { get => _numberFormat; set => Set(ref _numberFormat, value); }

        // a result in the ENG view is written with its decimal prefix, 1.234k rather than 1.234×10³
        private bool _usePrefixes;
        public bool UsePrefixes { get => _usePrefixes; set => Set(ref _usePrefixes, value); }

        // a thin gap every three digits of a whole part, in the input line and in the result
        private bool _groupDigits;
        public bool GroupDigits { get => _groupDigits; set => Set(ref _groupDigits, value); }

        private DecimalMark _decimalMark = DecimalMark.Region;
        public DecimalMark DecimalMark { get => _decimalMark; set => Set(ref _decimalMark, value); }

        // the mark the display draws; the region is asked each time, and anything but a comma is a dot
        public string DecimalMarkText => DecimalMark switch
        {
            DecimalMark.Comma => ",",
            DecimalMark.Dot => ".",
            _ => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? "," : "."
        };

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;
            Changed?.Invoke();
        }
    }
}
