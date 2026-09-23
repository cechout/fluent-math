using System;
using System.Collections.Generic;
using System.Globalization;

namespace FluentMath.Models
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

        // largest denominator a result may come back as
        //
        // this is what keeps an irrational out: with four digits to work with, the best fraction for a
        // root or a pi is still a good 1e-7 away from it and gets rejected below, while one further
        // digit of room already lets the square root of two through as 1217471/860882
        private const long MaxFractionDenominator = 10000;

        // ceiling on the whole part, so a value big enough to be read as a decimal anyway is not turned
        // into a fraction; it also keeps the expansion below inside a long
        private const long MaxFractionNumerator = 10000000000;

        // how close the fraction has to sit to count as the same number; the value has already been cut
        // to twelve significant digits, so anything further out than this is a different number
        private const double FractionTolerance = 1e-12;


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

        // the same value in the shape the S to D key currently has selected; a form this value does not
        // have falls back to the decimal rather than to nothing
        public static string ToLatex(double value, AnswerForm form, bool displayFractions)
        {
            if (form == AnswerForm.Decimal) return ToLatex(value);
            if (!TryToFraction(value, out long numerator, out long denominator)) return ToLatex(value);
            if (denominator <= 1) return ToLatex(value);

            string command = displayFractions ? "dfrac" : "frac";
            if (form == AnswerForm.Improper) return FractionLatex(command, numerator, denominator);

            long whole = numerator / denominator;
            if (whole == 0) return FractionLatex(command, numerator, denominator);

            // the sign rides on the whole part, so the remainder is always written positive
            long remainder = Math.Abs(numerator % denominator);
            return $"{whole.ToString(CultureInfo.InvariantCulture)}{FractionLatex(command, remainder, denominator)}";
        }

        private static string FractionLatex(string command, long numerator, long denominator)
        {
            string top = numerator.ToString(CultureInfo.InvariantCulture);
            string bottom = denominator.ToString(CultureInfo.InvariantCulture);

            return $"\\{command}{{{top}}}{{{bottom}}}";
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
        public static string ErrorToText(EvaluationError error)
        {
            if (error == EvaluationError.Syntax) return "Syntax ERROR";

            return "Math ERROR";
        }

        public static string ErrorToLatex(EvaluationError error)
        {
            return $"\\text{{{ErrorToText(error)}}}";
        }


        // === the same values as tokens ===

        // what the native display draws, mirroring ToLatex arm for arm rather than parsing what that
        // produces, so the two shapes cannot drift apart; a change to either belongs in both
        public static List<MathToken> ToTokens(double value, AnswerForm form, bool displayFractions)
        {
            if (form != AnswerForm.Decimal
                && TryToFraction(value, out long numerator, out long denominator)
                && denominator > 1)
            {
                long whole = form == AnswerForm.Mixed ? numerator / denominator : 0;
                if (whole == 0) return new List<MathToken> { FractionTokens(numerator, denominator) };

                // the sign rides on the whole part, so the remainder is always written positive
                List<MathToken> mixed = DigitTokens(whole.ToString(CultureInfo.InvariantCulture));
                mixed.Add(FractionTokens(Math.Abs(numerator % denominator), denominator));

                return mixed;
            }

            return ToTokens(value);
        }

        public static List<MathToken> ToTokens(double value)
        {
            double absolute = Math.Abs(value);
            if (value != 0 && (absolute >= ScientificUpperBound || absolute < ScientificLowerBound))
            {
                (string mantissa, int exponent) = SplitScientific(value);

                // spelled out the same way the EXP key spells it, so a result and a typed formula are
                // the same shape rather than two that happen to look alike
                List<MathToken> tokens = DigitTokens(mantissa);
                tokens.Add(new MathToken(TokenType.Operator, "*"));

                PowerToken power = new PowerToken();
                power.BaseTokens.AddRange(DigitTokens("10"));
                power.ExponentTokens.AddRange(DigitTokens(exponent.ToString(CultureInfo.InvariantCulture)));
                tokens.Add(power);

                return tokens;
            }

            return DigitTokens(ToPlainString(value));
        }

        private static FractionToken FractionTokens(long numerator, long denominator)
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.AddRange(DigitTokens(numerator.ToString(CultureInfo.InvariantCulture)));
            fraction.DenominatorTokens.AddRange(DigitTokens(denominator.ToString(CultureInfo.InvariantCulture)));

            return fraction;
        }

        // a leading minus goes in as part of the number rather than as an operator token: these tokens are
        // only ever drawn and never evaluated, and an operator would take the spacing that belongs between
        // two operands
        private static List<MathToken> DigitTokens(string text)
        {
            List<MathToken> tokens = new List<MathToken>();
            foreach (char character in text)
            {
                tokens.Add(new MathToken(TokenType.Number, character == '-' ? "−" : character.ToString()));
            }

            return tokens;
        }


        // === fractions ===

        // the simplest fraction that still hits the value, found by continued-fraction expansion, which
        // is what lets 0.333333333333 come back as a third
        //
        // this is numeric and nothing else: a result that came out of a root or a pi has no fraction to
        // find here, and the caller leaves it as a decimal
        public static bool TryToFraction(double value, out long numerator, out long denominator)
        {
            numerator = 0;
            denominator = 1;

            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            if (Math.Abs(rounded) >= MaxFractionNumerator) return false;

            long sign = rounded < 0 ? -1 : 1;
            double remaining = Math.Abs(rounded);

            // the expansion only ever needs the last two convergents, so that is all that is carried
            long previousNumerator = 1;
            long currentNumerator = (long)Math.Floor(remaining);
            long previousDenominator = 0;
            long currentDenominator = 1;

            for (int step = 0; step < 32; step++)
            {
                if (currentNumerator > MaxFractionNumerator) break;

                double fraction = remaining - Math.Floor(remaining);
                if (fraction < 1e-15) break; // the expansion has landed on a whole number, it is exact

                remaining = 1.0 / fraction;

                // a term past the cap can only ever produce a denominator past it as well, and checking
                // it here is also what keeps the multiplication below inside a long
                long term = (long)Math.Floor(remaining);
                if (term > MaxFractionDenominator) break;

                long nextNumerator = term * currentNumerator + previousNumerator;
                long nextDenominator = term * currentDenominator + previousDenominator;
                if (nextDenominator > MaxFractionDenominator) break;

                previousNumerator = currentNumerator;
                currentNumerator = nextNumerator;
                previousDenominator = currentDenominator;
                currentDenominator = nextDenominator;
            }

            if (currentDenominator <= 0) return false;

            double candidate = (double)currentNumerator / currentDenominator;
            double allowed = FractionTolerance * Math.Max(1, Math.Abs(rounded));
            if (Math.Abs(candidate - Math.Abs(rounded)) > allowed) return false;

            numerator = sign * currentNumerator;
            denominator = currentDenominator;
            return true;
        }

        // a whole number is already its own simplest form, so it counts as having no fraction to show
        public static bool HasFractionForm(double value)
        {
            if (!TryToFraction(value, out _, out long denominator)) return false;

            return denominator > 1;
        }

        // a mixed number needs a whole part to split off, so it only exists above one
        public static bool HasMixedForm(double value)
        {
            if (!TryToFraction(value, out long numerator, out long denominator)) return false;

            return denominator > 1 && Math.Abs(numerator) > denominator;
        }


        // === helpers ===

        private static string ToScientificLatex(double value)
        {
            (string mantissaText, int exponent) = SplitScientific(value);

            // the times sign goes through the same helper the input line uses, so a result is not spaced
            // differently from the formula that produced it
            return $"{mantissaText}{LatexHelper.TaggedOperator("\\times")}10^{{{exponent}}}";
        }

        // shared by both output shapes, so a rounding carry is handled in one place rather than two
        private static (string Mantissa, int Exponent) SplitScientific(double value)
        {
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            double mantissa = RoundToSignificantDigits(value / Math.Pow(10, exponent), SignificantDigits);

            // rounding can carry the mantissa up to exactly 10, e.g. 9.9999999999999e5
            if (Math.Abs(mantissa) >= 10)
            {
                mantissa /= 10;
                exponent++;
            }

            return (mantissa.ToString(PlainNumberFormat, CultureInfo.InvariantCulture), exponent);
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
