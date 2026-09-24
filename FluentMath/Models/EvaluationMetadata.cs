using System;
using System.Numerics;

namespace FluentMath.Models
{
    // what went wrong, in the categories a calculator display can actually name
    // Syntax means the formula does not parse, everything else means it parses but has no real result
    //
    // Argument is the one a Casio names on its own: a pair of bounds that is the wrong way round or not
    // made of whole numbers, as in RanInt#(6,1)
    //
    // TimeOut is the other one: a calculus structure that ran out of the work it may do before its answer
    // was good enough
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


    // whether the trigonometric functions read their argument as degrees, radians or gradians
    // Degrees is the default, same as a Casio out of the box
    public enum AngleMode
    {
        Degrees,
        Radians,
        Gradians
    }


    // which shape a finished result is shown in; the S to D key cycles the exact form, the recurring
    // decimal and the decimal, and its shift swaps the exact form between improper and mixed
    // the exact form is a fraction for a rational and a form with roots or π otherwise, drawn the same
    // under both names, since only a fraction has a whole part to split off
    // a value only has a form when one was found for it at all, and the prime factors only when it is a
    // whole number above zero, which the FACT key asks for; the ENG keys ask for the engineering form, a
    // mantissa over a power of ten that is a multiple of three, and the °′″ key for degrees, minutes and
    // seconds
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


    // a result is one value, or the pair a Casio shows for a division with remainder and for Pol and Rec
    //
    // the pair only exists when that operation is the whole calculation; anywhere inside one it hands on
    // its first value, and so does Ans
    public enum ResultKind
    {
        Single,
        QuotientRemainder,
        Polar,
        Rectangular
    }


    // a number as the evaluator carries it: the double every calculation has, and beside it the exact
    // value, for as long as every step that led to it was exact
    //
    // when the exact value is known the double is read off it, so the two never disagree about a zero or
    // a whole number: √2×√2 is 2 in both, and 1÷(√2×√2−2) is the Math ERROR it is on a Casio
    // a double on its own converts into one without an exact value, so a constant written into the
    // arithmetic below as a plain number has to be made exact on purpose, see Whole
    public readonly struct MathValue
    {
        public double Value { get; }
        public ExactValue? Exact { get; }

        // whether the value is an angle in degrees, minutes and seconds, which a result is shown as; the
        // evaluator decides which operations keep it one, and every operation below drops it except a
        // sign, since −2°30′ is still an angle
        public bool IsSexagesimal { get; }

        public MathValue(double value, ExactValue? exact = null) : this(value, exact, false) { }

        private MathValue(double value, ExactValue? exact, bool sexagesimal)
        {
            Value = exact != null ? exact.ToDouble() : value;
            Exact = exact;
            IsSexagesimal = sexagesimal;
        }

        public MathValue AsSexagesimal(bool sexagesimal) => new MathValue(Value, Exact, sexagesimal);

        // a whole number with its exact value, for the functions whose answer is always whole; past the
        // range a double holds every whole number in, the digits are no longer the value
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


    // the outcome of one evaluation; a failure carries no value, so IsSuccess has to be checked first
    //
    // deliberately not an exception: half-typed input is the normal state here rather than an
    // exceptional one, and the display has to survive being asked to evaluate it
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
