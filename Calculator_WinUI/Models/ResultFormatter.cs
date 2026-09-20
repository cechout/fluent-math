using System;
using System.Globalization;

namespace Calculator_WinUI.Models
{
    // turns an evaluated double into something the display can show
    //
    // the rounding is not cosmetic; a double cannot hold 0.1 + 0.2 exactly, so without cutting the
    // result back to the digits a calculator claims to have, every second sum ends in a tail of noise
    // twelve significant digits is roughly what the Casio the app is modelled on shows
    //
    // everything here formats with InvariantCulture on purpose, a German system would otherwise put a
    // comma into a number that then no longer parses back
    public static class ResultFormatter
    {
        // === constants ===

        private const int SignificantDigits = 12;

        // outside this window a plain decimal is a wall of zeros, so the output switches to a power of ten
        private const double ScientificUpperBound = 1e12;
        private const double ScientificLowerBound = 1e-9;

        // enough placeholders to spell out the smallest number that still avoids scientific notation
        private const string PlainNumberFormat = "0.####################";

        // Math.Round refuses more than 15 decimals, and past that there is nothing left to round anyway
        private const int MaxRoundingDecimals = 15;


        // === public formatting ===

        public static string ToLatex(double value)
        {
            double absolute = Math.Abs(value);
            if (value != 0 && (absolute >= ScientificUpperBound || absolute < ScientificLowerBound))
            {
                return ToScientificLatex(value);
            }

            return ToPlainString(value);
        }

        // the same rounding as ToLatex but always as plain digits, without the switch to scientific
        // notation ToLatex makes outside its window
        public static string ToPlainString(double value)
        {
            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            if (rounded == 0) return "0"; // catches negative zero, which would otherwise print as -0

            return rounded.ToString(PlainNumberFormat, CultureInfo.InvariantCulture);
        }

        // the wording is the one a Casio uses, short enough to still fit the display at full size
        public static string ErrorToLatex(EvaluationError error)
        {
            string text;
            if (error == EvaluationError.Syntax) { text = "Syntax ERROR"; }
            else { text = "Math ERROR"; }

            return $"\\text{{{text}}}";
        }


        // === helpers ===

        private static string ToScientificLatex(double value)
        {
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            double mantissa = RoundToSignificantDigits(value / Math.Pow(10, exponent), SignificantDigits);

            // rounding can carry the mantissa up to exactly 10, e.g. 9.9999999999999e5
            if (Math.Abs(mantissa) >= 10)
            {
                mantissa /= 10;
                exponent++;
            }

            // the times sign goes through the same helper the input line uses, so a result is not spaced
            // differently from the formula that produced it
            string mantissaText = mantissa.ToString(PlainNumberFormat, CultureInfo.InvariantCulture);
            return $"{mantissaText}{LatexHelper.TaggedOperator("\\times")}10^{{{exponent}}}";
        }

        private static double RoundToSignificantDigits(double value, int digits)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value)) return value;

            int magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            int decimals = digits - 1 - magnitude;

            // a number far above the significant digits gets rounded on the other side of the point,
            // which Math.Round cannot express, so it is scaled down and back up instead
            if (decimals < 0)
            {
                double scale = Math.Pow(10, -decimals);
                return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
            }

            if (decimals > MaxRoundingDecimals) decimals = MaxRoundingDecimals;
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}
