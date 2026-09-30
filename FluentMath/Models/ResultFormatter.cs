using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace FluentMath.Models
{
    // the result formatter:
    // turns an evaluated value into what the display shows, rounded to the twelve digits of a Casio, so
    // 0.1 + 0.2 has no tail of noise
    // (InvariantCulture throughout, so a number parses back; a decimal comma is only drawn by the layout)
    public static class ResultFormatter
    {
        // === constants ===

        private const int SignificantDigits = 12;

        // outside this window the output switches to a power of ten; (the lower bounds of Norm 2 and Norm 1)
        private const double ScientificUpperBound = 1e12;
        private const double ScientificLowerBound = 1e-9;
        private const double Norm1LowerBound = 1e-2;

        // enough placeholders to spell out the smallest number that still avoids scientific notation
        private const string PlainNumberFormat = "0.####################";

        // Math.Round refuses more than 15 decimals
        private const int MaxRoundingDecimals = 15;

        // the largest denominator the numeric search returns, which is what keeps an irrational out
        private const long MaxFractionDenominator = 10000;

        // the ceiling on the whole part; (also keeps the expansion inside a long)
        private const long MaxFractionNumerator = 10000000000;

        // the largest number FACT takes apart, every whole number shown without a power of ten
        // (a Casio gives up on a prime factor above 1000; trial division up to a million costs nothing)
        private const long MaxFactorised = 999999999999;

        // --- exact forms ---
        // a fraction or π coefficient is shown when it fits this many characters as a mixed number, sign
        // and separators included (13871 48/89 is eleven; a Casio takes ten)
        private const int FractionBudget = 12;

        // a form with roots is shown with every coefficient, denominator and radicand below these, the
        // ranges of the Casio manual (99√2 exact, 100√2 a decimal)
        private const long MaxFormCoefficient = 100;
        private const long MaxFormRadicand = 1000;

        // a recurring decimal is shown when its digits up to the end of the first period fit this, a
        // leading 0 not counted (1÷17 fits, 1÷97 does not)
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

            // the times sign through the helper of the input line, so both are spaced alike
            return $"{written.Digits}{LatexHelper.TaggedOperator("\\times")}10^{{{exponent}}}";
        }

        public static string ToLatex(MathValue value, AnswerForm form, bool displayFractions)
        {
            return ToLatex(value, form, displayFractions, NumberFormat.Default);
        }

        // the value in the shape S⇔D has selected; a form it lacks falls back to the decimal
        // (improper and mixed are the exact form; roots or π have no whole part and are the same under both)
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

        // the same rounding as ToLatex, always as plain digits
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

        // the wording of a Casio
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

        // what the display draws, mirroring ToLatex arm for arm; a change to either belongs in both
        public static List<MathToken> ToTokens(EvaluationResult result, AnswerForm form, bool displayFractions, NumberFormat format)
        {
            if (form == AnswerForm.PrimeFactors && TryPrimeFactors(result.Value, out List<(long Prime, int Exponent)> factors))
            {
                return PrimeFactorTokens(factors);
            }

            if (result.Kind == ResultKind.Single) return ToTokens(result.FirstValue, form, displayFractions, format);

            // the names are drawn as text beside the digits, like the minus of a negative result
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

                // the sign rides on the whole part; the structure the mixed fraction key types, so a seed
                // puts back what is drawn
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

            // spelled out the way the EXP key spells it, so a result has the shape of a typed formula
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

        // a leading minus is part of the number, not an operator, which would take the spacing of one
        // (these tokens are only drawn, never evaluated)
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

        // with a decimal comma the layout draws it as a semicolon, like the one between two arguments
        private const string PairSeparator = ", ";

        // what a Casio calls the two values
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
        // (judged on the rounded value, so a number that rounds up to 1e12 is not thirteen digits)
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

        // exactly this many decimals; a number too large gets a power of ten whose mantissa keeps them
        // (a negative number that rounds to nothing keeps its minus for the next calculation)
        private static WrittenDecimal WriteFixed(double value, int decimals)
        {
            (decimal mantissa, int exponent) = Decompose(value);
            if (exponent >= 12) return WriteScientific(value, decimals + 1);

            // below half the last decimal it rounds to zero; scaling it would leave the decimal range
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

        // a mantissa from 1 to below 10 and its power of ten, read off the fifteen digits a Casio holds,
        // so 2.675 rounds to 2.68
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

        // whether ENG can write the value over this power of ten: the mantissa has to stay from 1e-9 up
        // to below 1e12, where ENG and its shift stop stepping
        public static bool CanWriteEngineering(double value, int exponent)
        {
            double rounded = RoundToSignificantDigits(value, SignificantDigits);
            if (rounded == 0 || !double.IsFinite(rounded)) return false;

            int magnitude = Decompose(rounded).Exponent - exponent;
            return magnitude >= -9 && magnitude < 12;
        }

        // the value over that power of ten, the mantissa in the number format (Norm twelve significant
        // digits, Fix n decimals, Sci n significant digits)
        // (with prefixes 1.234k, and nothing for the power 0; without them always the power, 1234×10⁰)
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

        // the primes of a whole number from 1 to MaxFactorised with their exponents, smallest first
        // (1 gives an empty list, anything else false)
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

        // real powers and times signs, so the factors carry on as the product they are
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

        // the fraction a value is shown as: its exact rational, else the numeric search; none for an
        // irrational or a whole number, and none past the budget (1÷12345 is 1/12345, a longer one a decimal)
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

        // the fraction by long division, split into the digits before the period and the period: 7/3 is
        // 2. and 3, 5/12 is 0.41 and 6 (none for a fraction that ends or whose period ends too late)
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

            // a remainder that comes round again repeats the digits; the period lies between its appearances
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

        // the value as degrees, minutes and seconds, the seconds to two decimals without trailing zeros:
        // 2.2583 is 2°15′29.88″, 2.5 is 2°30′0″
        // (read off the fifteen digits a Casio holds; a negative value that rounds to nothing keeps its minus)
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

        // an irrational exact value the way a Casio writes it: up to two terms of roots over one
        // denominator, (√6−√2)/4 or 5+2√6, or a rational times π, 1/6π; null outside the Casio ranges
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

        // the same form as tokens, mirroring ExactFormLatex, with real roots and π so a seed evaluates
        // back exactly; asInput writes a leading minus as the sign the evaluator reads
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

        // a minus for the whole value: drawn as part of the number, typed as the sign
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

        // the simplest fraction that still hits the value, by continued fraction expansion (0.333333333333
        // is a third); only for a value without an exact one, whose caps keep an irrational out
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

                // a term past the cap gives a denominator past it too; (also keeps the product inside a long)
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

            // the fraction is the same number when it shows the same twelve digits
            double candidate = (double)currentNumerator / currentDenominator;
            if (RoundToSignificantDigits(candidate, SignificantDigits) != Math.Abs(rounded)) return false;

            numerator = sign * currentNumerator;
            denominator = currentDenominator;
            return true;
        }


        // === helpers ===

        // shared by both output shapes, so a rounding carry is handled once
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

            // rounding left of the point, which Math.Round cannot express, by scaling down and back up
            if (decimals < 0)
            {
                double scale = Math.Pow(10, -decimals);
                return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
            }

            if (decimals <= MaxRoundingDecimals) return Math.Round(value, decimals, MidpointRounding.AwayFromZero);

            // a small number needs more decimals than Math.Round takes, so its mantissa is rounded and
            // scaled back
            double unit = Math.Pow(10, magnitude);
            return Math.Round(value / unit, digits - 1, MidpointRounding.AwayFromZero) * unit;
        }
    }


    // a decimal the way the display writes it: the digits, and behind them a power of ten, a prefix or
    // nothing; (what is drawn, carried on and returned by Rnd is built from this one shape)
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
