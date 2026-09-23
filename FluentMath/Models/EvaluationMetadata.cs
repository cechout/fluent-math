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


    // which shape a finished result is shown in; the S to D key cycles through them
    // a value only has the last two when a fraction was found for it at all
    public enum AnswerForm
    {
        Decimal,
        Improper,
        Mixed
    }


    // the outcome of one evaluation; a failure carries no value, so IsSuccess has to be checked first
    //
    // deliberately not an exception: half-typed input is the normal state here rather than an
    // exceptional one, and the display has to survive being asked to evaluate it
    public readonly struct EvaluationResult
    {
        public bool IsSuccess { get; }
        public double Value { get; }
        public EvaluationError Error { get; }

        private EvaluationResult(bool isSuccess, double value, EvaluationError error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public static EvaluationResult Success(double value)
        {
            return new EvaluationResult(true, value, EvaluationError.None);
        }

        public static EvaluationResult Failure(EvaluationError error)
        {
            return new EvaluationResult(false, 0, error);
        }
    }
}
