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
    //   expression  := term (plus or minus, term)*
    //   term        := combination (times or divided by or divided with remainder, combination)*
    //   combination := product (nPr or nCr, product)*
    //   product     := unary (implicit, postfix)*
    //   unary       := sign* postfix
    //   postfix     := atom (factorial or reciprocal or percent or prefix)*
    //   atom        := number | constant | Ans | Ran# | bracketed expression | fraction | mixed fraction
    //                   | power | root | function | logarithm
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

        // what Ran# and RanInt# draw from; settable so a test can hand in a seeded one
        public Random RandomSource { get; set; } = new Random();

        // --- the second value of a pair ---
        // the remainder of a division with remainder that was the last operation at the top level, and
        // the angle or y of the last Pol or Rec; Evaluate makes a pair of them only when that operation is
        // the whole calculation
        private double? _topLevelRemainder;
        private double _coordinateSecond;

        // --- trigonometric results ---
        private const double TrigNoisePerRadian = 1e-14; // the most a zero of sin or cos comes out as, per radian of angle
        private const int TrigDigits = 15;               // significant digits a trigonometric result is kept to

        // --- whole numbers ---
        private const int WholeNumberDigits = 15;        // the digits a Casio computes with, at which a value is judged whole
        private const double WholeNumberLimit = 1e15;    // above this a double no longer holds every whole number exactly


        // === constructor ===

        public MathEvaluator(AngleMode angleMode = AngleMode.Degrees)
        {
            AngleMode = angleMode;
        }


        // === entry point ===

        public EvaluationResult Evaluate(IReadOnlyList<MathToken> tokens)
        {
            _error = EvaluationError.None;
            _topLevelRemainder = null;

            if (tokens.Count == 0) return EvaluationResult.Success(0); // empty input reads as 0, same as the display

            int position = 0;
            double value = ParseExpression(tokens, ref position, topLevel: true);

            if (_error != EvaluationError.None) return EvaluationResult.Failure(_error);

            // leftover tokens mean the list did not parse as one expression, a stray closing bracket say
            if (position < tokens.Count) return EvaluationResult.Failure(EvaluationError.Syntax);

            if (double.IsNaN(value)) return EvaluationResult.Failure(EvaluationError.Domain);
            if (double.IsInfinity(value)) return EvaluationResult.Failure(EvaluationError.Overflow);

            // Pol and Rec show both values only as the whole formula; 1+Pol(3,4) is 6 on the Casio
            if (tokens.Count == 1 && tokens[0] is FunctionToken { Value: "pol" or "rec" } coordinates)
            {
                ResultKind kind = coordinates.Value == "pol" ? ResultKind.Polar : ResultKind.Rectangular;
                return EvaluationResult.Pair(kind, value, _coordinateSecond);
            }

            if (_topLevelRemainder is double remainder)
            {
                return EvaluationResult.Pair(ResultKind.QuotientRemainder, value, remainder);
            }

            return EvaluationResult.Success(value);
        }


        // === grammar ===

        // topLevel is set for the formula itself and for nothing nested in it, a bracket or a slot, which is
        // the only level a division with remainder shows its remainder from
        private double ParseExpression(IReadOnlyList<MathToken> tokens, ref int position, bool topLevel = false)
        {
            double value = ParseTerm(tokens, ref position, topLevel);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "+" && token.Value != "-") break;

                // a sum is not a division with remainder, whichever side of it the division stands on
                if (topLevel) _topLevelRemainder = null;

                position++;
                double right = ParseTerm(tokens, ref position);

                if (token.Value == "+") { value += right; }
                else { value -= right; }
            }

            return value;
        }

        private double ParseTerm(IReadOnlyList<MathToken> tokens, ref int position, bool topLevel = false)
        {
            double value = ParseCombination(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "*" && token.Value != "/" && token.Value != "÷R") break;

                position++;
                double right = ParseCombination(tokens, ref position);
                if (_error != EvaluationError.None) return 0;

                // only the last operation of the formula keeps its remainder
                if (topLevel) _topLevelRemainder = null;

                if (token.Value == "*")
                {
                    value *= right;
                }
                else if (token.Value == "/")
                {
                    if (right == 0) return Fail(EvaluationError.DivideByZero);
                    value /= right;
                }
                else
                {
                    value = DivideWithRemainder(value, right, topLevel);
                }
            }

            return value;
        }

        // the quotient of a whole dividend of zero or more by a whole divisor above zero; inside a
        // calculation that is all it hands on, so 10+17÷R6 is 12 as on the Casio
        //
        // any other pair of operands makes it a plain division, the way −17÷R5 is −17/5 on the Casio
        private double DivideWithRemainder(double dividend, double divisor, bool topLevel)
        {
            if (divisor == 0) return Fail(EvaluationError.DivideByZero);

            if (!TryWholeNumber(dividend, out double wholeDividend) || !TryWholeNumber(divisor, out double wholeDivisor)
                || wholeDividend < 0 || wholeDivisor <= 0)
            {
                return dividend / divisor;
            }

            long a = (long)wholeDividend;
            long b = (long)wholeDivisor;

            if (topLevel) _topLevelRemainder = a % b;
            return a / b;
        }

        // nPr and nCr bind tighter than times and divided by and looser than a product written without a
        // sign, the way a Casio ranks them: 12÷2C2 is 12 over 2C2; a sign in front belongs to n, so −5C2
        // has no result
        private double ParseCombination(IReadOnlyList<MathToken> tokens, ref int position)
        {
            double value = ParseProduct(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (!IsCombinationOperator(token)) break;

                position++;
                double right = ParseProduct(tokens, ref position);
                if (_error != EvaluationError.None) return 0;

                value = token.Value == "P" ? Permutations(value, right) : Combinations(value, right);
            }

            return value;
        }

        private static bool IsCombinationOperator(MathToken token)
        {
            return token.Type == TokenType.Operator && (token.Value == "P" || token.Value == "C");
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

                case MixedFractionToken mixed:
                    position++;
                    return EvaluateMixedFraction(mixed);

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

                // three decimals from 0.000 to 0.999, the way a Casio draws it
                case RandomToken:
                    position++;
                    return Math.Floor(RandomSource.NextDouble() * 1000) / 1000;
            }

            if (token.Type == TokenType.Number)
            {
                // the input keeps one token per character, so the whole run is what makes up one value
                // reading it greedily is also what stops the implicit multiplication above from turning
                // 45 into 4 times 5
                int start = position;
                var literal = new StringBuilder();
                while (position < tokens.Count && tokens[position].Type == TokenType.Number)
                {
                    literal.Append(tokens[position].Value);
                    position++;
                }

                // a result carried on as digits is its full value rather than the twelve digits shown, for
                // as long as the run is exactly the digits it was seeded as
                if (IsSeededRun(tokens, start, position)) return tokens[start].Seed!.Magnitude;

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

        private static bool IsSeededRun(IReadOnlyList<MathToken> tokens, int start, int end)
        {
            SeededValue? seed = tokens[start].Seed;
            if (seed == null || end - start != seed.DigitCount) return false;

            for (int index = start; index < end; index++)
            {
                if (!ReferenceEquals(tokens[index].Seed, seed)) return false;
            }

            return true;
        }

        private static bool StartsAtom(MathToken token)
        {
            if (token.Type == TokenType.Number) return true;
            if (token.Type == TokenType.Constant) return true;
            if (token.Type == TokenType.BracketOpen) return true;

            return token is FractionToken
                || token is MixedFractionToken
                || token is PowerToken
                || token is RootToken
                || token is FunctionToken
                || token is LogarithmToken
                || token is AnsToken
                || token is RandomToken;
        }


        // === reading ===

        // a copy of the formula with a bracket pair around every operand that binds tighter than the
        // divided by in front of it, which is how the history line shows the way the formula was read
        //
        // a Casio rewrites the input itself on =, 6÷2(1+2) into 6÷(2(1+2)); here the tree stays exactly as
        // it was typed and only the copy the history line draws carries the brackets
        // the scan below follows ParseCombination and ParseProduct and has to move with them
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
                if (tokens[index].Type != TokenType.Operator) continue;
                if (tokens[index].Value != "/" && tokens[index].Value != "÷R") continue;

                int start = index + 1;
                int end = OperandEnd(tokens, start, out int factors, out int unclosed);
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

        // where the operand starting at start ends, and how many factors it has: the products
        // ParseProduct reads, joined by the P and C of ParseCombination; signs belong to the factor they
        // stand in front of, the way ParseUnary reads them
        private static int OperandEnd(List<MathToken> tokens, int start, out int factors, out int unclosed)
        {
            int position = start;
            factors = 0;
            unclosed = 0;

            while (true)
            {
                while (position < tokens.Count && IsSign(tokens[position])) position++;

                while (position < tokens.Count && StartsAtom(tokens[position]))
                {
                    position = AtomEnd(tokens, position, ref unclosed);
                    while (position < tokens.Count && tokens[position].Type == TokenType.Postfix) position++;

                    factors++;
                }

                if (position >= tokens.Count || !IsCombinationOperator(tokens[position])) return position;
                position++;
            }
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

            // a decimal prefix scales by its power of ten; a small one divides rather than multiplies,
            // because 5 divided by 1000 lands on the nearest double and 5 times 0.001 does not
            if (PostfixToken.PrefixExponent(kind) is int exponent)
            {
                double scale = Math.Pow(10, Math.Abs(exponent));
                return exponent < 0 ? value / scale : value * scale;
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

        // every part has to be a whole number, the one input rule a Casio has for it, which it enforces by
        // not taking a decimal point there; here anything goes in and the check happens on =
        //
        // the magnitudes add and the signs multiply: a minus on any one part makes the whole number
        // negative, so a whole part of 1 with −1 over 2 is −3/2, as on the Casio. Two negative parts
        // cancel, which is the same rule carried on; nothing measured that case
        private double EvaluateMixedFraction(MixedFractionToken mixed)
        {
            double whole = EvaluateSlot(mixed.WholeTokens);
            double numerator = EvaluateSlot(mixed.NumeratorTokens);
            double denominator = EvaluateSlot(mixed.DenominatorTokens);
            if (_error != EvaluationError.None) return 0;

            if (!TryWholeNumber(whole, out whole)
                || !TryWholeNumber(numerator, out numerator)
                || !TryWholeNumber(denominator, out denominator))
            {
                return Fail(EvaluationError.Syntax);
            }

            if (denominator == 0) return Fail(EvaluationError.DivideByZero);

            double magnitude = Math.Abs(whole) + Math.Abs(numerator) / Math.Abs(denominator);
            bool negative = (whole < 0) ^ (numerator < 0) ^ (denominator < 0);

            return negative ? -magnitude : magnitude;
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
            double[] arguments = new double[function.Arguments.Count];
            for (int index = 0; index < arguments.Length; index++)
            {
                arguments[index] = EvaluateSlot(function.Arguments[index]);
            }

            if (_error != EvaluationError.None) return 0;

            double parameter = arguments[0];

            switch (function.Value)
            {
                case "sin":
                case "cos":
                case "tan":
                case "sec":
                case "csc":
                case "cot":
                    return Trigonometric(function.Value, parameter);

                case "arcsin":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Asin(parameter));

                case "arccos":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Acos(parameter));

                case "arctan":
                    return FromRadians(Math.Atan(parameter));

                // the inverse of a reciprocal is the inverse of its partner taken at the reciprocal
                //
                // cot⁻¹ answers between 0° and 180° rather than between −90° and 90°, which keeps it
                // continuous through zero and is the range a German formula collection gives it
                case "arcsec":
                    if (Math.Abs(parameter) < 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Acos(1 / parameter));

                case "arccsc":
                    if (Math.Abs(parameter) < 1) return Fail(EvaluationError.Domain);
                    return FromRadians(Math.Asin(1 / parameter));

                case "arccot":
                    return FromRadians(Math.PI / 2 - Math.Atan(parameter));

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

                case "sech":
                    return 1 / Math.Cosh(parameter);

                case "csch":
                    if (parameter == 0) return Fail(EvaluationError.Domain);
                    return 1 / Math.Sinh(parameter);

                case "coth":
                    if (parameter == 0) return Fail(EvaluationError.Domain);
                    return 1 / Math.Tanh(parameter);

                case "arsech":
                    if (parameter <= 0 || parameter > 1) return Fail(EvaluationError.Domain);
                    return Math.Acosh(1 / parameter);

                case "arcsch":
                    if (parameter == 0) return Fail(EvaluationError.Domain);
                    return Math.Asinh(1 / parameter);

                case "arcoth":
                    if (Math.Abs(parameter) <= 1) return Fail(EvaluationError.Domain);
                    return Math.Atanh(1 / parameter);

                case "abs":
                    return Math.Abs(parameter);

                // floor and Intg are the same function from two sets of keys, the Windows one and the
                // Casio one; Int cuts towards zero, so Int(−2.5) is −2 where Intg(−2.5) is −3
                case "floor":
                case "intg":
                    return Math.Floor(AtCasioPrecision(parameter));

                case "ceil":
                    return Math.Ceiling(AtCasioPrecision(parameter));

                case "int":
                    return Math.Truncate(AtCasioPrecision(parameter));

                case "gcd":
                    return GreatestCommonDivisor(arguments[0], arguments[1]);

                case "lcm":
                    return LeastCommonMultiple(arguments[0], arguments[1]);

                case "ranint":
                    return RandomInteger(arguments[0], arguments[1]);

                case "rndfix":
                    return RoundToDecimals(arguments[0], arguments[1]);

                case "pol":
                    return Polar(arguments[0], arguments[1]);

                case "rec":
                    return Rectangular(arguments[0], arguments[1]);
            }

            return Fail(EvaluationError.Syntax);
        }


        // === coordinates ===

        // r of the point (x, y), with θ left in _coordinateSecond for the pair; θ is in the angle unit
        // selected and runs up to and including a half turn, so Pol(−1, 0) is π as on the Casio
        //
        // the origin has no angle, which the Casio answers with a Math ERROR
        private double Polar(double x, double y)
        {
            if (x == 0 && y == 0) return Fail(EvaluationError.Domain);

            // a negative zero would put a point on the negative axis at minus a half turn
            _coordinateSecond = FromRadians(Math.Atan2(y == 0 ? 0 : y, x));

            return double.Hypot(x, y);
        }

        // x of the point at distance r and angle θ, with y left in _coordinateSecond; through the same
        // cleaned sine and cosine as sin and cos, so Rec(1, 90) is exactly (0, 1)
        private double Rectangular(double r, double angle)
        {
            double x = r * Trigonometric("cos", angle);
            _coordinateSecond = r * Trigonometric("sin", angle);

            return x;
        }


        // === whole numbers ===

        // the value at the fifteen digits a Casio computes with
        //
        // 0.1×30 is 3.0000000000000004 as a double and a plain 3 on a Casio, and 1−0.9 is 0.09999999999999998;
        // without this, Int(10(1−0.9)) would be 0 and GCD(0.1×30, 6) a Math ERROR
        private static double AtCasioPrecision(double value)
        {
            return ResultFormatter.RoundToSignificantDigits(value, WholeNumberDigits);
        }

        // whether a value is a whole number at that precision, and small enough that a double still holds
        // every whole number around it
        private static bool TryWholeNumber(double value, out double whole)
        {
            whole = AtCasioPrecision(value);

            return Math.Abs(whole) < WholeNumberLimit && whole == Math.Floor(whole);
        }

        // both arguments have to be whole; a sign does not change the divisor, so GCD(−12, 18) is 6 the
        // way a Casio answers it
        private double GreatestCommonDivisor(double first, double second)
        {
            if (!TryWholeNumber(first, out double a) || !TryWholeNumber(second, out double b))
            {
                return Fail(EvaluationError.Domain);
            }

            return Gcd((long)Math.Abs(a), (long)Math.Abs(b));
        }

        // a multiple of zero is zero, so LCM(0, 5) is 0 rather than an error, which is what a Casio shows
        private double LeastCommonMultiple(double first, double second)
        {
            if (!TryWholeNumber(first, out double a) || !TryWholeNumber(second, out double b))
            {
                return Fail(EvaluationError.Domain);
            }

            long x = (long)Math.Abs(a);
            long y = (long)Math.Abs(b);
            if (x == 0 || y == 0) return 0;

            return (double)(x / Gcd(x, y)) * y;
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0) (a, b) = (b, a % b);

            return a;
        }

        // n and r have to be whole, n not negative and r no larger than n; anything else counts nothing,
        // which a Casio answers with a Math ERROR
        private static bool TryCountPair(double n, double r, out double count, out double chosen)
        {
            bool whole = TryWholeNumber(n, out count) & TryWholeNumber(r, out chosen);

            return whole && count >= 0 && chosen >= 0 && chosen <= count;
        }

        // the loop stops at the first overflow, which a large n reaches within a few hundred factors, so a
        // count near the whole number limit cannot keep it running
        private double Permutations(double n, double r)
        {
            if (!TryCountPair(n, r, out double count, out double chosen)) return Fail(EvaluationError.Domain);

            double result = 1;
            for (double factor = count; factor > count - chosen; factor--)
            {
                result *= factor;
                if (double.IsInfinity(result)) return Fail(EvaluationError.Overflow);
            }

            return result;
        }

        // built from the smaller side one factor at a time, where every intermediate step is itself a
        // count and therefore whole, so nothing is lost to a division until the double runs out of digits
        private double Combinations(double n, double r)
        {
            if (!TryCountPair(n, r, out double count, out double chosen)) return Fail(EvaluationError.Domain);

            double smaller = Math.Min(chosen, count - chosen);
            double result = 1;

            for (double step = 1; step <= smaller; step++)
            {
                result = result * (count - smaller + step) / step;
                if (double.IsInfinity(result)) return Fail(EvaluationError.Overflow);
            }

            return result;
        }

        // a whole number from low to high, both included; bounds that are not whole or not in order are
        // an Argument ERROR, the way RanInt#(6,1) is on a Casio
        private double RandomInteger(double low, double high)
        {
            if (!TryWholeNumber(low, out double from) || !TryWholeNumber(high, out double to) || from >= to)
            {
                return Fail(EvaluationError.Argument);
            }

            return RandomSource.NextInt64((long)from, (long)to + 1);
        }

        // the value rounded to a whole number of decimals from 0 to 9, the range Fix takes
        //
        // rounded as a decimal from the fifteen digits a Casio holds, so 2.675 rounds up to 2.68 the way
        // it reads, where the double just below 2.675 would round down
        private double RoundToDecimals(double value, double decimals)
        {
            if (!TryWholeNumber(decimals, out double places) || places < 0 || places > 9)
            {
                return Fail(EvaluationError.Argument);
            }

            if (Math.Abs(value) >= WholeNumberLimit) return value; // no decimals left to round

            decimal exact = decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture);

            return (double)Math.Round(exact, (int)places, MidpointRounding.AwayFromZero);
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
        //
        // sec shares those poles, csc and cot have theirs wherever the sine is zero, and cot is an exact 0
        // wherever the cosine is
        private double Trigonometric(string name, double angle)
        {
            double radians = ToRadians(angle);
            double sine = CleanTrigResult(Math.Sin(radians), radians);
            double cosine = CleanTrigResult(Math.Cos(radians), radians);

            switch (name)
            {
                case "sin": return sine;
                case "cos": return cosine;
                case "tan": return TrigRatio(sine, cosine, radians);
                case "sec": return TrigRatio(1, cosine, radians);
                case "csc": return TrigRatio(1, sine, radians);
            }

            return TrigRatio(cosine, sine, radians);
        }

        private double TrigRatio(double numerator, double denominator, double radians)
        {
            if (denominator == 0) return Fail(EvaluationError.Domain);

            return CleanTrigResult(numerator / denominator, radians);
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
