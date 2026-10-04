using System;
using System.Globalization;

namespace FluentMath.Models.Converters
{
    // the numbers a converter shows; written with a dot, the way the keypad types them
    public static class UnitFormat
    {
        // --- rounding ---
        private const int AmountDecimals = 2;
        private const int RateDecimals = 4; // the rate line; (more than an amount)

        public static string Amount(double value)
        {
            return Math.Round(value, AmountDecimals).ToString(CultureInfo.InvariantCulture);
        }

        public static string Rate(double value)
        {
            return Math.Round(value, RateDecimals).ToString(CultureInfo.InvariantCulture);
        }
    }
}
