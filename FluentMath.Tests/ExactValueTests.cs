using System;
using System.Numerics;
using FluentMath.Models;
using Xunit;

namespace FluentMath.Tests
{
    // the exact path on its own: which values it can hold, the one shape it keeps each of them in, and
    // where it lets go and leaves the double alone
    public class ExactValueTests
    {
        // === helpers ===

        private static ExactValue Whole(long value) => ExactValue.FromInteger(value);

        private static ExactValue Fraction(long numerator, long denominator)
        {
            return ExactValue.FromRational(new Rational(numerator, denominator));
        }

        private static ExactValue Root(long radicand) => ExactValue.SquareRoot(Whole(radicand))!;


        // === rationals ===

        [Fact]
        public void KeepsAFractionInLowestTermsWithTheSignOnTop()
        {
            Rational value = new Rational(6, -8);

            Assert.Equal(-3, value.Numerator);
            Assert.Equal(4, value.Denominator);
        }

        [Fact]
        public void ReadsADecimalAsTheFractionItIs()
        {
            Assert.Equal(Fraction(1, 10), ExactValue.FromDecimal("0.1"));
            Assert.Equal(Fraction(-5, 4), ExactValue.FromDecimal("-1.25"));
            Assert.Equal(Whole(1230), ExactValue.FromDecimal("1.23", 3));
            Assert.Equal(Fraction(1, 1000), ExactValue.FromDecimal("1", -3));

            Assert.Null(ExactValue.FromDecimal("."));
            Assert.Null(ExactValue.FromDecimal("1.2.3"));
        }

        // 0.1 + 0.2 is 0.30000000000000004 as doubles and a plain 3/10 here
        [Fact]
        public void AddsFractionsWithoutRoundingNoise()
        {
            ExactValue? sum = ExactValue.Add(ExactValue.FromDecimal("0.1"), ExactValue.FromDecimal("0.2"));

            Assert.Equal(Fraction(3, 10), sum);
            Assert.Equal(0.3, sum!.ToDouble());
        }

        // each side is cut before it becomes a double, so a fraction of two huge numbers is still read
        [Fact]
        public void ReadsAFractionOfTwoHugeNumbersAsADouble()
        {
            BigInteger large = BigInteger.Pow(10, 300);
            Rational value = new Rational(large * 3 + 1, large);

            Assert.Equal(3, value.ToDouble(), 12);
        }


        // === roots ===

        [Fact]
        public void PullsTheSquaresOutOfARoot()
        {
            ExactValue root = Root(8);

            SurdTerm term = Assert.Single(root.Terms);
            Assert.Equal(new Rational(2, 1), term.Coefficient);
            Assert.Equal(2, term.Radicand);
        }

        [Fact]
        public void TakesTheRootOfAFractionWithARationalDenominator()
        {
            ExactValue root = ExactValue.SquareRoot(Fraction(1, 2))!;

            SurdTerm term = Assert.Single(root.Terms);
            Assert.Equal(new Rational(1, 2), term.Coefficient);
            Assert.Equal(2, term.Radicand);
        }

        [Fact]
        public void TakesTheRootOfALargeSquareAsAWhole()
        {
            ExactValue square = ExactValue.FromInteger(BigInteger.Pow(10, 40));

            Assert.Equal(ExactValue.FromInteger(BigInteger.Pow(10, 20)), ExactValue.SquareRoot(square));
        }

        // a large number that is no square would need a search too long to be worth it
        [Fact]
        public void GivesUpOnTheRootOfALargeNumberThatIsNoSquare()
        {
            Assert.Null(ExactValue.SquareRoot(ExactValue.FromInteger(BigInteger.Pow(10, 20) + 1)));
        }

        [Fact]
        public void MultipliesTwoRootsUnderOneSign()
        {
            Assert.Equal(Root(6), ExactValue.Multiply(Root(2), Root(3)));
            Assert.Equal(Whole(2), ExactValue.Multiply(Root(2), Root(2)));

            // √6×√10 is √60, which is 2√15
            ExactValue product = ExactValue.Multiply(Root(6), Root(10))!;
            Assert.Equal(new Rational(2, 1), product.Terms[0].Coefficient);
            Assert.Equal(15, product.Terms[0].Radicand);
        }

        [Fact]
        public void DividesBySumOfTwoRootsThroughItsConjugate()
        {
            ExactValue quotient = ExactValue.Divide(Whole(1), ExactValue.Add(Root(2), Root(3)))!;

            Assert.Equal(ExactValue.Subtract(Root(3), Root(2)), quotient);
        }

        [Fact]
        public void TakesAnNthRootOnlyWhereItComesOutRational()
        {
            Assert.Equal(Whole(2), ExactValue.Root(Whole(8), Whole(3)));
            Assert.Equal(Fraction(-2, 3), ExactValue.Root(Fraction(-8, 27), Whole(3)));

            Assert.Null(ExactValue.Root(Whole(2), Whole(3)));
            Assert.Null(ExactValue.Root(Whole(-16), Whole(4)));
        }


        // === the shape of a value ===

        // a Casio shows two terms at most, and √2+√3+√5 is a decimal there
        [Fact]
        public void LetsGoOfAThirdTerm()
        {
            Assert.Null(ExactValue.Add(Root(2), ExactValue.Add(Root(3), Root(5))));
        }

        // the rational term first, then the roots from the largest radicand down
        [Fact]
        public void PutsTheRationalTermInFront()
        {
            ExactValue value = ExactValue.Add(Root(3), Whole(-2))!;

            Assert.Equal(1, value.Terms[0].Radicand);
            Assert.Equal(3, value.Terms[1].Radicand);

            ExactValue roots = ExactValue.Add(Root(2), Root(3))!;
            Assert.Equal(3, roots.Terms[0].Radicand);
            Assert.Equal(2, roots.Terms[1].Radicand);
        }

        [Fact]
        public void DropsTermsThatCancel()
        {
            ExactValue difference = ExactValue.Subtract(ExactValue.Add(Root(2), Whole(1)), Root(2))!;

            Assert.Equal(Whole(1), difference);
            Assert.True(ExactValue.Subtract(Root(5), Root(5))!.IsZero);
        }

        [Fact]
        public void KeepsPiOnlyTimesARational()
        {
            Assert.Equal(ExactValue.Pi, ExactValue.Divide(ExactValue.Multiply(ExactValue.Pi, Whole(2)), Whole(2)));
            Assert.Equal(Whole(3), ExactValue.Divide(ExactValue.Multiply(ExactValue.Pi, Whole(3)), ExactValue.Pi));

            Assert.Null(ExactValue.Add(ExactValue.Pi, Whole(1)));
            Assert.Null(ExactValue.Multiply(ExactValue.Pi, Root(2)));
            Assert.Null(ExactValue.Multiply(ExactValue.Pi, ExactValue.Pi));
            Assert.Null(ExactValue.Divide(Whole(1), ExactValue.Pi));
        }


        // === powers ===

        [Fact]
        public void RaisesToAWholePower()
        {
            ExactValue sum = ExactValue.Add(Root(2), Root(3))!;

            Assert.Equal(ExactValue.Add(Whole(5), ExactValue.Multiply(Whole(2), Root(6))), ExactValue.Power(sum, Whole(2)));
            Assert.Equal(Fraction(1, 8), ExactValue.Power(Whole(2), Whole(-3)));
            Assert.Equal(Whole(1), ExactValue.Power(ExactValue.Pi, Whole(0)));
        }

        [Fact]
        public void RaisesOnlyToAWholePower()
        {
            Assert.Null(ExactValue.Power(Whole(4), Fraction(1, 2)));
            Assert.Null(ExactValue.Power(Whole(2), ExactValue.Pi));
        }

        // zero to a power that is not positive has no value, which the double path reports
        [Fact]
        public void LeavesZeroToNothingToTheDouble()
        {
            Assert.Null(ExactValue.Power(ExactValue.Zero, Whole(0)));
            Assert.Null(ExactValue.Power(ExactValue.Zero, Whole(-1)));
            Assert.Equal(ExactValue.Zero, ExactValue.Power(ExactValue.Zero, Whole(3)));
        }

        // past the limits a value lets go of the exact path, and the double carries on alone
        [Fact]
        public void LetsGoOfAValueTooLargeToKeep()
        {
            Assert.NotNull(ExactValue.Power(Whole(2), Whole(1000)));
            Assert.Null(ExactValue.Power(Whole(2), Whole(1100)));
            Assert.Null(ExactValue.Power(Whole(2), ExactValue.FromInteger(BigInteger.Pow(10, 9))));
        }


        // === everything is optional ===

        [Fact]
        public void PassesAMissingValueOn()
        {
            Assert.Null(ExactValue.Add(null, Whole(1)));
            Assert.Null(ExactValue.Multiply(Whole(1), null));
            Assert.Null(ExactValue.Divide(Whole(1), ExactValue.Zero));
            Assert.Null(ExactValue.SquareRoot(Whole(-4)));
        }

        [Fact]
        public void ReadsTheSignOfATwoTermValue()
        {
            Assert.Equal(1, ExactValue.Subtract(Whole(2), Root(3))!.Sign);
            Assert.Equal(-1, ExactValue.Subtract(Root(2), Root(3))!.Sign);
            Assert.Equal(Math.Sqrt(3) - Math.Sqrt(2), ExactValue.Abs(ExactValue.Subtract(Root(2), Root(3)))!.ToDouble(), 15);
        }
    }
}
