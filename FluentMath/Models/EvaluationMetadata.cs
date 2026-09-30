using System;
using System.Numerics;

namespace FluentMath.Models
{
    // what went wrong, in the categories a calculator display names
    // Syntax does not parse; the rest parse but have no real result
    // Argument: bounds the wrong way round or not whole, as in RanInt#(6,1)
    // TimeOut: a calculus structure that ran out of work before its answer was good enough
    public enum EvaluationError
    {
        None,
        Syntax,
        DivideByZero,
        Domain,
        Overflow,
        Argument,
        TimeOut
    }


    // the unit the trigonometric functions read; degrees by default, as on a Casio
    public enum AngleMode
    {
        Degrees,
        Radians,
        Gradians
    }


    // the shape a finished result is shown in; S⇔D cycles exact, recurring and decimal, its shift swaps
    // improper and mixed
    // (the exact form is a fraction, or roots and π, drawn alike under both names; PrimeFactors is FACT,
    // Engineering the ENG keys, Sexagesimal the °′″ key)
    public enum AnswerForm
    {
        Decimal,
        Improper,
        Mixed,
        Recurring,
        PrimeFactors,
        Engineering,
        Sexagesimal
    }


    // one value, or the pair a Casio shows for a division with remainder and for Pol and Rec
    // (only as the whole calculation; inside one, and in Ans, the pair hands on its first value)
    public enum ResultKind
    {
        Single,
        QuotientRemainder,
        Polar,
        Rectangular
    }


    // a number as the evaluator carries it: the double, and the exact value while every step was exact
    //
    // with an exact value the double is read off it, so √2×√2 is 2 in both and 1÷(√2×√2−2) a Math ERROR
    // (a plain double converts without an exact value; a constant is made exact on purpose, see Whole)
    public readonly struct MathValue
    {
        public double Value { get; }
        public ExactValue? Exact { get; }

        // an angle in degrees, minutes and seconds; every operation below drops it but the sign (−2°30′)
        public bool IsSexagesimal { get; }

        public MathValue(double value, ExactValue? exact = null) : this(value, exact, false) { }

        private MathValue(double value, ExactValue? exact, bool sexagesimal)
        {
            Value = exact != null ? exact.ToDouble() : value;
            Exact = exact;
            IsSexagesimal = sexagesimal;
        }

        public MathValue AsSexagesimal(bool sexagesimal) => new MathValue(Value, Exact, sexagesimal);

        // a whole number with its exact value; past 2^53 the digits are no longer the value
        public static MathValue Whole(double value)
        {
            if (Math.Abs(value) >= 9007199254740992) return new MathValue(value);

            return new MathValue(value, ExactValue.FromInteger(new BigInteger(value)));
        }

        public static implicit operator MathValue(double value) => new MathValue(value);

        public static MathValue operator +(MathValue left, MathValue right)
        {
            return new MathValue(left.Value + right.Value, ExactValue.Add(left.Exact, right.Exact));
        }

        public static MathValue operator -(MathValue left, MathValue right)
        {
            return new MathValue(left.Value - right.Value, ExactValue.Subtract(left.Exact, right.Exact));
        }

        public static MathValue operator -(MathValue value)
        {
            return new MathValue(-value.Value, ExactValue.Negate(value.Exact), value.IsSexagesimal);
        }

        public static MathValue operator *(MathValue left, MathValue right)
        {
            return new MathValue(left.Value * right.Value, ExactValue.Multiply(left.Exact, right.Exact));
        }

        // the caller has ruled out a zero divisor
        public static MathValue operator /(MathValue left, MathValue right)
        {
            return new MathValue(left.Value / right.Value, ExactValue.Divide(left.Exact, right.Exact));
        }
    }


    // the outcome of one evaluation; check IsSuccess first, a failure carries no value
    // (not an exception; half-typed input is the normal state here)
    public readonly struct EvaluationResult
    {
        public bool IsSuccess { get; }
        public EvaluationError Error { get; }
        public ResultKind Kind { get; }

        public MathValue FirstValue { get; } // the first value of a pair, which is also what Ans takes
        public MathValue SecondValue { get; } // the remainder, the angle or y; 0 for a single value

        public double Value => FirstValue.Value;
        public double Second => SecondValue.Value;

        private EvaluationResult(bool isSuccess, MathValue value, EvaluationError error, ResultKind kind, MathValue second)
        {
            IsSuccess = isSuccess;
            FirstValue = value;
            Error = error;
            Kind = kind;
            SecondValue = second;
        }

        public static EvaluationResult Success(MathValue value)
        {
            return new EvaluationResult(true, value, EvaluationError.None, ResultKind.Single, 0);
        }

        public static EvaluationResult Pair(ResultKind kind, MathValue first, MathValue second)
        {
            return new EvaluationResult(true, first, EvaluationError.None, kind, second);
        }

        public static EvaluationResult Failure(EvaluationError error)
        {
            return new EvaluationResult(false, 0, error, ResultKind.Single, 0);
        }
    }
}
