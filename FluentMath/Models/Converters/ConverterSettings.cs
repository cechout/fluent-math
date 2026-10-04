using System;
using System.Collections.Generic;

namespace FluentMath.Models.Converters
{
    // the converter setup, one object for the whole app; the decimal mark and the digit grouping are the
    // calculators, which the converters share
    // (every setter reports a change through Changed, which is what saves it)
    public sealed class ConverterSettings
    {
        // --- ranges ---
        public const int MinCurrencyDecimals = 0;
        public const int MaxCurrencyDecimals = 6;
        public const int MinUnitDigits = 6; // significant digits
        public const int MaxUnitDigits = 15;

        // raised once per value that actually changed
        public event Action? Changed;

        // a money amount to this many decimals, trailing zeros dropped
        private int _currencyDecimals = 2;
        public int CurrencyDecimals { get => _currencyDecimals; set => Set(ref _currencyDecimals, value); }

        // a measure to this many significant digits, trailing zeros dropped
        private int _unitDigits = 10;
        public int UnitDigits { get => _unitDigits; set => Set(ref _unitDigits, value); }

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;
            Changed?.Invoke();
        }
    }
}
