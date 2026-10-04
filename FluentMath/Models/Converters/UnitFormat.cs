using System;
using System.Globalization;

namespace FluentMath.Models.Converters
{
    // the numbers a converter shows, in two steps:
    // Amount and Rate round a value to plain text with a dot, the way the keypad types it, so a line that
    // becomes active can be edited from there; Display then puts in the decimal mark and the grouping
    public static class UnitFormat
    {
        // --- power of ten ---
        // a measure outside this range is written as 1.5e-12
        private const double SmallestPlain = 1e-9;
        private const double LargestPlain = 1e15; // exclusive

        private const int ExtraRateDecimals = 2; // the currency rate line; (more than an amount)
        private const string GroupGap = " "; // thin space

        public static string Amount(double value, bool roundsToDecimals, ConverterSettings settings)
        {
            return roundsToDecimals
                ? ToDecimals(value, settings.CurrencyDecimals)
                : ToSignificant(value, settings.UnitDigits);
        }

        public static string Rate(double value, bool roundsToDecimals, ConverterSettings settings)
        {
            return roundsToDecimals
                ? ToDecimals(value, settings.CurrencyDecimals + ExtraRateDecimals)
                : ToSignificant(value, settings.UnitDigits);
        }

        // the decimal mark for the dot, and a gap every three digits of the whole part when grouping is on
        public static string Display(string plain, CalculatorSettings settings)
        {
            int end = plain.IndexOfAny(new[] { '.', 'e' });
            if (end < 0) end = plain.Length;

            int start = plain.StartsWith('-') ? 1 : 0;
            string whole = plain.Substring(start, end - start);

            if (settings.GroupDigits)
            {
                for (int i = whole.Length - 3; i > 0; i -= 3)
                    whole = whole.Insert(i, GroupGap);
            }

            return plain.Substring(0, start) + whole + plain.Substring(end).Replace(".", settings.DecimalMarkText);
        }

        private static string ToDecimals(double value, int decimals)
        {
            return value.ToString(Pattern(decimals), CultureInfo.InvariantCulture);
        }

        private static string ToSignificant(double value, int digits)
        {
            double size = Math.Abs(value);
            if (size == 0) return "0";

            if (size < SmallestPlain || size >= LargestPlain)
            {
                string mantissa = value.ToString("E" + (digits - 1), CultureInfo.InvariantCulture);
                int e = mantissa.IndexOf('E');
                string front = mantissa.Substring(0, e);
                if (front.Contains('.')) front = front.TrimEnd('0').TrimEnd('.');
                return front + "e" + int.Parse(mantissa.Substring(e + 1), CultureInfo.InvariantCulture);
            }

            // the decimals that leave the wanted significant digits; negative rounds whole tens away
            int decimals = digits - 1 - (int)Math.Floor(Math.Log10(size));
            if (decimals < 0)
            {
                double step = Math.Pow(10, -decimals);
                value = Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
            }

            return value.ToString(Pattern(Math.Max(decimals, 0)), CultureInfo.InvariantCulture);
        }

        // "0.##" for two decimals; rounds, writes no exponent and drops trailing zeros
        private static string Pattern(int decimals)
        {
            return decimals > 0 ? "0." + new string('#', decimals) : "0";
        }
    }
}
