using System;
using System.Collections.Generic;
using System.Linq;
using FluentMath.Engines;
using FluentMath.Models;
using Xunit;

namespace FluentMath.Tests
{
    public class MathEvaluatorTests
    {
        // === helpers ===

        private static double Value(AngleMode mode, params string[] keys)
        {
            EvaluationResult result = new MathEvaluator(mode).Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result.Value;
        }

        private static double Value(params string[] keys)
        {
            return Value(AngleMode.Degrees, keys);
        }

        private static EvaluationError Error(params string[] keys)
        {
            EvaluationResult result = new MathEvaluator().Evaluate(Keys.Press(keys).RootTokens);

            Assert.False(result.IsSuccess, "expected a failure but got " + result.Value);
            return result.Error;
        }


        private static double ValueWithAnswer(double lastAnswer, params string[] keys)
        {
            var evaluator = new MathEvaluator { LastAnswer = lastAnswer };
            EvaluationResult result = evaluator.Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result.Value;
        }

        private static EvaluationError Error(AngleMode mode, params string[] keys)
        {
            EvaluationResult result = new MathEvaluator(mode).Evaluate(Keys.Press(keys).RootTokens);

            Assert.False(result.IsSuccess, "expected a failure but got " + result.Value);
            return result.Error;
        }


        // === associativity ===

        [Fact]
        public void ChainsDivisionsLeftToRight()
        {
            Assert.Equal(5, Value("100", "/", "10", "/", "2"));
        }

        [Fact]
        public void ChainsPowersRightToLeft()
        {
            // two to the ninth, not eight squared; the second power key lifts the 3 into its own base
            Assert.Equal(512, Value("2", "pow", "3", "pow", "2"));
        }

        [Fact]
        public void MultipliesImplicitlyInEveryShape()
        {
            Assert.Equal(-6, Value("-", "2", "(", "3", ")"));
            Assert.Equal(24, Value("2", "(", "3", ")", "(", "4", ")"));
            Assert.Equal(15, ValueWithAnswer(5, "3", "ans"));
            Assert.Equal(2 * Math.PI, Value("2", "pi"), 12);
        }

        // what a Casio answers: a product written without a sign is divided by as a whole
        [Fact]
        public void BindsAnImplicitProductTighterThanADivision()
        {
            Assert.Equal(1, Value("6", "/", "2", "(", "1", "+", "2", ")"));
            Assert.Equal(1 / (2 * Math.PI), Value("1", "/", "2", "pi"), 15);
            Assert.Equal(-1, Value("6", "/", "-", "2", "(", "3", ")"));
            Assert.Equal(1, Value("8", "/", "2", "(", "2", ")", "/", "2"));
        }

        [Fact]
        public void LeavesAWrittenTimesSignToTheLeftToRightOrder()
        {
            Assert.Equal(9, Value("6", "/", "2", "*", "(", "1", "+", "2", ")"));
            Assert.Equal(12, Value("2", "(", "3", ")", "!"));
        }


        // === reading ===

        private static string Read(params string[] keys)
        {
            return string.Concat(MathEvaluator.CloneWithImpliedBrackets(Keys.Press(keys).RootTokens)
                .Select(token => token.Value));
        }

        [Fact]
        public void BracketsAProductThatADivisionTakesWhole()
        {
            Assert.Equal("6/(2(1+2))", Read("6", "/", "2", "(", "1", "+", "2", ")"));
            Assert.Equal("1/(2pi)", Read("1", "/", "2", "pi"));
            Assert.Equal("6/(-2(3))", Read("6", "/", "-", "2", "(", "3", ")"));
            Assert.Equal("6/(2(8/(2(2))))", Read("6", "/", "2", "(", "8", "/", "2", "(", "2", ")", ")"));
        }

        [Fact]
        public void LeavesAFormulaThatReadsLeftToRightAlone()
        {
            Assert.Equal("6/2*3", Read("6", "/", "2", "*", "3"));
            Assert.Equal("6*2(3)", Read("6", "*", "2", "(", "3", ")"));
            Assert.Equal("2(3)/4", Read("2", "(", "3", ")", "/", "4"));
            Assert.Equal("6/-2", Read("6", "/", "-", "2"));
        }

        // the closing bracket the user never typed goes in first, so the new one pairs off with the new
        // opening one
        [Fact]
        public void ClosesABracketLeftOpenInsideTheProduct()
        {
            Assert.Equal("6/(2(1+2))", Read("6", "/", "2", "(", "1", "+", "2"));
        }

        [Fact]
        public void BracketsAProductInsideASlotAsWell()
        {
            List<MathToken> read = MathEvaluator.CloneWithImpliedBrackets(
                Keys.Press("frac", "6", "/", "2", "pi").RootTokens);

            FractionToken fraction = Assert.IsType<FractionToken>(Assert.Single(read));
            Assert.Equal("6/(2pi)", string.Concat(fraction.NumeratorTokens.Select(token => token.Value)));
        }

        [Fact]
        public void LeavesTheTreeItReadsAsItWasTyped()
        {
            MathInputManager manager = Keys.Press("6", "/", "2", "(", "1", "+", "2", ")");
            MathEvaluator.CloneWithImpliedBrackets(manager.RootTokens);

            Assert.Equal(8, manager.RootTokens.Count);
        }


        // === root edges ===

        [Fact]
        public void TakesAnIndexOtherThanAWholeOne()
        {
            Assert.Equal(0.5, Value("root", "-", "2", "right", "4"), 12);
            Assert.Equal(16, Value("root", "0.5", "right", "4"), 12);
        }

        [Fact]
        public void RefusesANegativeRadicandWithoutAnOddWholeIndex()
        {
            Assert.Equal(EvaluationError.Domain, Error("root", "2", "right", "-", "4"));
            Assert.Equal(EvaluationError.Domain, Error("root", "0.5", "right", "-", "4"));
            Assert.Equal(EvaluationError.Domain, Error("root", "0", "right", "8"));
            Assert.Equal(-2, Value("root", "3", "right", "-", "8"));
        }


        // === logarithm edges ===

        [Fact]
        public void RefusesALogarithmBaseThatHasNoLogarithm()
        {
            Assert.Equal(EvaluationError.Domain, Error("logb", "1", "right", "8"));
            Assert.Equal(EvaluationError.Domain, Error("logb", "0", "right", "8"));
            Assert.Equal(EvaluationError.Domain, Error("logb", "-", "2", "right", "8"));
        }

        [Fact]
        public void RefusesALogarithmArgumentThatHasNoLogarithm()
        {
            Assert.Equal(EvaluationError.Domain, Error("log", "0"));
            Assert.Equal(EvaluationError.Domain, Error("log", "-", "5"));
            Assert.Equal(EvaluationError.Domain, Error("fn:ln", "0"));
            Assert.Equal(0, Value("fn:ln", "1"));
        }
        // === precedence ===

        [Fact]
        public void AddsAndSubtractsLeftToRight()
        {
            Assert.Equal(5, Value("10", "-", "3", "-", "2"));
        }

        [Fact]
        public void MultipliesBeforeAdding()
        {
            Assert.Equal(14, Value("2", "+", "3", "*", "4"));
        }

        [Fact]
        public void ReadsARunOfDigitsAsOneValue()
        {
            Assert.Equal(50, Value("45", "+", "5"));
        }

        [Fact]
        public void MultipliesImplicitlyBeforeABracket()
        {
            Assert.Equal(27, Value("3", "(", "4", "+", "5", ")"));
        }

        [Fact]
        public void MultipliesImplicitlyBeforeAFunction()
        {
            Assert.Equal(1, Value("2", "fn:sin", "30"));
        }

        [Fact]
        public void TreatsALeadingMinusAsASign()
        {
            Assert.Equal(3, Value("-", "5", "+", "8"));
        }

        [Fact]
        public void ClosesAnOpenBracketOnEvaluation()
        {
            Assert.Equal(5, Value("(", "2", "+", "3"));
        }


        // === structured tokens ===

        [Fact]
        public void LiftsTheOperandIntoTheNumerator()
        {
            Assert.Equal(27.5, Value("5", "+", "45", "frac", "2"));
        }

        [Fact]
        public void RaisesTheOperandToThePower()
        {
            Assert.Equal(1024, Value("2", "pow", "10"));
        }

        [Fact]
        public void TakesABracketGroupAsThePowerBase()
        {
            Assert.Equal(9, Value("(", "1", "+", "2", ")", "pow", "2"));
        }

        [Fact]
        public void DefaultsAnEmptyRootIndexToTwo()
        {
            Assert.Equal(4, Value("sqrt", "16"));
        }

        [Fact]
        public void EvaluatesAnNthRoot()
        {
            Assert.Equal(2, Value("root", "3", "right", "8"));
        }

        [Fact]
        public void TakesTheOddRootOfANegativeRadicand()
        {
            Assert.Equal(-2, Value("root", "3", "right", "-", "8"));
        }

        [Fact]
        public void DefaultsAnEmptyLogarithmBaseToTen()
        {
            Assert.Equal(2, Value("log", "100"));
        }

        [Fact]
        public void EvaluatesALogarithmWithAChosenBase()
        {
            Assert.Equal(3, Value("logb", "2", "right", "8"));
        }

        [Fact]
        public void NestsAFractionInsideAnExponent()
        {
            Assert.Equal(2, Value("8", "pow", "1", "frac", "3"), 10);
        }

        [Fact]
        public void EvaluatesThePrefilledPowerBase()
        {
            Assert.Equal(1, Value("powe", "0"));
            Assert.Equal(Math.E, Value("powe", "1"), 12);
        }


        // === postfix ===

        [Fact]
        public void EvaluatesAFactorial()
        {
            Assert.Equal(120, Value("5", "!"));
        }

        [Fact]
        public void BindsAFactorialTighterThanALeadingSign()
        {
            Assert.Equal(-120, Value("-", "5", "!"));
        }

        [Fact]
        public void EvaluatesAReciprocal()
        {
            Assert.Equal(0.25, Value("4", "inv"));
        }

        [Fact]
        public void EvaluatesAPercentAsADivisionByAHundred()
        {
            Assert.Equal(0.5, Value("50", "%"));
        }

        [Fact]
        public void CarriesAPostfixThroughAnImplicitMultiplication()
        {
            // the implicit branch has to go through the postfix level too, otherwise the factorial is
            // left lying in the token list and the whole formula comes back as a syntax error
            Assert.Equal(12, Value("2", "(", "3", ")", "!"));
            Assert.Equal(6, Value("(", "1", "+", "2", ")", "(", "2", ")", "!"));
            Assert.Equal(1, Value("2", "(", "2", ")", "inv"));
        }

        [Fact]
        public void ChainsTwoPostfixKeys()
        {
            Assert.Equal(0.5, Value("2", "!", "inv"));
        }


        // === scientific exponent ===

        [Fact]
        public void ReadsAScientificExponentAsPartOfTheNumber()
        {
            Assert.Equal(300000, Value("3", "exp", "5"));
        }

        [Fact]
        public void ReadsAScientificExponentAsTheMultiplicationItIs()
        {
            // the EXP key spells out times ten to the n and binds like any other multiplication, so this
            // reads left to right as a third of a hundred thousand rather than as one over three hundred
            // thousand; that changed when the key stopped being a token of its own
            Assert.Equal(100000.0 / 3.0, Value("1", "/", "3", "exp", "5"), 9);
        }

        [Fact]
        public void ReadsANegativeScientificExponent()
        {
            Assert.Equal(0.03, Value("3", "exp", "-", "2"), 12);
        }


        // === angles ===

        [Fact]
        public void ReadsTheArgumentInTheSelectedUnit()
        {
            Assert.Equal(0.5, Value(AngleMode.Degrees, "fn:sin", "30"));
            Assert.Equal(1, Value(AngleMode.Gradians, "fn:sin", "100"));
            Assert.Equal(0, Value(AngleMode.Radians, "fn:sin", "0"));
        }

        [Fact]
        public void ReturnsAnInverseTrigResultInTheSelectedUnit()
        {
            Assert.Equal(90, Value(AngleMode.Degrees, "fn:arcsin", "1"), 10);
            Assert.Equal(100, Value(AngleMode.Gradians, "fn:arcsin", "1"), 10);
            Assert.Equal(Math.PI / 2, Value(AngleMode.Radians, "fn:arcsin", "1"), 10);
        }

        [Fact]
        public void RoundsTheTrigNoiseAwayAtAWholeTurn()
        {
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:sin", "180"));
        }


        // === hyperbolic ===

        [Fact]
        public void ReadsAHyperbolicArgumentAsAPlainReal()
        {
            double inDegrees = Value(AngleMode.Degrees, "fn:sinh", "1");
            double inRadians = Value(AngleMode.Radians, "fn:sinh", "1");

            Assert.Equal(Math.Sinh(1), inDegrees, 12);
            Assert.Equal(inDegrees, inRadians);
        }

        [Fact]
        public void InvertsTheHyperbolicFunctions()
        {
            Assert.Equal(1, Value("fn:arsinh", "fn:sinh", "1"), 10);
            Assert.Equal(2, Value("fn:arcosh", "fn:cosh", "2"), 10);
            Assert.Equal(0.5, Value("fn:artanh", "fn:tanh", "0.5"), 10);
        }

        [Fact]
        public void EvaluatesAnAbsoluteValue()
        {
            Assert.Equal(5, Value("fn:abs", "-", "5"));
        }


        // === reciprocal trigonometry ===

        [Fact]
        public void EvaluatesTheReciprocalsOfTheTrigFunctions()
        {
            Assert.Equal(2, Value("fn:sec", "60"));
            Assert.Equal(2, Value("fn:csc", "30"));
            Assert.Equal(1, Value("fn:cot", "45"));
            Assert.Equal(0, Value("fn:cot", "90"));
            Assert.Equal(2, Value(AngleMode.Gradians, "fn:csc", "100", "/", "3"), 12);
        }

        [Fact]
        public void FindsThePolesOfTheReciprocals()
        {
            Assert.Equal(EvaluationError.Domain, Error("fn:sec", "90"));
            Assert.Equal(EvaluationError.Domain, Error("fn:csc", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:csc", "180"));
            Assert.Equal(EvaluationError.Domain, Error("fn:cot", "0"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Radians, "fn:sec", "pi", "/", "2"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Radians, "fn:cot", "pi"));
        }

        [Fact]
        public void InvertsTheReciprocalsInTheSelectedUnit()
        {
            Assert.Equal(60, Value("fn:arcsec", "2"), 10);
            Assert.Equal(30, Value("fn:arccsc", "2"), 10);
            Assert.Equal(45, Value("fn:arccot", "1"), 10);
            Assert.Equal(Math.PI / 3, Value(AngleMode.Radians, "fn:arcsec", "2"), 12);
            Assert.Equal(EvaluationError.Domain, Error("fn:arcsec", "0.5"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arccsc", "-", "0.5"));
        }

        // between 0° and 180°, so it runs on through zero rather than jumping from 90° to −90°
        [Fact]
        public void AnswersTheInverseCotangentFromAHalfTurnAboveZero()
        {
            Assert.Equal(90, Value("fn:arccot", "0"), 10);
            Assert.Equal(135, Value("fn:arccot", "-", "1"), 10);
            Assert.Equal(Math.PI / 2, Value(AngleMode.Radians, "fn:arccot", "0"), 12);
        }

        [Fact]
        public void EvaluatesTheHyperbolicReciprocalsAndTheirInverses()
        {
            Assert.Equal(1, Value("fn:sech", "0"));
            Assert.Equal(1 / Math.Sinh(1), Value("fn:csch", "1"), 14);
            Assert.Equal(1 / Math.Tanh(1), Value("fn:coth", "1"), 14);
            Assert.Equal(0, Value("fn:arsech", "1"));
            Assert.Equal(Math.Asinh(1), Value("fn:arcsch", "1"), 14);
            Assert.Equal(Math.Atanh(0.5), Value("fn:arcoth", "2"), 14);
            Assert.Equal(Value(AngleMode.Degrees, "fn:coth", "2"), Value(AngleMode.Radians, "fn:coth", "2"));
        }

        [Fact]
        public void KeepsTheHyperbolicReciprocalsInsideTheirDomains()
        {
            Assert.Equal(EvaluationError.Domain, Error("fn:csch", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:coth", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arsech", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arsech", "2"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arcsch", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arcoth", "1"));
        }


        // === whole numbers ===

        [Fact]
        public void CutsTowardsZeroForIntAndDownwardsForIntg()
        {
            Assert.Equal(-2, Value("fn:int", "-", "2.5"));
            Assert.Equal(-3, Value("fn:intg", "-", "2.5"));
            Assert.Equal(-3, Value("fn:floor", "-", "2.5"));
            Assert.Equal(-2, Value("fn:ceil", "-", "2.5"));
            Assert.Equal(3, Value("fn:ceil", "2.1"));
        }

        // 10(1−0.9) is 0.9999999999999998 as a double and a plain 1 at the fifteen digits a Casio holds
        [Fact]
        public void JudgesAWholeNumberAtTheDigitsACasioComputesWith()
        {
            Assert.Equal(1, Value("fn:int", "10", "(", "1", "-", "0.9", ")"));
            Assert.Equal(3, Value("fn:gcd", "0.1", "*", "30", "right", "6"));
        }

        [Fact]
        public void FindsTheGreatestCommonDivisorAndTheLeastCommonMultiple()
        {
            Assert.Equal(6, Value("fn:gcd", "-", "12", "right", "18"));
            Assert.Equal(12, Value("fn:lcm", "4", "right", "6"));
            Assert.Equal(0, Value("fn:lcm", "0", "right", "5"));
            Assert.Equal(EvaluationError.Domain, Error("fn:gcd", "12.5", "right", "5"));
        }

        [Fact]
        public void CountsPermutationsAndCombinations()
        {
            Assert.Equal(20, Value("5", "npr", "2"));
            Assert.Equal(10, Value("5", "ncr", "2"));
            Assert.Equal(1, Value("0", "ncr", "0"));
            Assert.Equal(118264581564861424, Value("60", "ncr", "30"), 0);
        }

        // what a Casio answers for each of them: C binds tighter than divided by and looser than a sign
        [Fact]
        public void RanksACombinationTheWayACasioDoes()
        {
            Assert.Equal(12, Value("12", "/", "2", "ncr", "2"));
            Assert.Equal(20, Value("2", "*", "5", "ncr", "2"));
            Assert.Equal(10, Value("5", "ncr", "2", "!"));
            Assert.Equal(EvaluationError.Domain, Error("-", "5", "ncr", "2"));
        }

        [Fact]
        public void CountsNothingForAnImpossibleSelection()
        {
            Assert.Equal(EvaluationError.Domain, Error("5", "ncr", "7"));
            Assert.Equal(EvaluationError.Domain, Error("2.5", "ncr", "2"));
            Assert.Equal(EvaluationError.Domain, Error("5", "npr", "-", "1"));
            Assert.Equal(EvaluationError.Overflow, Error("200", "npr", "200"));
        }

        [Fact]
        public void BracketsACombinationThatADivisionTakesWhole()
        {
            Assert.Equal("12/(2C2)", Read("12", "/", "2", "ncr", "2"));
            Assert.Equal("12/(2(3)C2)", Read("12", "/", "2", "(", "3", ")", "ncr", "2"));
        }


        // === random numbers ===

        private static MathEvaluator Seeded()
        {
            return new MathEvaluator { RandomSource = new Random(7) };
        }

        // three decimals from 0.000 to 0.999, the way a Casio draws Ran#
        [Fact]
        public void DrawsRanAsThreeDecimalsBelowOne()
        {
            MathEvaluator evaluator = Seeded();

            for (int draw = 0; draw < 200; draw++)
            {
                double value = evaluator.Evaluate(Keys.Press("rand").RootTokens).Value;

                Assert.InRange(value, 0, 0.999);
                Assert.Equal(Math.Round(value, 3), value);
            }
        }

        [Fact]
        public void DrawsAWholeNumberBetweenBothBoundsIncluded()
        {
            MathEvaluator evaluator = Seeded();
            var seen = new HashSet<double>();

            for (int draw = 0; draw < 200; draw++)
            {
                EvaluationResult result = evaluator.Evaluate(Keys.Press("fn:ranint", "1", "right", "6").RootTokens);
                seen.Add(result.Value);
            }

            Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6 }, seen.OrderBy(value => value));
        }

        // RanInt#(6,1) is the Argument ERROR a Casio gives
        [Fact]
        public void RefusesBoundsThatAreTheWrongWayRoundOrNotWhole()
        {
            Assert.Equal(EvaluationError.Argument, Error("fn:ranint", "6", "right", "1"));
            Assert.Equal(EvaluationError.Argument, Error("fn:ranint", "1", "right", "1"));
            Assert.Equal(EvaluationError.Argument, Error("fn:ranint", "1.5", "right", "3"));
        }


        // === rounding to decimals ===

        // 2.675 is a hair below itself as a double, and still rounds up the way it reads
        [Fact]
        public void RoundsToAWholeNumberOfDecimalsTheWayTheNumberReads()
        {
            Assert.Equal(2.68, Value("fn:rndfix", "2.675", "right", "2"));
            Assert.Equal(-3, Value("fn:rndfix", "-", "2.5", "right", "0"));
            Assert.Equal(0.333, Value("fn:rndfix", "1", "/", "3", "right", "3"));
        }

        [Fact]
        public void TakesNoMoreDecimalsThanFixDoes()
        {
            Assert.Equal(EvaluationError.Argument, Error("fn:rndfix", "1", "right", "10"));
            Assert.Equal(EvaluationError.Argument, Error("fn:rndfix", "1", "right", "1.5"));
            Assert.Equal(EvaluationError.Argument, Error("fn:rndfix", "1", "right", "-", "1"));
        }


        // === decimal prefixes ===

        [Fact]
        public void ScalesByTheDecimalPrefix()
        {
            Assert.Equal(5000, Value("5", "pre:kilo"));
            Assert.Equal(0.005, Value("5", "pre:milli"));
            Assert.Equal(3e-6, Value("3", "pre:micro"));
            Assert.Equal(1e18, Value("1", "pre:exa"));
            Assert.Equal(500000, Value("2", "pre:kilo", "/", "4", "pre:milli"));
        }

        // a prefix is part of its operand, so the square key lifts 2k whole into the base
        [Fact]
        public void BindsAPrefixAsTightlyAsAFactorial()
        {
            Assert.Equal(4000000, Value("2", "pre:kilo", "pow", "2"));
        }


        // === Ans ===

        [Fact]
        public void ResolvesAnsToTheLastAnswer()
        {
            var evaluator = new MathEvaluator { LastAnswer = 7 };
            EvaluationResult result = evaluator.Evaluate(Keys.Press("ans", "*", "2").RootTokens);

            Assert.True(result.IsSuccess);
            Assert.Equal(14, result.Value);
        }

        [Fact]
        public void CarriesTheFullAnswerRatherThanTheDisplayedDigits()
        {
            var evaluator = new MathEvaluator { LastAnswer = 1.0 / 3.0 };
            EvaluationResult result = evaluator.Evaluate(Keys.Press("ans", "*", "3").RootTokens);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value, 15);
        }


        // === angle edges ===

        [Fact]
        public void FindsTheTangentPoleInEveryUnitThatHasOne()
        {
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Degrees, "fn:tan", "90"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Degrees, "fn:tan", "270"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Degrees, "fn:tan", "-", "90"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Gradians, "fn:tan", "100"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Gradians, "fn:tan", "300"));
        }

        // no double lands exactly on pi over two, but the cosine there is nothing but rounding noise, and a
        // Casio answers tan(pi/2) with a Math ERROR
        [Fact]
        public void FindsTheTangentPoleInRadiansToo()
        {
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Radians, "fn:tan", "pi", "/", "2"));
            Assert.Equal(EvaluationError.Domain, Error(AngleMode.Radians, "fn:tan", "3", "pi", "/", "2"));
        }

        [Fact]
        public void LandsOnAnExactZeroAtEveryZeroOfSineAndCosine()
        {
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:cos", "90"));
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:cos", "270"));
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:tan", "180"));
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:sin", "-", "180"));
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:sin", "180000000"));
            Assert.Equal(0, Value(AngleMode.Radians, "fn:sin", "pi"));
            Assert.Equal(0, Value(AngleMode.Radians, "fn:sin", "3", "pi"));
            Assert.Equal(0, Value(AngleMode.Radians, "fn:cos", "pi", "/", "2"));
        }

        // the noise that is snapped to zero is the size of the angle times the precision of a double, and
        // a result of a very small angle is far above that however small it is
        [Fact]
        public void KeepsEveryDigitOfAVerySmallTrigResult()
        {
            double sine = Value(AngleMode.Degrees, "fn:sin", "0.0000001");
            Assert.Equal(1.74532925199433, sine * 1e9, 13);

            double tangent = Value(AngleMode.Radians, "fn:tan", "0.000000000001");
            Assert.Equal(1, tangent * 1e12, 13);
        }

        // twelve digits of pi are not pi, and the difference is a real result rather than noise
        [Fact]
        public void KeepsASmallResultBesideAZeroThatWasNotQuiteHit()
        {
            double sine = Value(AngleMode.Radians, "fn:sin", "3.14159265359");

            Assert.Equal(-2.06823107, sine * 1e13, 8);
        }

        // cut to fifteen digits, sin 30 is exactly one half and the difference is a real zero
        [Fact]
        public void LetsAnExactTrigValueCompareEqual()
        {
            Assert.Equal(1, Value(AngleMode.Degrees, "fn:tan", "45"));
            Assert.Equal(0.5, Value(AngleMode.Degrees, "fn:cos", "60"));
            Assert.Equal(EvaluationError.DivideByZero,
                Error("1", "/", "(", "fn:sin", "30", "right", "-", "0.5", ")"));
        }

        [Fact]
        public void ReadsAWholeTurnInEveryUnit()
        {
            Assert.Equal(0, Value(AngleMode.Degrees, "fn:sin", "360"));
            Assert.Equal(0, Value(AngleMode.Gradians, "fn:sin", "400"));
            Assert.Equal(0, Value(AngleMode.Radians, "fn:sin", "0"));
        }


        // === postfix edges ===

        [Fact]
        public void TakesTheFactorialOfZeroAndOfTheLargestValueThatHasOne()
        {
            Assert.Equal(1, Value("0", "!"));
            Assert.Equal(1, Value("1", "!"));

            EvaluationResult largest = new MathEvaluator().Evaluate(Keys.Press("170", "!").RootTokens);
            Assert.True(largest.IsSuccess);
        }

        [Fact]
        public void RefusesTheFactorialOfANegativeWholeNumber()
        {
            Assert.Equal(EvaluationError.Domain, Error("(", "-", "5", ")", "!"));
        }

        [Fact]
        public void ChainsPercentSigns()
        {
            Assert.Equal(0.005, Value("50", "%", "%"), 12);
        }

        [Fact]
        public void TakesAPostfixInsideASlot()
        {
            Assert.Equal(1.0 / 6.0, Value("1", "frac", "3", "!"), 12);
            Assert.Equal(120, ValueWithAnswer(5, "ans", "!"));
        }


        // === scientific edges ===

        [Fact]
        public void TakesAScientificExponentOnADecimalMantissa()
        {
            Assert.Equal(1500, Value("1.5", "exp", "3"));
        }

        [Fact]
        public void TakesAFractionInAScientificExponent()
        {
            Assert.Equal(3 * Math.Sqrt(10), Value("3", "exp", "1", "frac", "2"), 10);
        }

        [Fact]
        public void TakesAScientificExponentInsideASlot()
        {
            // Right steps out of the exponent first, since Down does nothing in a slot that has no
            // neighbour above or below it
            Assert.Equal(500, Value("frac", "2", "exp", "3", "right", "down", "4"));
        }

        [Fact]
        public void ReportsSyntaxForAnEmptyScientificExponent()
        {
            Assert.Equal(EvaluationError.Syntax, Error("3", "exp"));
        }


        // === states the keys can reach but the grammar cannot read ===

        [Fact]
        public void ReportsSyntaxForAScientificTokenLeftWithoutItsMantissa()
        {
            // deleting the 3 out of 3x10^5 is an ordinary backspace away, and what is left has no
            // reading at all; it has to come back as an error rather than as an exception
            // two steps left is now inside the ten, so backspace takes a digit off it rather than
            // stranding the exponent
            Assert.Equal(3, Value("3", "exp", "5", "left", "left", "back"));
        }

        [Fact]
        public void ReportsSyntaxWhenAFractionKeySplitsANumberFromItsExponent()
        {
            Assert.Equal(EvaluationError.Syntax, Error("3", "exp", "5", "left", "left", "frac"));
        }


        // === nesting ===

        [Fact]
        public void ClosesABracketLeftOpenInsideASlot()
        {
            Assert.Equal(1, Value("frac", "(", "1", "+", "2", "down", "3"));
        }

        [Fact]
        public void NestsAStructureInsideEveryOtherStructure()
        {
            Assert.Equal(2, Value("sqrt", "1", "frac", "0.25"), 12);
            Assert.Equal(3, Value("log", "10", "pow", "3"), 12);
            Assert.Equal(4, Value("2", "pow", "sqrt", "4"), 12);

            // Right leaves the function argument first, so the reciprocal lands on the whole cosine
            // rather than on the 60 inside it
            Assert.Equal(0.5, Value("1", "frac", "fn:cos", "60", "right", "inv"), 12);
        }

        // === failures ===

        [Fact]
        public void ReportsSyntaxForAnEmptySlot()
        {
            Assert.Equal(EvaluationError.Syntax, Error("3", "frac"));
        }

        [Fact]
        public void ReportsSyntaxForAStrayClosingBracket()
        {
            Assert.Equal(EvaluationError.Syntax, Error("2", ")", "3"));
        }

        [Fact]
        public void ReportsDivideByZero()
        {
            Assert.Equal(EvaluationError.DivideByZero, Error("5", "/", "0"));
            Assert.Equal(EvaluationError.DivideByZero, Error("0", "inv"));
        }

        [Fact]
        public void ReportsDomainOutsideAFunctionsRange()
        {
            Assert.Equal(EvaluationError.Domain, Error("log", "0"));
            Assert.Equal(EvaluationError.Domain, Error("fn:tan", "90"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arcsin", "2"));
            Assert.Equal(EvaluationError.Domain, Error("fn:arcosh", "0.5"));
            Assert.Equal(EvaluationError.Domain, Error("fn:artanh", "1"));
        }

        [Fact]
        public void ReportsDomainForAZeroBaseThatCannotBeRaised()
        {
            // the FX-991 answers Math ERROR to both; Math.Pow would hand back 1 and an infinity
            Assert.Equal(EvaluationError.Domain, Error("0", "pow", "0"));
            Assert.Equal(EvaluationError.Domain, Error("0", "pow", "-", "1"));
            Assert.Equal(0, Value("0", "pow", "0.5"));
            Assert.Equal(1, Value("5", "pow", "0"));
        }

        [Fact]
        public void ReportsDomainForAFactorialThatHasNone()
        {
            Assert.Equal(EvaluationError.Domain, Error("0.5", "!"));
        }

        [Fact]
        public void ReportsOverflowPastTheLargestFactorial()
        {
            Assert.Equal(EvaluationError.Overflow, Error("171", "!"));
        }

        [Fact]
        public void ReadsEmptyInputAsZero()
        {
            EvaluationResult result = new MathEvaluator().Evaluate(new MathInputManager().RootTokens);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value);
        }
    }
}
