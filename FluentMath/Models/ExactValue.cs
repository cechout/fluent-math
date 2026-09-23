using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace FluentMath.Models
{
    // a fraction of two whole numbers of any size, always in lowest terms with the sign on the numerator
    public readonly struct Rational : IEquatable<Rational>
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        public Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            if (divisor > 1)
            {
                numerator /= divisor;
                denominator /= divisor;
            }

            Numerator = numerator;
            Denominator = denominator;
        }

        public bool IsZero => Numerator.IsZero;
        public bool IsInteger => Denominator.IsOne;
        public int Sign => Numerator.Sign;

        public static Rational operator +(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Denominator + right.Numerator * left.Denominator,
                left.Denominator * right.Denominator);
        }

        public static Rational operator -(Rational value)
        {
            return new Rational(-value.Numerator, value.Denominator);
        }

        public static Rational operator -(Rational left, Rational right)
        {
            return left + -right;
        }

        public static Rational operator *(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Numerator, left.Denominator * right.Denominator);
        }

        // the caller has ruled out a zero divisor
        public static Rational operator /(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Denominator, left.Denominator * right.Numerator);
        }

        // each side is cut to its leading 64 bits before it becomes a double and the power of two that cost
        // is put back afterwards, so neither side overflows on the way however large it is
        public double ToDouble()
        {
            int numeratorShift = Math.Max(0, (int)BigInteger.Abs(Numerator).GetBitLength() - 64);
            int denominatorShift = Math.Max(0, (int)Denominator.GetBitLength() - 64);

            double ratio = (double)(Numerator >> numeratorShift) / (double)(Denominator >> denominatorShift);
            return Math.ScaleB(ratio, numeratorShift - denominatorShift);
        }

        public bool Equals(Rational other)
        {
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object? obj) => obj is Rational other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
    }


    // one term of an exact value: a rational coefficient times the square root of a square free whole
    // number, which is 1 for the rational term
    public readonly struct SurdTerm
    {
        public Rational Coefficient { get; }
        public long Radicand { get; }

        public SurdTerm(Rational coefficient, long radicand)
        {
            Coefficient = coefficient;
            Radicand = radicand;
        }
    }


    // a real number known exactly: a sum of rational multiples of square roots, or a rational multiple of π
    //
    // the exact path the evaluator carries beside every double, so √2×√2 is 2 and 1÷3×3 is 1 rather than
    // the doubles nearest to them; a step that has no exact answer, a logarithm or e say, hands back null,
    // and from there on only the double is left. Every operation takes and returns null for that reason
    //
    // every root is of a square free number and every coefficient is rational, so two values are the same
    // number exactly when they have the same terms, which is what lets the inverse functions look their
    // argument up in a table
    // π stands only on its own, times a rational: π+1 and π√2 have no exact value here, and a Casio shows
    // both as a decimal
    public sealed class ExactValue : IEquatable<ExactValue>
    {
        // === limits ===

        // past these a value drops the exact path and keeps the double; they bound the work a key can cost,
        // and a value that large is shown as a decimal anyway
        private const int MaxTerms = 2;                 // the forms a Casio shows have two terms at most
        private const int MaxBits = 1024;               // of a numerator or a denominator; 170! still fits
        private const long MaxRadicand = 1000000000000; // what a square factor is still searched for in
        private const int MaxExponent = 4096;           // a whole power, before its size is even looked at


        // === value ===

        // the rational term first, then the roots by descending radicand, none of them zero; empty for 0
        public IReadOnlyList<SurdTerm> Terms { get; }

        // the value is its one rational term times π
        public bool TimesPi { get; }

        private ExactValue(List<SurdTerm> terms, bool timesPi)
        {
            Terms = terms;
            TimesPi = timesPi;
        }

        public static readonly ExactValue Zero = new ExactValue(new List<SurdTerm>(), false);

        public static readonly ExactValue Pi = new ExactValue(
            new List<SurdTerm> { new SurdTerm(new Rational(1, 1), 1) }, true);

        public bool IsZero => Terms.Count == 0;

        public static ExactValue FromRational(Rational value)
        {
            if (value.IsZero) return Zero;

            return new ExactValue(new List<SurdTerm> { new SurdTerm(value, 1) }, false);
        }

        public static ExactValue FromInteger(BigInteger value)
        {
            return FromRational(new Rational(value, 1));
        }

        // a number written in decimal digits, with an optional minus and point, times a power of ten; null
        // for anything that is not one, or one too large to keep
        public static ExactValue? FromDecimal(string text, int exponent = 0)
        {
            bool negative = text.StartsWith('-');
            string body = negative ? text.Substring(1) : text;

            int point = body.IndexOf('.');
            if (point >= 0 && body.IndexOf('.', point + 1) >= 0) return null;

            string digits = point < 0 ? body : body.Remove(point, 1);
            if (digits.Length == 0) return null;

            foreach (char character in digits)
            {
                if (!char.IsAsciiDigit(character)) return null;
            }

            BigInteger mantissa = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
            if (negative) mantissa = -mantissa;

            int scale = (point < 0 ? 0 : body.Length - point - 1) - exponent;
            Rational value = scale >= 0
                ? new Rational(mantissa, BigInteger.Pow(10, scale))
                : new Rational(mantissa * BigInteger.Pow(10, -scale), 1);

            return Create(new[] { new SurdTerm(value, 1) }, false);
        }

        // the value when it is a rational, which is also every whole number
        public bool TryGetRational(out Rational value)
        {
            value = new Rational(0, 1);
            if (IsZero) return true;
            if (TimesPi || Terms.Count != 1 || Terms[0].Radicand != 1) return false;

            value = Terms[0].Coefficient;
            return true;
        }

        public bool IsRational => TryGetRational(out _);

        public double ToDouble()
        {
            double sum = 0;
            foreach (SurdTerm term in Terms)
            {
                double root = term.Radicand == 1 ? 1 : Math.Sqrt(term.Radicand);
                sum += term.Coefficient.ToDouble() * root;
            }

            return TimesPi ? sum * Math.PI : sum;
        }

        // one term decides its own sign; two are compared as doubles, which only a value within rounding
        // of zero could get wrong, and a nonzero value that close takes a twenty digit number to type
        public int Sign
        {
            get
            {
                if (IsZero) return 0;
                if (Terms.Count == 1) return Terms[0].Coefficient.Sign;

                return Math.Sign(ToDouble());
            }
        }


        // === arithmetic ===

        public static ExactValue? Add(ExactValue? left, ExactValue? right)
        {
            if (left == null || right == null) return null;
            if (left.IsZero) return right;
            if (right.IsZero) return left;
            if (left.TimesPi != right.TimesPi) return null;

            List<SurdTerm> terms = new List<SurdTerm>(left.Terms);
            terms.AddRange(right.Terms);

            return Create(terms, left.TimesPi);
        }

        public static ExactValue? Negate(ExactValue? value)
        {
            if (value == null) return null;

            return Scale(value, new Rational(-1, 1));
        }

        public static ExactValue? Subtract(ExactValue? left, ExactValue? right)
        {
            return Add(left, Negate(right));
        }

        // two roots multiply under one sign, and the square their radicands share comes out in front:
        // √6×√10 is √60, which is 2√15
        public static ExactValue? Multiply(ExactValue? left, ExactValue? right)
        {
            if (left == null || right == null) return null;
            if (left.IsZero || right.IsZero) return Zero;
            if (left.TimesPi && right.TimesPi) return null;

            List<SurdTerm> terms = new List<SurdTerm>();
            foreach (SurdTerm a in left.Terms)
            {
                foreach (SurdTerm b in right.Terms)
                {
                    long shared = Gcd(a.Radicand, b.Radicand);
                    long first = a.Radicand / shared;
                    long second = b.Radicand / shared;
                    if (first > MaxRadicand / second) return null;

                    Rational coefficient = a.Coefficient * b.Coefficient * new Rational(shared, 1);
                    terms.Add(new SurdTerm(coefficient, first * second));
                }
            }

            return Create(terms, left.TimesPi || right.TimesPi);
        }

        // a single root is divided by as its reciprocal, √s over c·s; a sum of two terms is multiplied by
        // its conjugate on both sides, which leaves a rational under the bar, so 1÷(√2+√3) is √3−√2
        //
        // null for a zero divisor, which the double path reports as the error it is
        public static ExactValue? Divide(ExactValue? left, ExactValue? right)
        {
            if (left == null || right == null || right.IsZero) return null;
            if (left.IsZero) return Zero;

            // π over π leaves the two rationals, and π under a bar with nothing to cancel it has no exact value
            if (right.TimesPi)
            {
                if (!left.TimesPi) return null;
                return Divide(WithoutPi(left), WithoutPi(right));
            }

            if (right.Terms.Count == 1)
            {
                SurdTerm term = right.Terms[0];
                Rational factor = new Rational(1, 1) / (term.Coefficient * new Rational(term.Radicand, 1));

                ExactValue reciprocal = new ExactValue(new List<SurdTerm> { new SurdTerm(factor, term.Radicand) }, false);
                return Multiply(left, reciprocal);
            }

            SurdTerm x = right.Terms[0];
            SurdTerm y = right.Terms[1];

            ExactValue conjugate = new ExactValue(
                new List<SurdTerm> { x, new SurdTerm(-y.Coefficient, y.Radicand) }, false);

            // x² − y², never zero: two different square free radicands cannot square to the same rational
            Rational under = x.Coefficient * x.Coefficient * new Rational(x.Radicand, 1)
                - y.Coefficient * y.Coefficient * new Rational(y.Radicand, 1);

            ExactValue? product = Multiply(left, conjugate);
            return product == null ? null : Scale(product, new Rational(1, 1) / under);
        }

        // a whole power, by squaring and multiplying, so every step goes through the limits
        //
        // zero to a power that is not positive has no value, which the double path reports
        public static ExactValue? Power(ExactValue? value, ExactValue? exponent)
        {
            if (value == null || exponent == null) return null;
            if (!exponent.TryGetRational(out Rational power) || !power.IsInteger) return null;

            BigInteger count = power.Numerator;
            if (count.IsZero) return value.IsZero ? null : FromInteger(1);
            if (value.IsZero) return count.Sign > 0 ? Zero : null;
            if (BigInteger.Abs(count) > MaxExponent) return null;

            if (count.Sign < 0) return Divide(FromInteger(1), Power(value, FromInteger(-count)));

            ExactValue? result = FromInteger(1);
            ExactValue? square = value;

            while (true)
            {
                if (!count.IsEven) result = Multiply(result, square);
                count >>= 1;
                if (count.IsZero || result == null) return result;

                square = Multiply(square, square);
                if (square == null) return null;
            }
        }

        // the root of a rational with its squares pulled out, √8 as 2√2 and √(1/2) as √2/2
        //
        // a root of anything else, a sum of roots or π, has no form here; a negative radicand has no real
        // root, which the double path reports
        public static ExactValue? SquareRoot(ExactValue? value)
        {
            if (value == null || !value.TryGetRational(out Rational radicand)) return null;
            if (radicand.IsZero) return Zero;
            if (radicand.Sign < 0) return null;

            // p/q is √(pq)/q; p and q share no factor, so neither do their square free parts
            if (!TrySplitSquare(radicand.Numerator, out BigInteger numeratorOutside, out long numeratorInside)) return null;
            if (!TrySplitSquare(radicand.Denominator, out BigInteger denominatorOutside, out long denominatorInside)) return null;
            if (numeratorInside > MaxRadicand / denominatorInside) return null;

            Rational coefficient = new Rational(numeratorOutside, denominatorOutside * denominatorInside);
            return Create(new[] { new SurdTerm(coefficient, numeratorInside * denominatorInside) }, false);
        }

        // an nth root only where it comes out rational, ∛8 as 2 and ∛(8/27) as 2/3; an odd root of a
        // negative number is the negative root of its magnitude
        public static ExactValue? Root(ExactValue? value, ExactValue? index)
        {
            if (value == null || index == null) return null;
            if (!index.TryGetRational(out Rational degree) || !degree.IsInteger) return null;

            if (degree.Numerator.IsOne) return value;
            if (degree.Numerator == 2) return SquareRoot(value);
            if (degree.Numerator < 3 || degree.Numerator > MaxBits) return null;

            if (!value.TryGetRational(out Rational radicand)) return null;

            int power = (int)degree.Numerator;
            bool negative = radicand.Sign < 0;
            if (negative && power % 2 == 0) return null;

            BigInteger numerator = BigInteger.Abs(radicand.Numerator);
            BigInteger top = IntegerRoot(numerator, power);
            BigInteger bottom = IntegerRoot(radicand.Denominator, power);

            if (BigInteger.Pow(top, power) != numerator || BigInteger.Pow(bottom, power) != radicand.Denominator)
            {
                return null;
            }

            return FromRational(new Rational(negative ? -top : top, bottom));
        }

        public static ExactValue? Abs(ExactValue? value)
        {
            if (value == null) return null;

            return value.Sign < 0 ? Negate(value) : value;
        }

        // every coefficient times the same rational
        private static ExactValue? Scale(ExactValue value, Rational factor)
        {
            List<SurdTerm> terms = new List<SurdTerm>();
            foreach (SurdTerm term in value.Terms)
            {
                terms.Add(new SurdTerm(term.Coefficient * factor, term.Radicand));
            }

            return Create(terms, value.TimesPi);
        }

        private static ExactValue WithoutPi(ExactValue value)
        {
            return new ExactValue(new List<SurdTerm>(value.Terms), false);
        }


        // === building ===

        // merges the terms under one radicand, drops the ones that cancelled and sorts the rest into the
        // order a Casio writes them in; null past a limit, or for π with anything but a rational beside it
        private static ExactValue? Create(IEnumerable<SurdTerm> terms, bool timesPi)
        {
            Dictionary<long, Rational> merged = new Dictionary<long, Rational>();
            foreach (SurdTerm term in terms)
            {
                merged[term.Radicand] = merged.TryGetValue(term.Radicand, out Rational sum)
                    ? sum + term.Coefficient
                    : term.Coefficient;
            }

            List<SurdTerm> result = new List<SurdTerm>();
            foreach (KeyValuePair<long, Rational> entry in merged)
            {
                if (entry.Value.IsZero) continue;
                if (entry.Key > MaxRadicand || TooLarge(entry.Value)) return null;

                result.Add(new SurdTerm(entry.Value, entry.Key));
            }

            if (result.Count == 0) return Zero;
            if (result.Count > MaxTerms) return null;
            if (timesPi && (result.Count != 1 || result[0].Radicand != 1)) return null;

            // the rational term first, then the roots from the largest radicand down
            result.Sort((a, b) => Order(b.Radicand).CompareTo(Order(a.Radicand)));

            return new ExactValue(result, timesPi);
        }

        private static long Order(long radicand)
        {
            return radicand == 1 ? long.MaxValue : radicand;
        }

        private static bool TooLarge(Rational value)
        {
            return BigInteger.Abs(value.Numerator).GetBitLength() > MaxBits || value.Denominator.GetBitLength() > MaxBits;
        }

        // n as a square times a square free number, √n being the root of the first times the root of the
        // second; false for a number too large to search that is not a square as a whole
        //
        // the trial division only has to reach the cube root of what is left: past it, what is left has at
        // most two prime factors, and is then either their square or square free
        private static bool TrySplitSquare(BigInteger n, out BigInteger outside, out long inside)
        {
            BigInteger root = IntegerRoot(n, 2);
            if (root * root == n)
            {
                outside = root;
                inside = 1;
                return true;
            }

            outside = 0;
            inside = 0;
            if (n > MaxRadicand) return false;

            long remaining = (long)n;
            long square = 1;
            long free = 1;

            for (long divisor = 2; divisor * divisor * divisor <= remaining; divisor++)
            {
                int count = 0;
                while (remaining % divisor == 0)
                {
                    remaining /= divisor;
                    count++;
                }

                for (int pair = 0; pair < count / 2; pair++) square *= divisor;
                if (count % 2 == 1) free *= divisor;
            }

            long rest = (long)IntegerRoot(remaining, 2);
            if (rest * rest == remaining) square *= rest;
            else free *= remaining;

            outside = square;
            inside = free;
            return true;
        }

        // the whole kth root of n, rounded down, by Newton steps from a start above it
        private static BigInteger IntegerRoot(BigInteger n, int k)
        {
            if (n.IsZero) return BigInteger.Zero;

            BigInteger x = BigInteger.One << (int)((n.GetBitLength() + k - 1) / k);
            while (true)
            {
                BigInteger next = ((k - 1) * x + n / BigInteger.Pow(x, k - 1)) / k;
                if (next >= x) return x;

                x = next;
            }
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0) (a, b) = (b, a % b);

            return a;
        }


        // === equality ===

        public bool Equals(ExactValue? other)
        {
            if (other == null || other.TimesPi != TimesPi || other.Terms.Count != Terms.Count) return false;

            for (int index = 0; index < Terms.Count; index++)
            {
                if (Terms[index].Radicand != other.Terms[index].Radicand) return false;
                if (!Terms[index].Coefficient.Equals(other.Terms[index].Coefficient)) return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ExactValue other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(TimesPi);
            foreach (SurdTerm term in Terms)
            {
                hash.Add(term.Coefficient);
                hash.Add(term.Radicand);
            }

            return hash.ToHashCode();
        }
    }
}
