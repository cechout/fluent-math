namespace FluentMath.Models
{
    // what went wrong, in the categories a calculator display can actually name
    // Syntax means the formula does not parse, everything else means it parses but has no real result
    //
    // Argument is the one a Casio names on its own: a pair of bounds that is the wrong way round or not
    // made of whole numbers, as in RanInt#(6,1)
    public enum EvaluationError
    {
        None,
        Syntax,
        DivideByZero,
        Domain,
        Overflow,
        Argument
    }


    // whether the trigonometric functions read their argument as degrees, radians or gradians
    // Degrees is the default, same as a Casio out of the box
    public enum AngleMode
    {
        Degrees,
        Radians,
        Gradians
    }


    // which shape a finished result is shown in; the S to D key cycles through the first three
    // a value only has the fractions when a fraction was found for it at all, and the prime factors only
    // when it is a whole number above zero, which the FACT key asks for
    public enum AnswerForm
    {
        Decimal,
        Improper,
        Mixed,
        PrimeFactors
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


    // the outcome of one evaluation; a failure carries no value, so IsSuccess has to be checked first
    //
    // deliberately not an exception: half-typed input is the normal state here rather than an
    // exceptional one, and the display has to survive being asked to evaluate it
    public readonly struct EvaluationResult
    {
        public bool IsSuccess { get; }
        public double Value { get; } // the first value of a pair, which is also what Ans takes
        public EvaluationError Error { get; }

        public ResultKind Kind { get; }
        public double Second { get; } // the remainder, the angle or y; 0 for a single value

        private EvaluationResult(bool isSuccess, double value, EvaluationError error, ResultKind kind, double second)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            Kind = kind;
            Second = second;
        }

        public static EvaluationResult Success(double value)
        {
            return new EvaluationResult(true, value, EvaluationError.None, ResultKind.Single, 0);
        }

        public static EvaluationResult Pair(ResultKind kind, double first, double second)
        {
            return new EvaluationResult(true, first, EvaluationError.None, kind, second);
        }

        public static EvaluationResult Failure(EvaluationError error)
        {
            return new EvaluationResult(false, 0, error, ResultKind.Single, 0);
        }
    }
}
