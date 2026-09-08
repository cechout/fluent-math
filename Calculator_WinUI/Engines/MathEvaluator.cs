using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.Engines
{
    // turns the token tree MathInputManager owns into an actual number
    //
    // recursive descent straight over the token lists, with no string in between; a structured token is
    // just an atom that evaluates its own child lists through the same entry point, which is what lets a
    // fraction inside an exponent inside a root work without a single special case
    //
    // the grammar is the usual precedence ladder:
    //   expression := term (plus or minus, term)*
    //   term       := unary (times or divided by or implicit, unary)*
    //   unary      := sign* atom
    //   atom       := number | constant | bracketed expression | fraction | power | root | function
    //                  | logarithm
    //
    // nothing here throws; a failure sets _error and the recursion unwinds on its own, because a
    // half-typed formula is the normal state of the input rather than an exceptional one
    public class MathEvaluator
    {
        // === fields ===

        private readonly AngleMode _angleMode;

        // set the moment any sub-expression fails; every loop checks it so the parse stops early
        private EvaluationError _error;


        // === constructor ===

        public MathEvaluator(AngleMode angleMode = AngleMode.Degrees)
        {
            _angleMode = angleMode;
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
            double value = ParseUnary(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];

                if (token.Type == TokenType.Operator && (token.Value == "*" || token.Value == "/"))
                {
                    position++;
                    double right = ParseUnary(tokens, ref position);
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
                    continue;
                }

                // no operator but something that can start an atom: implicit multiplication, so that
                // 3(4+5) and 2sin(30) work the way they do on paper
                if (StartsAtom(token))
                {
                    value *= ParseAtom(tokens, ref position);
                    continue;
                }

                break;
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

            return ParseAtom(tokens, ref position);
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
                || token is LogarithmToken;
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
                    return CleanTrigResult(Math.Sin(ToRadians(parameter)));

                case "cos":
                    return CleanTrigResult(Math.Cos(ToRadians(parameter)));

                case "tan":
                    if (IsTangentPole(parameter)) return Fail(EvaluationError.Domain);
                    return CleanTrigResult(Math.Tan(ToRadians(parameter)));

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
            }

            return Fail(EvaluationError.Syntax);
        }


        // === angles ===

        private double ToRadians(double angle)
        {
            if (_angleMode == AngleMode.Degrees) return angle * Math.PI / 180.0;
            return angle;
        }

        private double FromRadians(double angle)
        {
            if (_angleMode == AngleMode.Degrees) return angle * 180.0 / Math.PI;
            return angle;
        }

        // sin(180) comes out as 1.2e-16 rather than 0, because the degree to radian conversion can never
        // be exact; rounding the result is what makes the display agree with the textbook
        private static double CleanTrigResult(double value)
        {
            return Math.Round(value, 12);
        }

        // tan has a pole every 180 degrees offset by 90, and floating point never lands exactly on it, so
        // the check is on the angle rather than on an infinite result
        // in radians no double hits pi/2 exactly, so there is nothing to catch there
        private bool IsTangentPole(double angle)
        {
            if (_angleMode != AngleMode.Degrees) return false;

            double normalized = Math.Abs(angle % 180);
            return Math.Abs(normalized - 90) < 1e-9;
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
