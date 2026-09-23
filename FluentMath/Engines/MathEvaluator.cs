using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FluentMath.Models;

namespace FluentMath.Engines
{
    // turns the token tree MathInputManager owns into an actual number
    //
    // recursive descent straight over the token lists, with no string in between; a structured token is
    // just an atom that evaluates its own child lists through the same entry point, which is what lets a
    // fraction inside an exponent inside a root work without a single special case
    //
    // the grammar is the usual precedence ladder:
    //   expression := term (plus or minus, term)*
    //   term       := product (times or divided by, product)*
    //   product    := unary (implicit, postfix)*
    //   unary      := sign* postfix
    //   postfix    := atom (factorial or reciprocal or percent)*
    //   atom       := number | constant | bracketed expression | fraction | power | root | function
    //                  | logarithm
    //
    // nothing here throws; a failure sets _error and the recursion unwinds on its own, because a
    // half-typed formula is the normal state of the input rather than an exceptional one
    public class MathEvaluator
    {
        // === fields ===

        // settable so one evaluator can follow the mode the user picks without being rebuilt; the
        // constructor argument stays for callers that only ever want one unit
        public AngleMode AngleMode { get; set; }

        // set the moment any sub-expression fails; every loop checks it so the parse stops early
        private EvaluationError _error;

        // what an Ans token resolves to; the caller sets it after every successful evaluation, so a
        // formula continuing from the previous result carries the full double rather than the digits
        // the display happened to show
        public double LastAnswer { get; set; }

        // --- trigonometric results ---
        private const double TrigNoisePerRadian = 1e-14; // the most a zero of sin or cos comes out as, per radian of angle
        private const int TrigDigits = 15;               // significant digits a trigonometric result is kept to


        // === constructor ===

        public MathEvaluator(AngleMode angleMode = AngleMode.Degrees)
        {
            AngleMode = angleMode;
        }


        // === entry point ===

        public EvaluationResult Evaluate(IReadOnlyList<MathToken> tokens)
        {
            _error = EvaluationError.None;

            if (tokens.Count == 0) return EvaluationResult.Success(0); // empty input reads as 0, same as the display

            int position = 0;
            double value = ParseExpression(tokens, ref position);

            if (_error != EvaluationError.None) return EvaluationResult.Failure(_error);

            // leftover tokens mean the list did not parse as one expression, a stray closing bracket say
            if (position < tokens.Count) return EvaluationResult.Failure(EvaluationError.Syntax);

            if (double.IsNaN(value)) return EvaluationResult.Failure(EvaluationError.Domain);
            if (double.IsInfinity(value)) return EvaluationResult.Failure(EvaluationError.Overflow);

            return EvaluationResult.Success(value);
        }


        // === grammar ===

        private double ParseExpression(IReadOnlyList<MathToken> tokens, ref int position)
        {
            double value = ParseTerm(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "+" && token.Value != "-") break;

                position++;
                double right = ParseTerm(tokens, ref position);

                if (token.Value == "+") { value += right; }
                else { value -= right; }
            }

            return value;
        }

        private double ParseTerm(IReadOnlyList<MathToken> tokens, ref int position)
        {
            double value = ParseProduct(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "*" && token.Value != "/") break;

                position++;
                double right = ParseProduct(tokens, ref position);
                if (_error != EvaluationError.None) return 0;

                if (token.Value == "*")
                {
                    value *= right;
                }
                else
                {
                    if (right == 0) return Fail(EvaluationError.DivideByZero);
                    value /= right;
                }
            }

            return value;
        }

        // a product written without a sign binds tighter than times and divided by, the way it does on a
        // Casio: 6÷2(1+2) divides by the whole of 2(1+2) and is 1, and 1÷2π is one over two pi
        private double ParseProduct(IReadOnlyList<MathToken> tokens, ref int position)
        {
            double value = ParseUnary(tokens, ref position);

            // no operator but something that can start an atom: implicit multiplication, so that
            // 3(4+5) and 2sin(30) work the way they do on paper
            //
            // through the postfix level rather than straight to the atom, the same way the explicit
            // times and divided by reach it; going to the atom left the factorial in 2(3)!
            // lying in the list with nothing to consume it, and the formula came back as a syntax
            // error instead of 12
            while (_error == EvaluationError.None && position < tokens.Count && StartsAtom(tokens[position]))
            {
                value *= ParsePostfix(tokens, ref position);
            }

            return value;
        }

        // a leading minus is a sign rather than a subtraction, which is the only reason a formula is
        // allowed to start with one at all
        private double ParseUnary(IReadOnlyList<MathToken> tokens, ref int position)
        {
            if (position < tokens.Count && tokens[position].Type == TokenType.Operator)
            {
                string op = tokens[position].Value;

                if (op == "-")
                {
                    position++;
                    return -ParseUnary(tokens, ref position);
                }
                if (op == "+")
                {
                    position++;
                    return ParseUnary(tokens, ref position);
                }
            }

            return ParsePostfix(tokens, ref position);
        }

        // a postfix key binds tighter than a sign standing in front of it, so -5! is the negative of
        // 5 factorial rather than the factorial of -5, which has none
        private double ParsePostfix(IReadOnlyList<MathToken> tokens, ref int position)
        {
            double value = ParseAtom(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Postfix) break;

                position++;
                value = ApplyPostfix(token.Value, value);
            }

            return value;
        }

        private double ParseAtom(IReadOnlyList<MathToken> tokens, ref int position)
        {
            if (position >= tokens.Count) return Fail(EvaluationError.Syntax);

            MathToken token = tokens[position];

            // structured tokens carry their operands inside themselves, so they need no lookahead at all
            switch (token)
            {
                case FractionToken fraction:
                    position++;
                    return EvaluateFraction(fraction);

                case PowerToken power:
                    position++;
                    return EvaluatePower(power);

                case RootToken root:
                    position++;
                    return EvaluateRoot(root);

                case LogarithmToken logarithm:
                    position++;
                    return EvaluateLogarithm(logarithm);

                case FunctionToken function:
                    position++;
                    return EvaluateFunction(function);

                case ConstantToken constant:
                    position++;
                    return constant.NumericValue;

                case AnsToken:
                    position++;
                    return LastAnswer;
            }

            if (token.Type == TokenType.Number)
            {
                // the input keeps one token per character, so the whole run is what makes up one value
                // reading it greedily is also what stops the implicit multiplication above from turning
                // 45 into 4 times 5
                var literal = new StringBuilder();
                while (position < tokens.Count && tokens[position].Type == TokenType.Number)
                {
                    literal.Append(tokens[position].Value);
                    position++;
                }

                if (!double.TryParse(literal.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    return Fail(EvaluationError.Syntax); // a lone decimal point is the realistic case
                }

                // the EXP key belongs to the number it follows rather than to the expression around it,
                // so it is consumed here inside the atom; that is what makes 1 / 3 EXP 5 one over three
                // hundred thousand instead of a third of a hundred thousand
                return number;
            }

            if (token.Type == TokenType.BracketOpen)
            {
                position++;
                double inner = ParseExpression(tokens, ref position);
                if (_error != EvaluationError.None) return 0;

                // an unclosed bracket is not an error, a calculator closes whatever is still open on =
                if (position < tokens.Count && tokens[position].Type == TokenType.BracketClose) position++;

                return inner;
            }

            return Fail(EvaluationError.Syntax);
        }

        private static bool StartsAtom(MathToken token)
        {
            if (token.Type == TokenType.Number) return true;
            if (token.Type == TokenType.Constant) return true;
            if (token.Type == TokenType.BracketOpen) return true;

            return token is FractionToken
                || token is PowerToken
                || token is RootToken
                || token is FunctionToken
                || token is LogarithmToken
                || token is AnsToken;
        }


        // === reading ===

        // a copy of the formula with a bracket pair around every product that binds tighter than the
        // divided by in front of it, which is how the history line shows the way the formula was read
        //
        // a Casio rewrites the input itself on =, 6÷2(1+2) into 6÷(2(1+2)); here the tree stays exactly as
        // it was typed and only the copy the history line draws carries the brackets
        // the scan below follows ParseProduct and has to move with it
        public static List<MathToken> CloneWithImpliedBrackets(IReadOnlyList<MathToken> tokens)
        {
            List<MathToken> copy = MathTokenCloner.CloneList(tokens);
            InsertImpliedBrackets(copy);

            return copy;
        }

        private static void InsertImpliedBrackets(List<MathToken> tokens)
        {
            foreach (MathToken token in tokens)
            {
                foreach (TokenSlot slot in MathInputManager.GetSlots(token)) InsertImpliedBrackets(slot.Tokens);
            }

            for (int index = 0; index < tokens.Count; index++)
            {
                if (tokens[index].Type != TokenType.Operator || tokens[index].Value != "/") continue;

                int start = index + 1;
                int end = ProductEnd(tokens, start, out int factors, out int unclosed);
                if (factors < 2) continue;

                // a bracket the user left open inside the product is closed first, or the new closing
                // bracket would pair off with it instead of with the one opened here
                for (int count = 0; count <= unclosed; count++)
                {
                    tokens.Insert(end, new MathToken(TokenType.BracketClose, ")"));
                }

                tokens.Insert(start, new MathToken(TokenType.BracketOpen, "("));
            }
        }

        // where the product starting at start ends, and how many factors it has; signs belong to the
        // first factor, the way ParseUnary reads them
        private static int ProductEnd(List<MathToken> tokens, int start, out int factors, out int unclosed)
        {
            int position = start;
            factors = 0;
            unclosed = 0;

            while (position < tokens.Count && IsSign(tokens[position])) position++;

            while (position < tokens.Count && StartsAtom(tokens[position]))
            {
                position = AtomEnd(tokens, position, ref unclosed);
                while (position < tokens.Count && tokens[position].Type == TokenType.Postfix) position++;

                factors++;
            }

            return position;
        }

        private static bool IsSign(MathToken token)
        {
            return token.Type == TokenType.Operator && (token.Value == "+" || token.Value == "-");
        }

        // a run of digits is one atom and a bracket group reaches to its partner, the same way ParseAtom
        // reads them; a group nobody closed reaches to the end of the list
        private static int AtomEnd(List<MathToken> tokens, int position, ref int unclosed)
        {
            if (tokens[position].Type == TokenType.Number)
            {
                while (position < tokens.Count && tokens[position].Type == TokenType.Number) position++;
                return position;
            }

            if (tokens[position].Type != TokenType.BracketOpen) return position + 1;

            int depth = 0;
            for (int index = position; index < tokens.Count; index++)
            {
                if (tokens[index].Type == TokenType.BracketOpen) depth++;
                if (tokens[index].Type != TokenType.BracketClose) continue;

                depth--;
                if (depth == 0) return index + 1;
            }

            unclosed += depth;
            return tokens.Count;
        }


        // === postfix ===

        private double ApplyPostfix(string kind, double value)
        {
            switch (kind)
            {
                case "!":
                    return Factorial(value);

                case "inv":
                    if (value == 0) return Fail(EvaluationError.DivideByZero);
                    return 1.0 / value;

                // plain division by a hundred, which is the meaning the FX-991 gives the key; the add-on
                // percent of a business calculator, where 200 + 10% comes out as 220, is deliberately
                // not what this does
                case "%":
                    return value / 100.0;
            }

            return Fail(EvaluationError.Syntax);
        }

        // only a whole count that is not negative has one, and 171! is already past the range of a
        // double, so the ceiling is checked here rather than left to come back as an infinity
        private double Factorial(double value)
        {
            if (value < 0 || value != Math.Floor(value)) return Fail(EvaluationError.Domain);
            if (value > 170) return Fail(EvaluationError.Overflow);

            double result = 1;
            for (int factor = 2; factor <= (int)value; factor++)
            {
                result *= factor;
            }

            return result;
        }


        // === structured tokens ===

        // every slot of a structured token is a complete expression of its own
        //
        // an empty slot is a syntax error here; the two slots that have a sensible default instead, the
        // root index and the logarithm base, are checked by their caller before it ever gets this far
        private double EvaluateSlot(List<MathToken> tokens)
        {
            if (tokens.Count == 0) return Fail(EvaluationError.Syntax);

            int position = 0;
            double value = ParseExpression(tokens, ref position);

            if (_error == EvaluationError.None && position < tokens.Count) return Fail(EvaluationError.Syntax);
            return value;
        }

        private double EvaluateFraction(FractionToken fraction)
        {
            double numerator = EvaluateSlot(fraction.NumeratorTokens);
            double denominator = EvaluateSlot(fraction.DenominatorTokens);
            if (_error != EvaluationError.None) return 0;

            if (denominator == 0) return Fail(EvaluationError.DivideByZero);
            return numerator / denominator;
        }

        private double EvaluatePower(PowerToken power)
        {
            double baseValue = EvaluateSlot(power.BaseTokens);
            double exponent = EvaluateSlot(power.ExponentTokens);
            if (_error != EvaluationError.None) return 0;

            // zero to the zero and zero to a negative power are both Math ERROR on an FX-991; Math.Pow
            // answers 1 and an infinity instead, neither of which a calculator should show
            if (baseValue == 0 && exponent <= 0) return Fail(EvaluationError.Domain);

            double result = Math.Pow(baseValue, exponent);
            if (double.IsNaN(result)) return Fail(EvaluationError.Domain); // negative base with a fractional exponent

            return result;
        }

        private double EvaluateRoot(RootToken root)
        {
            // an empty index is a plain square root, which is exactly what the display shows for it
            double index = 2;
            if (root.IndexTokens.Count > 0) index = EvaluateSlot(root.IndexTokens);

            double radicand = EvaluateSlot(root.RadicandTokens);
            if (_error != EvaluationError.None) return 0;

            if (index == 0) return Fail(EvaluationError.Domain);

            // a negative radicand only has a real root for an odd whole index, and Math.Pow cannot
            // express that, so the sign is pulled out and put back afterwards
            if (radicand < 0)
            {
                bool oddWholeIndex = index == Math.Floor(index) && Math.Abs(index % 2) == 1;
                if (!oddWholeIndex) return Fail(EvaluationError.Domain);

                return -Math.Pow(-radicand, 1.0 / index);
            }

            return Math.Pow(radicand, 1.0 / index);
        }

        private double EvaluateLogarithm(LogarithmToken logarithm)
        {
            // an empty base is the common logarithm, matching what the key produces without one
            double logBase = 10;
            if (logarithm.BaseTokens.Count > 0) logBase = EvaluateSlot(logarithm.BaseTokens);

            double parameter = EvaluateSlot(logarithm.ParameterTokens);
            if (_error != EvaluationError.None) return 0;

            if (parameter <= 0 || logBase <= 0 || logBase == 1) return Fail(EvaluationError.Domain);
            return Math.Log(parameter) / Math.Log(logBase);
        }

        private double EvaluateFunction(FunctionToken function)
        {
            double parameter = EvaluateSlot(function.ParameterTokens);
            if (_error != EvaluationError.None) return 0;

            switch (function.Value)
            {
                case "sin":
                case "cos":
                case "tan":
                    return Trigonometric(function.Value, parameter);

                case "arcsin":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Asin(parameter));

                case "arccos":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Acos(parameter));

                case "arctan":
                    return FromRadians(Math.Atan(parameter));

                case "ln":
                    if (parameter <= 0) return Fail(EvaluationError.Domain);
                    return Math.Log(parameter);

                // the hyperbolic family reads a plain real rather than an angle, so none of it goes
                // through ToRadians the way the trigonometric functions above do; routing it through the
                // angle mode would silently change every result as soon as the mode is switched
                case "sinh":
                    return Math.Sinh(parameter);

                case "cosh":
                    return Math.Cosh(parameter);

                case "tanh":
                    return Math.Tanh(parameter);

                case "arsinh":
                    return Math.Asinh(parameter);

                case "arcosh":
                    if (parameter < 1) return Fail(EvaluationError.Domain);
                    return Math.Acosh(parameter);

                case "artanh":
                    if (parameter <= -1 || parameter >= 1) return Fail(EvaluationError.Domain);
                    return Math.Atanh(parameter);

                case "abs":
                    return Math.Abs(parameter);
            }

            return Fail(EvaluationError.Syntax);
        }


        // === angles ===

        // a half turn in the unit currently selected: 180 degrees or 200 gradians
        // radians never ask, they are handed through untouched below
        private double HalfTurn()
        {
            if (AngleMode == AngleMode.Gradians) return 200.0;

            return 180.0;
        }

        // radians are returned unchanged rather than scaled by one, which also keeps an angle near the
        // top of the double range from overflowing on a conversion that would not move it
        private double ToRadians(double angle)
        {
            if (AngleMode == AngleMode.Radians) return angle;

            return angle * Math.PI / HalfTurn();
        }

        private double FromRadians(double angle)
        {
            if (AngleMode == AngleMode.Radians) return angle;

            return angle * HalfTurn() / Math.PI;
        }

        // tan is a pole wherever the cosine is an exact zero, which CleanTrigResult makes it at every odd
        // quarter turn; that holds in radians too, where no double lands on pi/2 itself, and it is what
        // makes tan(pi/2) the Math ERROR a Casio gives
        private double Trigonometric(string name, double angle)
        {
            double radians = ToRadians(angle);
            double sine = CleanTrigResult(Math.Sin(radians), radians);
            double cosine = CleanTrigResult(Math.Cos(radians), radians);

            if (name == "sin") return sine;
            if (name == "cos") return cosine;

            if (cosine == 0) return Fail(EvaluationError.Domain);
            return CleanTrigResult(sine / cosine, radians);
        }

        // sin(180) comes out as 1.2e-16 rather than 0, because the degree to radian conversion can never
        // be exact; rounding the result is what makes the display agree with the textbook
        //
        // only that noise is snapped to 0, and it grows with the angle; a result above it is real however
        // small it is, where rounding to twelve decimals turned sin of a ten-millionth of a degree into
        // 1.745e-9
        // the rest is cut to fifteen significant digits, about what a Casio computes with, which is what
        // lets sin 30 compare equal to 0.5 inside a formula
        private static double CleanTrigResult(double value, double radians)
        {
            if (Math.Abs(value) <= Math.Abs(radians) * TrigNoisePerRadian) return 0;

            return ResultFormatter.RoundToSignificantDigits(value, TrigDigits);
        }


        // === failure ===

        // records the first failure and hands back a dummy value, so callers can return it directly
        private double Fail(EvaluationError error)
        {
            if (_error == EvaluationError.None) _error = error;
            return 0;
        }
    }
}
