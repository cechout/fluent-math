using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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
    // every value carries its exact value beside the double, see MathValue; a step with no exact answer,
    // a logarithm, e or a sine of 18°, drops it and the rest of the calculation goes on with the double
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
        // the exact value comes along with it, so Ans+√2 on a result of √2 is 2√2 as on a Casio
        public MathValue LastAnswer { get; set; }

        // what Ran# and RanInt# draw from; settable so a test can hand in a seeded one
        public Random RandomSource { get; set; } = new Random();

        // the number format of the display, which Rnd rounds to
        public NumberFormat NumberFormat { get; set; } = NumberFormat.Default;

        // --- the second value of a pair ---
        // the remainder of a division with remainder that was the last operation at the top level, and
        // the angle or y of the last Pol or Rec; Evaluate makes a pair of them only when that operation is
        // the whole calculation
        private MathValue? _topLevelRemainder;
        private MathValue _coordinateSecond;

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

            if (tokens.Count == 0) return EvaluationResult.Success(MathValue.Whole(0)); // empty input reads as 0, same as the display

            int position = 0;
            MathValue value = ParseExpression(tokens, ref position, topLevel: true);

            if (_error != EvaluationError.None) return EvaluationResult.Failure(_error);

            // leftover tokens mean the list did not parse as one expression, a stray closing bracket say
            if (position < tokens.Count) return EvaluationResult.Failure(EvaluationError.Syntax);

            if (double.IsNaN(value.Value)) return EvaluationResult.Failure(EvaluationError.Domain);
            if (double.IsInfinity(value.Value)) return EvaluationResult.Failure(EvaluationError.Overflow);

            // Pol and Rec show both values only as the whole formula; 1+Pol(3,4) is 6 on the Casio
            if (tokens.Count == 1 && tokens[0] is FunctionToken { Value: "pol" or "rec" } coordinates)
            {
                ResultKind kind = coordinates.Value == "pol" ? ResultKind.Polar : ResultKind.Rectangular;
                return EvaluationResult.Pair(kind, value, _coordinateSecond);
            }

            if (_topLevelRemainder is MathValue remainder)
            {
                return EvaluationResult.Pair(ResultKind.QuotientRemainder, value, remainder);
            }

            return EvaluationResult.Success(value);
        }


        // === grammar ===

        // topLevel is set for the formula itself and for nothing nested in it, a bracket or a slot, which is
        // the only level a division with remainder shows its remainder from
        private MathValue ParseExpression(IReadOnlyList<MathToken> tokens, ref int position, bool topLevel = false)
        {
            MathValue value = ParseTerm(tokens, ref position, topLevel);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "+" && token.Value != "-") break;

                // a sum is not a division with remainder, whichever side of it the division stands on
                if (topLevel) _topLevelRemainder = null;

                position++;
                MathValue right = ParseTerm(tokens, ref position);

                if (token.Value == "+") { value += right; }
                else { value -= right; }
            }

            return value;
        }

        private MathValue ParseTerm(IReadOnlyList<MathToken> tokens, ref int position, bool topLevel = false)
        {
            MathValue value = ParseCombination(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Operator) break;
                if (token.Value != "*" && token.Value != "/" && token.Value != "÷R") break;

                position++;
                MathValue right = ParseCombination(tokens, ref position);
                if (_error != EvaluationError.None) return 0;

                // only the last operation of the formula keeps its remainder
                if (topLevel) _topLevelRemainder = null;

                if (token.Value == "*")
                {
                    value *= right;
                }
                else if (token.Value == "/")
                {
                    if (right.Value == 0) return Fail(EvaluationError.DivideByZero);
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
        private MathValue DivideWithRemainder(MathValue dividend, MathValue divisor, bool topLevel)
        {
            if (divisor.Value == 0) return Fail(EvaluationError.DivideByZero);

            if (!TryWholeNumber(dividend.Value, out double wholeDividend) || !TryWholeNumber(divisor.Value, out double wholeDivisor)
                || wholeDividend < 0 || wholeDivisor <= 0)
            {
                return dividend / divisor;
            }

            long a = (long)wholeDividend;
            long b = (long)wholeDivisor;

            if (topLevel) _topLevelRemainder = MathValue.Whole(a % b);
            return MathValue.Whole(a / b);
        }

        // nPr and nCr bind tighter than times and divided by and looser than a product written without a
        // sign, the way a Casio ranks them: 12÷2C2 is 12 over 2C2; a sign in front belongs to n, so −5C2
        // has no result
        private MathValue ParseCombination(IReadOnlyList<MathToken> tokens, ref int position)
        {
            MathValue value = ParseProduct(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (!IsCombinationOperator(token)) break;

                position++;
                MathValue right = ParseProduct(tokens, ref position);
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
        private MathValue ParseProduct(IReadOnlyList<MathToken> tokens, ref int position)
        {
            MathValue value = ParseUnary(tokens, ref position);

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
        private MathValue ParseUnary(IReadOnlyList<MathToken> tokens, ref int position)
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
        private MathValue ParsePostfix(IReadOnlyList<MathToken> tokens, ref int position)
        {
            MathValue value = ParseAtom(tokens, ref position);

            while (_error == EvaluationError.None && position < tokens.Count)
            {
                MathToken token = tokens[position];
                if (token.Type != TokenType.Postfix) break;

                position++;
                value = ApplyPostfix(token.Value, value);
            }

            return value;
        }

        private MathValue ParseAtom(IReadOnlyList<MathToken> tokens, ref int position)
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
                    return new MathValue(constant.NumericValue, constant.Exact);

                case AnsToken:
                    position++;
                    return LastAnswer;

                // three decimals from 0.000 to 0.999, the way a Casio draws it
                case RandomToken:
                    position++;
                    long thousandths = (long)Math.Floor(RandomSource.NextDouble() * 1000);
                    return new MathValue(thousandths / 1000.0, ExactValue.FromRational(new Rational(thousandths, 1000)));
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
                //
                // a typed number is exact as it stands, 0.1 is a tenth and not the double nearest to one
                return new MathValue(number, ExactValue.FromDecimal(literal.ToString()));
            }

            if (token.Type == TokenType.BracketOpen)
            {
                position++;
                MathValue inner = ParseExpression(tokens, ref position);
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

        private MathValue ApplyPostfix(string kind, MathValue value)
        {
            switch (kind)
            {
                case "!":
                    return Factorial(value);

                case "inv":
                    if (value.Value == 0) return Fail(EvaluationError.DivideByZero);
                    return MathValue.Whole(1) / value;

                // plain division by a hundred, which is the meaning the FX-991 gives the key; the add-on
                // percent of a business calculator, where 200 + 10% comes out as 220, is deliberately
                // not what this does
                case "%":
                    return value / MathValue.Whole(100);
            }

            // a decimal prefix scales by its power of ten; a small one divides rather than multiplies,
            // because 5 divided by 1000 lands on the nearest double and 5 times 0.001 does not
            if (PostfixToken.PrefixExponent(kind) is int exponent)
            {
                MathValue scale = new MathValue(Math.Pow(10, Math.Abs(exponent)),
                    ExactValue.FromInteger(BigInteger.Pow(10, Math.Abs(exponent))));

                return exponent < 0 ? value / scale : value * scale;
            }

            return Fail(EvaluationError.Syntax);
        }

        // only a whole count that is not negative has one, and 171! is already past the range of a
        // double, so the ceiling is checked here rather than left to come back as an infinity
        private MathValue Factorial(MathValue value)
        {
            double count = value.Value;
            if (count < 0 || count != Math.Floor(count)) return Fail(EvaluationError.Domain);
            if (count > 170) return Fail(EvaluationError.Overflow);

            double result = 1;
            BigInteger exact = BigInteger.One;
            for (int factor = 2; factor <= (int)count; factor++)
            {
                result *= factor;
                exact *= factor;
            }

            return new MathValue(result, ExactValue.FromInteger(exact));
        }


        // === structured tokens ===

        // every slot of a structured token is a complete expression of its own
        //
        // an empty slot is a syntax error here; the two slots that have a sensible default instead, the
        // root index and the logarithm base, are checked by their caller before it ever gets this far
        private MathValue EvaluateSlot(List<MathToken> tokens)
        {
            if (tokens.Count == 0) return Fail(EvaluationError.Syntax);

            int position = 0;
            MathValue value = ParseExpression(tokens, ref position);

            if (_error == EvaluationError.None && position < tokens.Count) return Fail(EvaluationError.Syntax);
            return value;
        }

        private MathValue EvaluateFraction(FractionToken fraction)
        {
            MathValue numerator = EvaluateSlot(fraction.NumeratorTokens);
            MathValue denominator = EvaluateSlot(fraction.DenominatorTokens);
            if (_error != EvaluationError.None) return 0;

            if (denominator.Value == 0) return Fail(EvaluationError.DivideByZero);
            return numerator / denominator;
        }

        // every part has to be a whole number, the one input rule a Casio has for it, which it enforces by
        // not taking a decimal point there; here anything goes in and the check happens on =
        //
        // the magnitudes add and the signs multiply: a minus on any one part makes the whole number
        // negative, so a whole part of 1 with −1 over 2 is −3/2, as on the Casio. Two negative parts
        // cancel, which is the same rule carried on; nothing measured that case
        private MathValue EvaluateMixedFraction(MixedFractionToken mixed)
        {
            MathValue wholeValue = EvaluateSlot(mixed.WholeTokens);
            MathValue numeratorValue = EvaluateSlot(mixed.NumeratorTokens);
            MathValue denominatorValue = EvaluateSlot(mixed.DenominatorTokens);
            if (_error != EvaluationError.None) return 0;

            if (!TryWholeNumber(wholeValue.Value, out double whole)
                || !TryWholeNumber(numeratorValue.Value, out double numerator)
                || !TryWholeNumber(denominatorValue.Value, out double denominator))
            {
                return Fail(EvaluationError.Syntax);
            }

            if (denominator == 0) return Fail(EvaluationError.DivideByZero);

            double magnitude = Math.Abs(whole) + Math.Abs(numerator) / Math.Abs(denominator);
            bool negative = (whole < 0) ^ (numerator < 0) ^ (denominator < 0);

            BigInteger bottom = new BigInteger(Math.Abs(denominator));
            Rational exact = new Rational(new BigInteger(Math.Abs(whole)) * bottom + new BigInteger(Math.Abs(numerator)), bottom);

            return negative
                ? new MathValue(-magnitude, ExactValue.FromRational(-exact))
                : new MathValue(magnitude, ExactValue.FromRational(exact));
        }

        private MathValue EvaluatePower(PowerToken power)
        {
            MathValue baseValue = EvaluateSlot(power.BaseTokens);
            MathValue exponent = EvaluateSlot(power.ExponentTokens);
            if (_error != EvaluationError.None) return 0;

            // zero to the zero and zero to a negative power are both Math ERROR on an FX-991; Math.Pow
            // answers 1 and an infinity instead, neither of which a calculator should show
            if (baseValue.Value == 0 && exponent.Value <= 0) return Fail(EvaluationError.Domain);

            double result = Math.Pow(baseValue.Value, exponent.Value);
            if (double.IsNaN(result)) return Fail(EvaluationError.Domain); // negative base with a fractional exponent

            // exact only for a whole exponent; a root is what the root key is for
            return new MathValue(result, ExactValue.Power(baseValue.Exact, exponent.Exact));
        }

        private MathValue EvaluateRoot(RootToken root)
        {
            // an empty index is a plain square root, which is exactly what the display shows for it
            MathValue index = MathValue.Whole(2);
            if (root.IndexTokens.Count > 0) index = EvaluateSlot(root.IndexTokens);

            MathValue radicand = EvaluateSlot(root.RadicandTokens);
            if (_error != EvaluationError.None) return 0;

            if (index.Value == 0) return Fail(EvaluationError.Domain);

            ExactValue? exact = ExactValue.Root(radicand.Exact, index.Exact);

            // a negative radicand only has a real root for an odd whole index, and Math.Pow cannot
            // express that, so the sign is pulled out and put back afterwards
            if (radicand.Value < 0)
            {
                bool oddWholeIndex = index.Value == Math.Floor(index.Value) && Math.Abs(index.Value % 2) == 1;
                if (!oddWholeIndex) return Fail(EvaluationError.Domain);

                return new MathValue(-Math.Pow(-radicand.Value, 1.0 / index.Value), exact);
            }

            return new MathValue(Math.Pow(radicand.Value, 1.0 / index.Value), exact);
        }

        private MathValue EvaluateLogarithm(LogarithmToken logarithm)
        {
            // an empty base is the common logarithm, matching what the key produces without one
            MathValue logBase = 10;
            if (logarithm.BaseTokens.Count > 0) logBase = EvaluateSlot(logarithm.BaseTokens);

            MathValue parameter = EvaluateSlot(logarithm.ParameterTokens);
            if (_error != EvaluationError.None) return 0;

            if (parameter.Value <= 0 || logBase.Value <= 0 || logBase.Value == 1) return Fail(EvaluationError.Domain);
            return Math.Log(parameter.Value) / Math.Log(logBase.Value);
        }

        private MathValue EvaluateFunction(FunctionToken function)
        {
            MathValue[] arguments = new MathValue[function.Arguments.Count];
            for (int index = 0; index < arguments.Length; index++)
            {
                arguments[index] = EvaluateSlot(function.Arguments[index]);
            }

            if (_error != EvaluationError.None) return 0;

            MathValue argument = arguments[0];
            double parameter = argument.Value;

            switch (function.Value)
            {
                case "sin":
                case "cos":
                case "tan":
                case "sec":
                case "csc":
                case "cot":
                    return Trigonometric(function.Value, argument);

                // the inverse functions look their argument up among the values a multiple of 15° has, so
                // sin⁻¹0.5 is exactly 30, or π/6 in radians
                case "arcsin":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return new MathValue(FromRadians(Math.Asin(parameter)), ExactAngle(SineSteps(argument.Exact)));

                case "arccos":
                    if (parameter < -1 || parameter > 1) return Fail(EvaluationError.Domain);
                    return new MathValue(FromRadians(Math.Acos(parameter)), ExactAngle(Complement(SineSteps(argument.Exact))));

                case "arctan":
                    return new MathValue(FromRadians(Math.Atan(parameter)), ExactAngle(TangentSteps(argument.Exact)));

                // the inverse of a reciprocal is the inverse of its partner taken at the reciprocal
                //
                // cot⁻¹ answers between 0° and 180° rather than between −90° and 90°, which keeps it
                // continuous through zero and is the range a German formula collection gives it
                case "arcsec":
                    if (Math.Abs(parameter) < 1) return Fail(EvaluationError.Domain);
                    return new MathValue(FromRadians(Math.Acos(1 / parameter)),
                        ExactAngle(Complement(SineSteps(Reciprocal(argument.Exact)))));

                case "arccsc":
                    if (Math.Abs(parameter) < 1) return Fail(EvaluationError.Domain);
                    return new MathValue(FromRadians(Math.Asin(1 / parameter)), ExactAngle(SineSteps(Reciprocal(argument.Exact))));

                case "arccot":
                    return new MathValue(FromRadians(Math.PI / 2 - Math.Atan(parameter)),
                        ExactAngle(Complement(TangentSteps(argument.Exact))));

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
                    return new MathValue(Math.Abs(parameter), ExactValue.Abs(argument.Exact));

                // floor and Intg are the same function from two sets of keys, the Windows one and the
                // Casio one; Int cuts towards zero, so Int(−2.5) is −2 where Intg(−2.5) is −3
                case "floor":
                case "intg":
                    return MathValue.Whole(Math.Floor(AtCasioPrecision(parameter)));

                case "ceil":
                    return MathValue.Whole(Math.Ceiling(AtCasioPrecision(parameter)));

                case "int":
                    return MathValue.Whole(Math.Truncate(AtCasioPrecision(parameter)));

                case "gcd":
                    return GreatestCommonDivisor(arguments[0].Value, arguments[1].Value);

                case "lcm":
                    return LeastCommonMultiple(arguments[0].Value, arguments[1].Value);

                case "ranint":
                    return RandomInteger(arguments[0].Value, arguments[1].Value);

                case "rndfix":
                    return RoundToDecimals(arguments[0].Value, arguments[1].Value);

                // the value the display would write, so Rnd(1÷3) in Fix 2 is 0.33 and three of it 0.99;
                // exact as written, which is what makes those two 33/100 and 99/100 on the Casio
                case "rnd":
                    WrittenDecimal written = ResultFormatter.Write(parameter, NumberFormat);
                    return new MathValue(written.Value, ExactValue.FromDecimal(written.Digits, written.Exponent ?? 0));

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
        // both are exact where they can be, so Pol(1, 1) is r = √2 and θ = 45
        private MathValue Polar(MathValue x, MathValue y)
        {
            if (x.Value == 0 && y.Value == 0) return Fail(EvaluationError.Domain);

            // a negative zero would put a point on the negative axis at minus a half turn
            _coordinateSecond = new MathValue(FromRadians(Math.Atan2(y.Value == 0 ? 0 : y.Value, x.Value)),
                ExactAngle(DirectionSteps(x.Exact, y.Exact)));

            ExactValue? squares = ExactValue.Add(ExactValue.Multiply(x.Exact, x.Exact), ExactValue.Multiply(y.Exact, y.Exact));
            return new MathValue(double.Hypot(x.Value, y.Value), ExactValue.SquareRoot(squares));
        }

        // x of the point at distance r and angle θ, with y left in _coordinateSecond; through the same
        // cleaned sine and cosine as sin and cos, so Rec(1, 90) is exactly (0, 1)
        private MathValue Rectangular(MathValue r, MathValue angle)
        {
            MathValue x = r * Trigonometric("cos", angle);
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
        private MathValue GreatestCommonDivisor(double first, double second)
        {
            if (!TryWholeNumber(first, out double a) || !TryWholeNumber(second, out double b))
            {
                return Fail(EvaluationError.Domain);
            }

            return MathValue.Whole(Gcd((long)Math.Abs(a), (long)Math.Abs(b)));
        }

        // a multiple of zero is zero, so LCM(0, 5) is 0 rather than an error, which is what a Casio shows
        private MathValue LeastCommonMultiple(double first, double second)
        {
            if (!TryWholeNumber(first, out double a) || !TryWholeNumber(second, out double b))
            {
                return Fail(EvaluationError.Domain);
            }

            long x = (long)Math.Abs(a);
            long y = (long)Math.Abs(b);
            if (x == 0 || y == 0) return MathValue.Whole(0);

            return MathValue.Whole((double)(x / Gcd(x, y)) * y);
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
        //
        // the exact count is multiplied up beside it, since the double loses digits long before it overflows
        private MathValue Permutations(MathValue n, MathValue r)
        {
            if (!TryCountPair(n.Value, r.Value, out double count, out double chosen)) return Fail(EvaluationError.Domain);

            double result = 1;
            BigInteger exact = BigInteger.One;

            for (double factor = count; factor > count - chosen; factor--)
            {
                result *= factor;
                if (double.IsInfinity(result)) return Fail(EvaluationError.Overflow);

                exact *= new BigInteger(factor);
            }

            return new MathValue(result, ExactValue.FromInteger(exact));
        }

        // built from the smaller side one factor at a time, where every intermediate step is itself a
        // count and therefore whole, so nothing is lost to a division until the double runs out of digits
        private MathValue Combinations(MathValue n, MathValue r)
        {
            if (!TryCountPair(n.Value, r.Value, out double count, out double chosen)) return Fail(EvaluationError.Domain);

            double smaller = Math.Min(chosen, count - chosen);
            double result = 1;
            BigInteger exact = BigInteger.One;

            for (double step = 1; step <= smaller; step++)
            {
                result = result * (count - smaller + step) / step;
                if (double.IsInfinity(result)) return Fail(EvaluationError.Overflow);

                exact = exact * new BigInteger(count - smaller + step) / new BigInteger(step);
            }

            return new MathValue(result, ExactValue.FromInteger(exact));
        }

        // a whole number from low to high, both included; bounds that are not whole or not in order are
        // an Argument ERROR, the way RanInt#(6,1) is on a Casio
        private MathValue RandomInteger(double low, double high)
        {
            if (!TryWholeNumber(low, out double from) || !TryWholeNumber(high, out double to) || from >= to)
            {
                return Fail(EvaluationError.Argument);
            }

            return MathValue.Whole(RandomSource.NextInt64((long)from, (long)to + 1));
        }

        // the value rounded to a whole number of decimals from 0 to 9, the range Fix takes
        //
        // rounded as a decimal from the fifteen digits a Casio holds, so 2.675 rounds up to 2.68 the way
        // it reads, where the double just below 2.675 would round down
        private MathValue RoundToDecimals(double value, double decimals)
        {
            if (!TryWholeNumber(decimals, out double places) || places < 0 || places > 9)
            {
                return Fail(EvaluationError.Argument);
            }

            if (Math.Abs(value) >= WholeNumberLimit) return value; // no decimals left to round

            decimal exact = decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture);

            decimal rounded = Math.Round(exact, (int)places, MidpointRounding.AwayFromZero);
            return new MathValue((double)rounded, ExactValue.FromDecimal(rounded.ToString(CultureInfo.InvariantCulture)));
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
        // at a multiple of 15° the exact values come out of the table below as well, cos 30 as √3/2
        private MathValue Trigonometric(string name, MathValue angle)
        {
            double radians = ToRadians(angle.Value);
            double sine = CleanTrigResult(Math.Sin(radians), radians);
            double cosine = CleanTrigResult(Math.Cos(radians), radians);

            int? steps = AngleSteps(angle.Exact);
            ExactValue? exactSine = ExactSine(steps);
            ExactValue? exactCosine = ExactSine(steps + 6);
            ExactValue one = ExactValue.FromInteger(1);

            switch (name)
            {
                case "sin": return new MathValue(sine, exactSine);
                case "cos": return new MathValue(cosine, exactCosine);
                case "tan": return TrigRatio(sine, cosine, radians, exactSine, exactCosine);
                case "sec": return TrigRatio(1, cosine, radians, one, exactCosine);
                case "csc": return TrigRatio(1, sine, radians, one, exactSine);
            }

            return TrigRatio(cosine, sine, radians, exactCosine, exactSine);
        }

        // where the exact values are known they decide, pole included: at an angle as large as 10²² degrees
        // the double only sees noise, cleans both the sine and the cosine to 0 and would report a pole at
        // every angle
        private MathValue TrigRatio(double numerator, double denominator, double radians,
            ExactValue? exactNumerator, ExactValue? exactDenominator)
        {
            if (exactDenominator != null)
            {
                if (exactDenominator.IsZero) return Fail(EvaluationError.Domain);
                return new MathValue(numerator / denominator, ExactValue.Divide(exactNumerator, exactDenominator));
            }

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


        // === exact angles ===

        // a Casio knows the exact values of every multiple of 15° and of nothing in between: sin 15 is
        // (√6−√2)/4, while sin 18 is a decimal although (√5−1)/4 would fit its forms; an angle is therefore
        // counted in steps of 15°, a quarter turn being six of them

        // sin 0°, 15°, 30° up to 90°; every other multiple of 15° has one of these, or its negative
        private static readonly ExactValue[] QuarterSines = BuildQuarterSines();

        private static ExactValue[] BuildQuarterSines()
        {
            ExactValue root2 = ExactValue.SquareRoot(ExactValue.FromInteger(2))!;
            ExactValue root3 = ExactValue.SquareRoot(ExactValue.FromInteger(3))!;
            ExactValue root6 = ExactValue.SquareRoot(ExactValue.FromInteger(6))!;
            ExactValue half = ExactValue.FromRational(new Rational(1, 2));
            ExactValue quarter = ExactValue.FromRational(new Rational(1, 4));

            return new[]
            {
                ExactValue.Zero,
                ExactValue.Multiply(ExactValue.Subtract(root6, root2), quarter)!,
                half,
                ExactValue.Multiply(root2, half)!,
                ExactValue.Multiply(root3, half)!,
                ExactValue.Multiply(ExactValue.Add(root6, root2), quarter)!,
                ExactValue.FromInteger(1)
            };
        }

        // the angle as a whole number of 15° steps within one turn; null when it has no exact value or is
        // not a multiple of 15°
        //
        // in radians only a rational multiple of π counts, and zero, which is a multiple of anything
        private int? AngleSteps(ExactValue? angle)
        {
            if (angle == null) return null;
            if (angle.IsZero) return 0;

            Rational steps;
            if (AngleMode == AngleMode.Radians)
            {
                if (!angle.TimesPi) return null;
                steps = angle.Terms[0].Coefficient * new Rational(12, 1);
            }
            else
            {
                if (!angle.TryGetRational(out Rational value)) return null;

                // 15° is 50/3 gradians
                steps = AngleMode == AngleMode.Gradians
                    ? value * new Rational(3, 50)
                    : value * new Rational(1, 15);
            }

            if (!steps.IsInteger) return null;

            return (int)((steps.Numerator % 24 + 24) % 24);
        }

        // the sine of so many steps, by the symmetries of the quarter turn table
        private static ExactValue? ExactSine(int? steps)
        {
            if (steps is not int step) return null;

            step = (step % 24 + 24) % 24;
            if (step <= 6) return QuarterSines[step];
            if (step <= 12) return QuarterSines[12 - step];
            if (step <= 18) return ExactValue.Negate(QuarterSines[step - 12]);

            return ExactValue.Negate(QuarterSines[24 - step]);
        }

        // the angle from −90° to 90° whose sine is the value, in steps, or null when it is none of the
        // table values
        private static int? SineSteps(ExactValue? value)
        {
            if (value == null) return null;

            for (int step = -6; step <= 6; step++)
            {
                if (value.Equals(ExactSine(step))) return step;
            }

            return null;
        }

        // the same for the tangent, from −75° to 75°, since ±90° has none
        private static int? TangentSteps(ExactValue? value)
        {
            if (value == null) return null;

            for (int step = -5; step <= 5; step++)
            {
                if (value.Equals(ExactValue.Divide(ExactSine(step), ExactSine(step + 6)))) return step;
            }

            return null;
        }

        // a quarter turn less, which takes sin⁻¹ to cos⁻¹ and tan⁻¹ to cot⁻¹
        private static int? Complement(int? steps)
        {
            return 6 - steps;
        }

        private static ExactValue? Reciprocal(ExactValue? value)
        {
            return ExactValue.Divide(ExactValue.FromInteger(1), value);
        }

        // the direction of the point (x, y) in steps, up to and including a half turn; the tangent of the
        // direction is y over x, and a point left of the axis lies a half turn away from what that gives
        private static int? DirectionSteps(ExactValue? x, ExactValue? y)
        {
            if (x == null || y == null) return null;
            if (x.IsZero) return y.Sign > 0 ? 6 : -6;

            int? steps = TangentSteps(ExactValue.Divide(y, x));
            if (steps is not int step || x.Sign > 0) return steps;

            return y.Sign >= 0 ? step + 12 : step - 12;
        }

        // so many steps in the unit currently selected: 15 degrees, 50/3 gradians or π/12 each
        private ExactValue? ExactAngle(int? steps)
        {
            if (steps is not int step) return null;

            return AngleMode switch
            {
                AngleMode.Radians => ExactValue.Multiply(ExactValue.Pi, ExactValue.FromRational(new Rational(step, 12))),
                AngleMode.Gradians => ExactValue.FromRational(new Rational(step * 50, 3)),
                _ => ExactValue.FromInteger(step * 15)
            };
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
