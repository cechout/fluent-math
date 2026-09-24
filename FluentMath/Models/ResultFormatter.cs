using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace FluentMath.Models
{
    // turns an evaluated double into something the display can show
    //
    // the rounding is not cosmetic; a double cannot hold 0.1 + 0.2 exactly, so without cutting the
    // result back to the digits a calculator claims to have, every second sum ends in a tail of noise
    // twelve significant digits is roughly what the Casio the app is modelled on shows
    //
    // everything here formats with InvariantCulture on purpose, a German system would otherwise put a
    // comma into a number that then no longer parses back; a decimal comma is only ever drawn, the layout
    // swaps it in
    public static class ResultFormatter
    {
        // === constants ===

        private const int SignificantDigits = 12;

        // outside this window a plain decimal is a wall of zeros, so the output switches to a power of ten
        // the lower bound is the one of Norm 2, which is what the display did before it had a setting
        private const double ScientificUpperBound = 1e12;
        private const double ScientificLowerBound = 1e-9;
        private const double Norm1LowerBound = 1e-2;

        // enough placeholders to spell out the smallest number that still avoids scientific notation
        private const string PlainNumberFormat = "0.####################";

        // Math.Round refuses more than 15 decimals
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

        // the largest number FACT takes apart: every whole number the display shows without a power of
        // ten, which trial division up to a million covers completely
        //
        // the Casio gives up on a prime factor above 1000 and shows it in brackets; covering the whole
        // range instead costs nothing noticeable
        private const long MaxFactorised = 999999999999;

        // --- exact forms ---
        // a fraction, and the coefficient of π, is shown when it fits this many characters written as a
        // mixed number, sign and separators included, 13871 48/89 being eleven; the Casio takes ten, here
        // it follows the twelve digits
        private const int FractionBudget = 12;

        // a form with roots is shown with every coefficient and its denominator below this, the ranges the
        // Casio manual gives; √997 is exact on the Casio and √1003 a decimal, 99√2 exact and 100√2 not
        private const long MaxFormCoefficient = 100;
        private const long MaxFormRadicand = 1000;

        // a recurring decimal is shown when its digits up to the end of the first period are no more than
        // this, a leading 0 not counted: 1÷17 has all sixteen under its bar, 1÷97 has no bar at all
        private const int MaxRecurringDigits = 16;

        // --- sexagesimal ---
        // the largest angle written in degrees, minutes and seconds, the 9999999°59′59″ a Casio converts
        private const long MaxSexagesimalDegrees = 9999999;
        private const long HundredthsPerDegree = 360000;
        private const long HundredthsPerMinute = 6000;


        // === public formatting ===

        public static string ToLatex(double value)
        {
            return ToLatex(value, NumberFormat.Default);
        }

        public static string ToLatex(double value, NumberFormat format)
        {
            return ToLatex(Write(value, format));
        }

        public static string ToLatex(WrittenDecimal written)
        {
            if (written.Prefix != null) return written.Digits + new PostfixToken(written.Prefix).ToLatex(null!);
            if (written.Exponent is not int exponent) return written.Digits;

            // the times sign goes through the same helper the input line uses, so a result is not spaced
            // differently from the formula that produced it
            return $"{written.Digits}{LatexHelper.TaggedOperator("\\times")}10^{{{exponent}}}";
        }

        public static string ToLatex(MathValue value, AnswerForm form, bool displayFractions)
        {
            return ToLatex(value, form, displayFractions, NumberFormat.Default);
        }

        // the same value in the shape the S to D key currently has selected; a form this value does not
        // have falls back to the decimal rather than to nothing
        //
        // the improper and the mixed form are the exact form: a fraction for a rational, and for anything
        // else the form with roots or π, which has no whole part to split off and is the same under both
        public static string ToLatex(MathValue value, AnswerForm form, bool displayFractions, NumberFormat format)
        {
            if (form == AnswerForm.Decimal) return ToLatex(value.Value, format);

            string command = displayFractions ? "dfrac" : "frac";

            if (form == AnswerForm.Recurring)
            {
                if (!TryRecurring(value, out string leading, out string period)) return ToLatex(value.Value, format);
                return $"{leading}\\overline{{{period}}}";
            }

            if (form == AnswerForm.Sexagesimal)
            {
                if (!TrySexagesimal(value.Value, out bool negative, out long degrees, out long minutes, out string seconds))
                {
                    return ToLatex(value.Value, format);
                }

                string sign = negative ? "-" : "";
                return $"{sign}{degrees.ToString(CultureInfo.InvariantCulture)}{MarkerLatex("degrees")}"
                    + $"{minutes.ToString(CultureInfo.InvariantCulture)}{MarkerLatex("minutes")}{seconds}{MarkerLatex("seconds")}";
            }

            if (!TryFraction(value, out long numerator, out long denominator))
            {
                return ExactFormLatex(value.Exact, command) ?? ToLatex(value.Value, format);
            }

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

        public static string ToLatex(EvaluationResult result, AnswerForm form, bool displayFractions)
        {
            return ToLatex(result, form, displayFractions, NumberFormat.Default);
        }

        // a result in the form it is shown in: a single value in the answer form, a pair as both its values
        // with their names, or the prime factors
        public static string ToLatex(EvaluationResult result, AnswerForm form, bool displayFractions, NumberFormat format)
        {
            if (form == AnswerForm.PrimeFactors && TryPrimeFactors(result.Value, out List<(long Prime, int Exponent)> factors))
            {
                return PrimeFactorLatex(factors);
            }

            if (result.Kind == ResultKind.Single) return ToLatex(result.FirstValue, form, displayFractions, format);

            (string first, string second) = PairNames(result.Kind);
            return $"{first}={ToLatex(result.FirstValue, form, displayFractions, format)}{PairSeparator}"
                + $"{second}={ToLatex(result.SecondValue, form, displayFractions, format)}";
        }

        private static string PrimeFactorLatex(List<(long Prime, int Exponent)> factors)
        {
            if (factors.Count == 0) return "1";

            List<string> parts = new List<string>();
            foreach ((long prime, int exponent) in factors)
            {
                string text = prime.ToString(CultureInfo.InvariantCulture);
                parts.Add(exponent == 1 ? text : $"{text}^{{{exponent.ToString(CultureInfo.InvariantCulture)}}}");
            }

            return string.Join(LatexHelper.TaggedOperator("\\times"), parts);
        }

        // the wording is the one a Casio uses, short enough to still fit the display at full size
        public static string ErrorToText(EvaluationError error)
        {
            if (error == EvaluationError.Syntax) return "Syntax ERROR";
            if (error == EvaluationError.Argument) return "Argument ERROR";
            if (error == EvaluationError.TimeOut) return "Time Out";

            return "Math ERROR";
        }

        public static string ErrorToLatex(EvaluationError error)
        {
            return $"\\text{{{ErrorToText(error)}}}";
        }


        // === the same values as tokens ===

        public static List<MathToken> ToTokens(EvaluationResult result, AnswerForm form, bool displayFractions)
        {
            return ToTokens(result, form, displayFractions, NumberFormat.Default);
        }

        // what the native display draws, mirroring ToLatex arm for arm rather than parsing what that
        // produces, so the two shapes cannot drift apart; a change to either belongs in both
        public static List<MathToken> ToTokens(EvaluationResult result, AnswerForm form, bool displayFractions, NumberFormat format)
        {
            if (form == AnswerForm.PrimeFactors && TryPrimeFactors(result.Value, out List<(long Prime, int Exponent)> factors))
            {
                return PrimeFactorTokens(factors);
            }

            if (result.Kind == ResultKind.Single) return ToTokens(result.FirstValue, form, displayFractions, format);

            // the names are drawn as text beside the digits rather than typed as anything, the same as the
            // minus of a negative result below
            (string first, string second) = PairNames(result.Kind);

            List<MathToken> tokens = DigitTokens(first + "=");
            tokens.AddRange(ToTokens(result.FirstValue, form, displayFractions, format));
            tokens.AddRange(DigitTokens(PairSeparator + second + "="));
            tokens.AddRange(ToTokens(result.SecondValue, form, displayFractions, format));

            return tokens;
        }

        public static List<MathToken> ToTokens(MathValue value, AnswerForm form, bool displayFractions)
        {
            return ToTokens(value, form, displayFractions, NumberFormat.Default);
        }

        public static List<MathToken> ToTokens(MathValue value, AnswerForm form, bool displayFractions, NumberFormat format)
        {
            if (form == AnswerForm.Decimal) return ToTokens(value.Value, format);

            // the digits in front of the period as they are, and the period as the token the bar is drawn over
            if (form == AnswerForm.Recurring)
            {
                if (!TryRecurring(value, out string leading, out string period)) return ToTokens(value.Value, format);

                List<MathToken> recurring = DigitTokens(leading);
                recurring.Add(new RecurringToken(period));

                return recurring;
            }

            if (form == AnswerForm.Sexagesimal) return SexagesimalTokens(value.Value, asInput: false) ?? ToTokens(value.Value, format);

            if (TryFraction(value, out long numerator, out long denominator))
            {
                long whole = form == AnswerForm.Mixed ? numerator / denominator : 0;
                if (whole == 0) return new List<MathToken> { FractionTokens(numerator, denominator) };

                // the sign rides on the whole part, so the remainder is always written positive; it is the
                // structure the mixed fraction key types, so seeding it puts back exactly what is drawn
                MixedFractionToken mixed = new MixedFractionToken();
                mixed.WholeTokens.AddRange(DigitTokens(whole.ToString(CultureInfo.InvariantCulture)));
                mixed.NumeratorTokens.AddRange(DigitTokens(Math.Abs(numerator % denominator).ToString(CultureInfo.InvariantCulture)));
                mixed.DenominatorTokens.AddRange(DigitTokens(denominator.ToString(CultureInfo.InvariantCulture)));

                return new List<MathToken> { mixed };
            }

            return ExactFormTokens(value, asInput: false) ?? ToTokens(value.Value, format);
        }

        public static List<MathToken> ToTokens(double value)
        {
            return ToTokens(value, NumberFormat.Default);
        }

        public static List<MathToken> ToTokens(double value, NumberFormat format)
        {
            return ToTokens(Write(value, format));
        }

        public static List<MathToken> ToTokens(WrittenDecimal written)
        {
            List<MathToken> tokens = DigitTokens(written.Digits);

            if (written.Prefix != null)
            {
                tokens.Add(new PostfixToken(written.Prefix));
                return tokens;
            }

            if (written.Exponent is not int exponent) return tokens;

            // spelled out the same way the EXP key spells it, so a result and a typed formula are
            // the same shape rather than two that happen to look alike
            tokens.Add(new MathToken(TokenType.Operator, "*"));

            PowerToken power = new PowerToken();
            power.BaseTokens.AddRange(DigitTokens("10"));
            power.ExponentTokens.AddRange(DigitTokens(exponent.ToString(CultureInfo.InvariantCulture)));
            tokens.Add(power);

            return tokens;
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


        // === pairs ===

        // what a Casio calls the two values; with a decimal comma the layout draws the separator as a
        // semicolon, the way it draws the one between two arguments
        private const string PairSeparator = ", ";

        private static (string First, string Second) PairNames(ResultKind kind)
        {
            return kind switch
            {
                ResultKind.QuotientRemainder => ("Q", "R"),
                ResultKind.Polar => ("r", "θ"),
                _ => ("x", "y")
            };
        }


        // === number formats ===

        // the value as the number format writes it
        public static WrittenDecimal Write(double value, NumberFormat format)
        {
            if (!double.IsFinite(value)) return new WrittenDecimal(ToPlainString(value));

            return format.Notation switch
            {
                NumberNotation.Fix => WriteFixed(value, format.Digits),
                NumberNotation.Sci => WriteScientific(value, SciDigits(format)),
                NumberNotation.Norm1 => WriteNormal(value, Norm1LowerBound),
                _ => WriteNormal(value, ScientificLowerBound)
            };
        }

        // the value rounded the way the number format writes it, which is what Rnd hands back
        public static double RoundToFormat(double value, NumberFormat format)
        {
            if (!double.IsFinite(value)) return value;

            return Write(value, format).Value;
        }

        // every significant digit up to twelve, and a power of ten outside the window
        //
        // the window is judged on the rounded value, so a number that rounds up to 1e12 is not written as
        // thirteen digits
        private static WrittenDecimal WriteNormal(double value, double lowerBound)
        {
            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            double absolute = Math.Abs(rounded);

            if (rounded != 0 && (absolute >= ScientificUpperBound || absolute < lowerBound))
            {
                (string mantissa, int exponent) = SplitScientific(value);
                return new WrittenDecimal(mantissa, exponent);
            }

            return new WrittenDecimal(ToPlainString(value));
        }

        // exactly this many decimals; a number too large for the display is written with a power of ten
        // whose mantissa keeps them
        //
        // a negative number that rounds to nothing keeps its minus, since the digits carry the value on
        // into the next calculation and the sign would otherwise be lost there
        private static WrittenDecimal WriteFixed(double value, int decimals)
        {
            (decimal mantissa, int exponent) = Decompose(value);
            if (exponent >= 12) return WriteScientific(value, decimals + 1);

            // below half the last decimal the value rounds to zero, and scaling it would leave the range
            // a decimal holds
            decimal plain = exponent < -decimals - 1 ? 0m : Scale(mantissa, exponent);
            decimal rounded = Math.Round(plain, decimals, MidpointRounding.AwayFromZero);

            if (Math.Abs(rounded) >= 1e12m) return WriteScientific(value, decimals + 1);

            return new WrittenDecimal(Signed(Math.Abs(rounded).ToString("F" + decimals, CultureInfo.InvariantCulture), value));
        }

        // exactly this many significant digits, always with a power of ten
        private static WrittenDecimal WriteScientific(double value, int significant)
        {
            (decimal mantissa, int exponent) = Decompose(value);

            decimal rounded = Math.Round(mantissa, significant - 1, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded) >= 10)
            {
                rounded /= 10;
                exponent++;
            }

            string digits = Math.Abs(rounded).ToString("F" + (significant - 1), CultureInfo.InvariantCulture);
            return new WrittenDecimal(Signed(digits, value), exponent);
        }

        private static int SciDigits(NumberFormat format)
        {
            return format.Digits == 0 ? SignificantDigits : format.Digits;
        }

        private static string Signed(string digits, double value)
        {
            return value < 0 ? "-" + digits : digits;
        }

        // the value as a mantissa from 1 to below 10 and its power of ten, read off the fifteen digits a
        // Casio holds, so it rounds the way it reads: 2.675 goes to 2.68 where the double just below it
        // would round down
        private static (decimal Mantissa, int Exponent) Decompose(double value)
        {
            string text = value.ToString("E14", CultureInfo.InvariantCulture);
            int split = text.IndexOf('E');

            decimal mantissa = decimal.Parse(text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture);
            int exponent = int.Parse(text.Substring(split + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

            return (mantissa, exponent);
        }

        private static decimal Scale(decimal value, int exponent)
        {
            for (int step = 0; step < exponent; step++) value *= 10;
            for (int step = 0; step > exponent; step--) value /= 10;

            return value;
        }


        // === engineering ===

        // the power of ten a first press of ENG writes: the largest multiple of three that leaves at least
        // one digit in front of the point
        public static int EngineeringExponent(double value)
        {
            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            if (rounded == 0 || !double.IsFinite(rounded)) return 0;

            int magnitude = Decompose(rounded).Exponent;
            return (int)Math.Floor(magnitude / 3.0) * 3;
        }

        // whether the ENG view can write the value over this power of ten: the mantissa has to stay a
        // number the display writes without a power of its own, from 1e-9 up to below 1e12, which is
        // where ENG and its shift stop stepping
        public static bool CanWriteEngineering(double value, int exponent)
        {
            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            if (rounded == 0 || !double.IsFinite(rounded)) return false;

            int magnitude = Decompose(rounded).Exponent - exponent;
            return magnitude >= -9 && magnitude < 12;
        }

        // the value over that power of ten, the mantissa written the way the number format writes
        // digits: up to twelve significant ones in Norm, n decimals in Fix and n significant digits in Sci
        //
        // with the prefixes on, a power that has one is written as it, 1.234k, and the power 0 as nothing
        // at all; without them the power is always written, 1234×10⁰, as on a Casio
        public static WrittenDecimal Engineering(double value, int exponent, NumberFormat format, bool usePrefixes)
        {
            (decimal mantissa, int magnitude) = Decompose(value);
            int shift = magnitude - exponent;

            int decimals = format.Notation switch
            {
                NumberNotation.Fix => format.Digits,
                NumberNotation.Sci => SciDigits(format) - 1 - shift,
                _ => SignificantDigits - 1 - shift
            };

            decimal rounded = Math.Abs(RoundDecimal(Scale(mantissa, shift), decimals));
            string digits = format.Notation == NumberNotation.Norm1 || format.Notation == NumberNotation.Norm2
                ? rounded.ToString(PlainNumberFormat, CultureInfo.InvariantCulture)
                : rounded.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);

            digits = Signed(digits, value);

            if (!usePrefixes) return new WrittenDecimal(digits, exponent);
            if (exponent == 0) return new WrittenDecimal(digits);

            string? prefix = PostfixToken.PrefixFor(exponent);
            return prefix == null ? new WrittenDecimal(digits, exponent) : new WrittenDecimal(digits, exponent, prefix);
        }

        // a negative number of decimals rounds in front of the point, the way Sci 3 writes 1234 as 1230
        private static decimal RoundDecimal(decimal value, int decimals)
        {
            if (decimals >= 0) return Math.Round(value, Math.Min(decimals, 28), MidpointRounding.AwayFromZero);

            decimal unit = Scale(1m, -decimals);
            return Math.Round(value / unit, 0, MidpointRounding.AwayFromZero) * unit;
        }


        // === prime factors ===

        // the primes of the value with their exponents, smallest first, for a whole number from 1 to
        // MaxFactorised as the display shows it; 1 has none and comes back as an empty list
        //
        // anything else has no prime factors, which the Casio answers with a Math ERROR
        public static bool TryPrimeFactors(double value, out List<(long Prime, int Exponent)> factors)
        {
            factors = new List<(long Prime, int Exponent)>();

            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            double shown = RoundToSignificantDigits(value, SignificantDigits);
            if (shown < 1 || shown > MaxFactorised || shown != Math.Floor(shown)) return false;

            long remaining = (long)shown;
            for (long divisor = 2; divisor * divisor <= remaining; divisor++)
            {
                int exponent = 0;
                while (remaining % divisor == 0)
                {
                    remaining /= divisor;
                    exponent++;
                }

                if (exponent > 0) factors.Add((divisor, exponent));
            }

            // what is left after every divisor up to its square root is itself a prime
            if (remaining > 1) factors.Add((remaining, 1));

            return true;
        }

        // real powers and real times signs, so the shown factors carry on into the next calculation as
        // the product they are
        public static List<MathToken> PrimeFactorTokens(List<(long Prime, int Exponent)> factors)
        {
            if (factors.Count == 0) return DigitTokens("1");

            List<MathToken> tokens = new List<MathToken>();
            foreach ((long prime, int exponent) in factors)
            {
                if (tokens.Count > 0) tokens.Add(new MathToken(TokenType.Operator, "*"));

                string text = prime.ToString(CultureInfo.InvariantCulture);
                if (exponent == 1)
                {
                    tokens.AddRange(DigitTokens(text));
                    continue;
                }

                PowerToken power = new PowerToken();
                power.BaseTokens.AddRange(DigitTokens(text));
                power.ExponentTokens.AddRange(DigitTokens(exponent.ToString(CultureInfo.InvariantCulture)));
                tokens.Add(power);
            }

            return tokens;
        }


        // === exact forms ===

        // the fraction a value is shown as: its exact value when that is a rational, the numeric search
        // below when it has none, and nothing at all for a value known to be irrational; a whole number is
        // its own simplest form and has none either
        //
        // only a fraction that fits the budget counts, so 1÷12345 is 1/12345 and a longer one a decimal
        public static bool TryFraction(MathValue value, out long numerator, out long denominator)
        {
            numerator = 0;
            denominator = 1;

            if (value.Exact != null)
            {
                if (!value.Exact.TryGetRational(out Rational rational) || rational.IsInteger) return false;
                if (MixedLength(rational.Numerator, rational.Denominator) > FractionBudget) return false;

                numerator = (long)rational.Numerator;
                denominator = (long)rational.Denominator;
                return true;
            }

            if (!TryToFraction(value.Value, out numerator, out denominator) || denominator <= 1) return false;

            return MixedLength(numerator, denominator) <= FractionBudget;
        }

        // a fraction, or a form with roots or π, which is what exact first opens a result in
        public static bool HasExactForm(MathValue value)
        {
            return HasFractionForm(value) || TryPiForm(value.Exact, out _, out _) || TryRootForm(value.Exact, out _, out _);
        }

        // a whole number is already its own simplest form, so it counts as having no fraction to show
        public static bool HasFractionForm(MathValue value)
        {
            return TryFraction(value, out _, out _);
        }

        // a mixed number needs a whole part to split off, so it only exists above one
        public static bool HasMixedForm(MathValue value)
        {
            if (!TryFraction(value, out long numerator, out long denominator)) return false;

            return Math.Abs(numerator) > denominator;
        }

        public static bool HasRecurringForm(MathValue value)
        {
            return TryRecurring(value, out _, out _);
        }

        // how many characters the value takes written as a mixed number, sign and separators included
        private static int MixedLength(BigInteger numerator, BigInteger denominator)
        {
            BigInteger magnitude = BigInteger.Abs(numerator);
            BigInteger whole = magnitude / denominator;
            BigInteger rest = magnitude % denominator;

            int length = numerator.Sign < 0 ? 1 : 0;
            if (rest.IsZero) return length + DigitCount(whole);

            length += DigitCount(rest) + 1 + DigitCount(denominator);
            if (!whole.IsZero) length += DigitCount(whole) + 1;

            return length;
        }

        private static int DigitCount(BigInteger value)
        {
            return BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture).Length;
        }


        // === recurring decimals ===

        // the fraction written out by long division, split into the digits in front of the period and the
        // period itself: 7/3 is 2. and 3, 5/12 is 0.41 and 6
        //
        // a fraction that ends has no period, and one whose period ends too late has none shown
        public static bool TryRecurring(MathValue value, out string leading, out string period)
        {
            leading = "";
            period = "";

            if (!TryFraction(value, out long numerator, out long denominator)) return false;

            long magnitude = Math.Abs(numerator);
            long whole = magnitude / denominator;
            long remainder = magnitude % denominator;
            int wholeDigits = whole == 0 ? 0 : whole.ToString(CultureInfo.InvariantCulture).Length;

            StringBuilder digits = new StringBuilder();
            Dictionary<long, int> seen = new Dictionary<long, int>();

            // a remainder that comes round again starts the same digits again, and the period is what
            // lies between its two appearances
            while (remainder != 0)
            {
                if (seen.TryGetValue(remainder, out int start))
                {
                    string sign = numerator < 0 ? "-" : "";
                    leading = sign + whole.ToString(CultureInfo.InvariantCulture) + "." + digits.ToString(0, start);
                    period = digits.ToString(start, digits.Length - start);
                    return true;
                }

                if (wholeDigits + digits.Length >= MaxRecurringDigits) return false;

                seen[remainder] = digits.Length;
                remainder *= 10;
                digits.Append((char)('0' + remainder / denominator));
                remainder %= denominator;
            }

            return false;
        }


        // === sexagesimal ===

        // the value as degrees, minutes and seconds, the seconds to two decimals and without the zeros
        // behind them: 2.2583 is 2°15′29.88″ and 2.5 is 2°30′0″; a second that rounds up to sixty carries
        // on into the minutes and the degrees
        //
        // read off the fifteen digits a Casio holds, the way the fixed decimals are; a negative value that
        // rounds to nothing keeps its minus, as it does there
        public static bool TrySexagesimal(double value, out bool negative, out long degrees, out long minutes, out string seconds)
        {
            negative = value < 0;
            degrees = 0;
            minutes = 0;
            seconds = "0";

            if (!double.IsFinite(value) || Math.Abs(value) >= MaxSexagesimalDegrees + 1) return false;

            (decimal mantissa, int exponent) = Decompose(value);
            long hundredths = (long)Math.Round(Math.Abs(Scale(mantissa, exponent)) * HundredthsPerDegree, MidpointRounding.AwayFromZero);

            degrees = hundredths / HundredthsPerDegree;
            if (degrees > MaxSexagesimalDegrees) return false;

            minutes = hundredths % HundredthsPerDegree / HundredthsPerMinute;
            seconds = (hundredths % HundredthsPerMinute / 100m).ToString("0.##", CultureInfo.InvariantCulture);
            return true;
        }

        public static bool HasSexagesimalForm(double value)
        {
            return TrySexagesimal(value, out _, out _, out _, out _);
        }

        // the digits with the real markers of the °′″ key between them, so seeding them types the angle back
        // in; asInput writes a leading minus as the sign
        public static List<MathToken>? SexagesimalTokens(double value, bool asInput)
        {
            if (!TrySexagesimal(value, out bool negative, out long degrees, out long minutes, out string seconds)) return null;

            List<MathToken> tokens = LeadingSign(negative, asInput);
            tokens.AddRange(DigitTokens(degrees.ToString(CultureInfo.InvariantCulture)));
            tokens.Add(new PostfixToken("degrees"));
            tokens.AddRange(DigitTokens(minutes.ToString(CultureInfo.InvariantCulture)));
            tokens.Add(new PostfixToken("minutes"));
            tokens.AddRange(DigitTokens(seconds));
            tokens.Add(new PostfixToken("seconds"));

            return tokens;
        }

        private static string MarkerLatex(string kind)
        {
            return new PostfixToken(kind).ToLatex(null!);
        }


        // === roots and π ===

        // an irrational exact value the way a Casio writes it: up to two terms with roots over one
        // denominator, the rational term first, (√6−√2)/4 or 5+2√6; or a rational times π, with the
        // coefficient as a fraction in front, 1/6π
        //
        // null when there is no such value, or when it does not fit the ranges a Casio shows it in; the
        // caller writes the decimal then
        private static string? ExactFormLatex(ExactValue? exact, string command)
        {
            if (TryPiForm(exact, out long piNumerator, out long piDenominator))
            {
                string sign = piNumerator < 0 ? "-" : "";
                long magnitude = Math.Abs(piNumerator);

                if (piDenominator > 1) return $"{sign}{FractionLatex(command, magnitude, piDenominator)}\\pi";
                return magnitude == 1 ? $"{sign}\\pi" : $"{sign}{magnitude.ToString(CultureInfo.InvariantCulture)}\\pi";
            }

            if (!TryRootForm(exact, out List<(long Coefficient, long Radicand)> terms, out long denominator)) return null;

            if (denominator == 1) return TermsLatex(terms);

            string bottom = denominator.ToString(CultureInfo.InvariantCulture);

            // a single term keeps its sign in front of the bar, two keep theirs inside it
            if (terms.Count == 1 && terms[0].Coefficient < 0)
            {
                return $"-\\{command}{{{TermsLatex(new List<(long, long)> { (-terms[0].Coefficient, terms[0].Radicand) })}}}{{{bottom}}}";
            }

            return $"\\{command}{{{TermsLatex(terms)}}}{{{bottom}}}";
        }

        private static string TermsLatex(List<(long Coefficient, long Radicand)> terms)
        {
            StringBuilder latex = new StringBuilder();

            foreach ((long coefficient, long radicand) in terms)
            {
                if (coefficient < 0) latex.Append('-');
                else if (latex.Length > 0) latex.Append('+');

                long magnitude = Math.Abs(coefficient);
                if (magnitude != 1 || radicand == 1) latex.Append(magnitude.ToString(CultureInfo.InvariantCulture));
                if (radicand != 1) latex.Append($"\\sqrt{{{radicand.ToString(CultureInfo.InvariantCulture)}}}");
            }

            return latex.ToString();
        }

        // the same form as tokens, mirroring ExactFormLatex; asInput writes a leading minus as the sign the
        // evaluator reads rather than as part of a number, which is the one difference between what is
        // drawn and what a continuing calculation is seeded with
        //
        // the roots are real roots and π is the real constant, so the seed evaluates back to the exact value
        public static List<MathToken>? ExactFormTokens(MathValue value, bool asInput)
        {
            if (TryPiForm(value.Exact, out long piNumerator, out long piDenominator))
            {
                List<MathToken> pi = LeadingSign(piNumerator < 0, asInput);
                long magnitude = Math.Abs(piNumerator);

                if (piDenominator > 1) pi.Add(FractionTokens(magnitude, piDenominator));
                else if (magnitude != 1) pi.AddRange(DigitTokens(magnitude.ToString(CultureInfo.InvariantCulture)));

                pi.Add(new ConstantToken("pi"));
                return pi;
            }

            if (!TryRootForm(value.Exact, out List<(long Coefficient, long Radicand)> terms, out long denominator)) return null;

            if (denominator == 1) return TermTokens(terms, asInput);

            FractionToken fraction = new FractionToken();
            fraction.DenominatorTokens.AddRange(DigitTokens(denominator.ToString(CultureInfo.InvariantCulture)));

            // a single term keeps its sign in front of the bar, two keep theirs inside it
            if (terms.Count == 1 && terms[0].Coefficient < 0)
            {
                fraction.NumeratorTokens.AddRange(TermTokens(new List<(long, long)> { (-terms[0].Coefficient, terms[0].Radicand) }, asInput));

                List<MathToken> negative = LeadingSign(true, asInput);
                negative.Add(fraction);
                return negative;
            }

            fraction.NumeratorTokens.AddRange(TermTokens(terms, asInput));
            return new List<MathToken> { fraction };
        }

        private static List<MathToken> TermTokens(List<(long Coefficient, long Radicand)> terms, bool asInput)
        {
            List<MathToken> tokens = new List<MathToken>();

            foreach ((long coefficient, long radicand) in terms)
            {
                if (tokens.Count == 0) tokens.AddRange(LeadingSign(coefficient < 0, asInput));
                else tokens.Add(new MathToken(TokenType.Operator, coefficient < 0 ? "-" : "+"));

                long magnitude = Math.Abs(coefficient);
                if (magnitude != 1 || radicand == 1) tokens.AddRange(DigitTokens(magnitude.ToString(CultureInfo.InvariantCulture)));

                if (radicand == 1) continue;

                RootToken root = new RootToken();
                root.RadicandTokens.AddRange(DigitTokens(radicand.ToString(CultureInfo.InvariantCulture)));
                tokens.Add(root);
            }

            return tokens;
        }

        // a minus that stands for the whole value: drawn as part of the number the way a negative decimal
        // is, and typed as the sign the evaluator reads in front of its operand
        private static List<MathToken> LeadingSign(bool negative, bool asInput)
        {
            if (!negative) return new List<MathToken>();
            if (asInput) return new List<MathToken> { new MathToken(TokenType.Operator, "-") };

            return DigitTokens("-");
        }

        private static bool TryPiForm(ExactValue? exact, out long numerator, out long denominator)
        {
            numerator = 0;
            denominator = 1;

            if (exact == null || !exact.TimesPi) return false;

            Rational coefficient = exact.Terms[0].Coefficient;
            if (MixedLength(coefficient.Numerator, coefficient.Denominator) > FractionBudget) return false;

            numerator = (long)coefficient.Numerator;
            denominator = (long)coefficient.Denominator;
            return true;
        }

        // the terms over their common denominator, each coefficient a whole number
        private static bool TryRootForm(ExactValue? exact, out List<(long Coefficient, long Radicand)> terms,
            out long denominator)
        {
            terms = new List<(long Coefficient, long Radicand)>();
            denominator = 1;

            if (exact == null || exact.TimesPi || exact.IsRational) return false;

            BigInteger common = BigInteger.One;
            foreach (SurdTerm term in exact.Terms)
            {
                BigInteger termDenominator = term.Coefficient.Denominator;
                common = common * termDenominator / BigInteger.GreatestCommonDivisor(common, termDenominator);
            }

            if (common >= MaxFormCoefficient) return false;

            foreach (SurdTerm term in exact.Terms)
            {
                BigInteger coefficient = term.Coefficient.Numerator * (common / term.Coefficient.Denominator);
                if (BigInteger.Abs(coefficient) >= MaxFormCoefficient || term.Radicand >= MaxFormRadicand) return false;

                terms.Add(((long)coefficient, term.Radicand));
            }

            denominator = (long)common;
            return true;
        }


        // === fractions ===

        // the simplest fraction that still hits the value, found by continued-fraction expansion, which
        // is what lets 0.333333333333 come back as a third
        //
        // this is numeric and nothing else: a result that came out of a root or a pi has no fraction to
        // find here, and the caller leaves it as a decimal
        // it only runs for a value that has no exact one, a logarithm or e say, since an exact value
        // knows its fraction; its caps are what keep an irrational out there
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

            // the fraction is the same number when it shows the same twelve digits; a fixed distance of
            // 1e-12 times the value was tighter than the rounding itself once a whole part took a digit,
            // and turned 7/3 away because 2.33333333333 is 3.3e-12 short of it
            double candidate = (double)currentNumerator / currentDenominator;
            if (RoundToSignificantDigits(candidate, SignificantDigits) != Math.Abs(rounded)) return false;

            numerator = sign * currentNumerator;
            denominator = currentDenominator;
            return true;
        }


        // === helpers ===

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

        // public because the evaluator cuts a trigonometric result with it as well
        public static double RoundToSignificantDigits(double value, int digits)
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

            if (decimals <= MaxRoundingDecimals) return Math.Round(value, decimals, MidpointRounding.AwayFromZero);

            // a small number needs more decimals than Math.Round takes, so its mantissa is rounded and
            // scaled back; capping the decimals instead cost 1.2345678901234e-8 four of its twelve digits
            double unit = Math.Pow(10, magnitude);
            return Math.Round(value / unit, digits - 1, MidpointRounding.AwayFromZero) * unit;
        }
    }


    // a decimal the way the display writes it: the digits, and behind them a power of ten, a decimal
    // prefix or nothing
    //
    // the one shape both outputs and the seed of the next calculation are built from, so what is drawn,
    // what is carried on and what Rnd returns are the same number
    public readonly struct WrittenDecimal
    {
        public string Digits { get; } // a negative number starts with a plain minus

        // the power of ten the digits are scaled by, null when they stand alone
        public int? Exponent { get; }

        // the prefix written for that power instead of the power itself, by its postfix name
        public string? Prefix { get; }

        public WrittenDecimal(string digits, int? exponent = null, string? prefix = null)
        {
            Digits = digits;
            Exponent = exponent;
            Prefix = prefix;
        }

        public double Value
        {
            get
            {
                string text = Exponent is int exponent
                    ? Digits + "E" + exponent.ToString(CultureInfo.InvariantCulture)
                    : Digits;

                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
