using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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


        // === mixed fractions ===

        [Fact]
        public void AddsTheWholePartToTheFraction()
        {
            Assert.Equal(7.0 / 3.0, Value("2", "mixed", "1", "right", "3"), 12);
            Assert.Equal(1.5, Value("mixed", "1", "right", "1", "right", "2"));
        }

        // measured on the Casio: minus, 1 1/2, plus 1 is −1/2
        [Fact]
        public void TakesALeadingMinusAsTheSignOfTheWholeMixedNumber()
        {
            Assert.Equal(-0.5, Value("-", "1", "mixed", "1", "right", "2", "right", "+", "1"));
        }

        // measured on the Casio: a whole part of 1 with −1 over 2 is −3/2, not 1/2
        [Fact]
        public void MakesTheWholeNumberNegativeWhenOnePartIs()
        {
            Assert.Equal(-1.5, Value("1", "mixed", "-", "1", "right", "2"));
            Assert.Equal(-1.5, Value("1", "mixed", "1", "right", "-", "2"));
            Assert.Equal(-1.5, Value("mixed", "-", "1", "right", "1", "right", "2"));
        }

        [Fact]
        public void CancelsTwoNegativeParts()
        {
            Assert.Equal(1.5, Value("mixed", "-", "1", "right", "-", "1", "right", "2"));
        }

        [Fact]
        public void RefusesAPartThatIsNotAWholeNumber()
        {
            Assert.Equal(EvaluationError.Syntax, Error("1.5", "mixed", "1", "right", "2"));
            Assert.Equal(EvaluationError.Syntax, Error("1", "mixed", "0.5", "right", "2"));
            Assert.Equal(EvaluationError.Syntax, Error("1", "mixed", "1", "right", "2.5"));
        }

        // a part is an expression like any slot, judged whole at the precision a Casio computes with
        [Fact]
        public void JudgesAComputedPartAtCasioPrecision()
        {
            Assert.Equal(3.5, Value("mixed", "0.1", "*", "30", "right", "1", "right", "2"));
        }

        [Fact]
        public void FailsOnAZeroDenominatorOrAnEmptyPart()
        {
            Assert.Equal(EvaluationError.DivideByZero, Error("1", "mixed", "1", "right", "0"));
            Assert.Equal(EvaluationError.Syntax, Error("1", "mixed", "1"));
            Assert.Equal(EvaluationError.Syntax, Error("mixed", "right", "1", "right", "2"));
        }

        [Fact]
        public void IsOneOperandToTheKeysAfterIt()
        {
            Assert.Equal(2.25, Value("1", "mixed", "1", "right", "2", "right", "pow", "2"));
            Assert.Equal(3, Value("1", "mixed", "1", "right", "2", "right", "(", "2", ")"));

            // the token has no text of its own, so what is left is the bracket pair around it and the (2)
            Assert.Equal("6/((2))", Read("6", "/", "1", "mixed", "1", "right", "2", "right", "(", "2", ")"));
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


        // === division with remainder ===

        private static EvaluationResult Result(AngleMode mode, params string[] keys)
        {
            EvaluationResult result = new MathEvaluator(mode).Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result;
        }

        private static EvaluationResult Result(params string[] keys) => Result(AngleMode.Degrees, keys);

        [Fact]
        public void ShowsTheQuotientAndTheRemainderOfAWholeCalculation()
        {
            EvaluationResult result = Result("17", "divr", "5");

            Assert.Equal(ResultKind.QuotientRemainder, result.Kind);
            Assert.Equal(3, result.Value);
            Assert.Equal(2, result.Second);
        }

        // measured on the Casio: 10+17÷R6 and 17÷R6+10 are both 12
        [Fact]
        public void HandsOnOnlyTheQuotientInsideACalculation()
        {
            Assert.Equal(ResultKind.Single, Result("10", "+", "17", "divr", "6").Kind);
            Assert.Equal(12, Value("10", "+", "17", "divr", "6"));
            Assert.Equal(12, Value("17", "divr", "6", "+", "10"));
            Assert.Equal(6, Value("17", "divr", "5", "*", "2"));
            Assert.Equal(ResultKind.Single, Result("(", "17", "divr", "5", ")").Kind);
        }

        // the division is the last operation, so the remainder is the one of 34÷R5
        [Fact]
        public void KeepsTheRemainderOfTheLastOperation()
        {
            EvaluationResult result = Result("2", "*", "17", "divr", "5");

            Assert.Equal(6, result.Value);
            Assert.Equal(4, result.Second);
        }

        // measured on the Casio: −17÷R5 is −17/5
        [Fact]
        public void TurnsIntoAPlainDivisionForANegativeOrBrokenOperand()
        {
            Assert.Equal(-3.4, Value("-", "17", "divr", "5"), 12);
            Assert.Equal(3.5, Value("17.5", "divr", "5"), 12);
            Assert.Equal(ResultKind.Single, Result("17", "divr", "-", "5").Kind);
            Assert.Equal(EvaluationError.DivideByZero, Error("17", "divr", "0"));
        }

        [Fact]
        public void DividesWithRemainderByAWholeImplicitProduct()
        {
            EvaluationResult result = Result("17", "divr", "2", "(", "3", ")");

            Assert.Equal(2, result.Value);
            Assert.Equal(5, result.Second);
            Assert.Equal("17÷R(2(3))", Read("17", "divr", "2", "(", "3", ")"));
        }


        // === coordinates ===

        // measured on the Casio in Rad: r=5; θ=0.927295218
        [Fact]
        public void ConvertsToPolarInTheAngleUnitSelected()
        {
            EvaluationResult radians = Result(AngleMode.Radians, "fn:pol", "3", "right", "4");
            Assert.Equal(ResultKind.Polar, radians.Kind);
            Assert.Equal(5, radians.Value);
            Assert.Equal(Math.Atan2(4, 3), radians.Second, 15);

            Assert.Equal(53.13010235415598, Result("fn:pol", "3", "right", "4").Second, 12);
        }

        // measured on the Casio: Pol(−1,0) is π, and the origin is a Math ERROR
        [Fact]
        public void TakesTheHalfTurnItselfAndRefusesTheOrigin()
        {
            Assert.Equal(Math.PI, Result(AngleMode.Radians, "fn:pol", "-", "1", "right", "0").Second);
            Assert.Equal(Math.PI, Result(AngleMode.Radians, "fn:pol", "-", "1", "right", "-", "0").Second);
            Assert.Equal(EvaluationError.Domain, Error("fn:pol", "0", "right", "0"));
        }

        // measured on the Casio in Rad: x=0.3085028998; y=−1.976063248
        [Fact]
        public void ConvertsToRectangularThroughTheCleanedSineAndCosine()
        {
            EvaluationResult radians = Result(AngleMode.Radians, "fn:rec", "2", "right", "30");
            Assert.Equal(ResultKind.Rectangular, radians.Kind);
            Assert.Equal(0.3085028998, radians.Value, 10);
            Assert.Equal(-1.976063248, radians.Second, 9);

            EvaluationResult degrees = Result("fn:rec", "1", "right", "90");
            Assert.Equal(0, degrees.Value);
            Assert.Equal(1, degrees.Second);
        }

        // measured on the Casio: 1+Pol(3,4) and Pol(3,4)+1 are both 6
        [Fact]
        public void HandsOnTheFirstValueInsideACalculation()
        {
            Assert.Equal(6, Value("1", "+", "fn:pol", "3", "right", "4"));
            Assert.Equal(6, Value("fn:pol", "3", "right", "4", "right", "+", "1"));
            Assert.Equal(ResultKind.Single, Result("1", "+", "fn:pol", "3", "right", "4").Kind);
        }


        // === a result carried on ===

        [Fact]
        public void ReadsSeededDigitsAsTheFullValueUntilOneOfThemIsEdited()
        {
            MathInputManager manager = new MathInputManager();
            manager.SeedWithValue("0.333333333333", 1.0 / 3.0);
            manager.AddOperator("*");
            manager.AddNumber("3");

            Assert.Equal(1, new MathEvaluator().Evaluate(manager.RootTokens).Value);

            // one digit more is a different number, typed by hand
            MathInputManager edited = new MathInputManager();
            edited.SeedWithValue("0.333333333333", 1.0 / 3.0);
            edited.AddNumber("3");

            Assert.Equal(0.3333333333333, new MathEvaluator().Evaluate(edited.RootTokens).Value, 15);
        }

        [Fact]
        public void KeepsTheSignOfASeededNegativeValueOutsideItsDigits()
        {
            MathInputManager manager = new MathInputManager();
            manager.SeedWithValue("-0.333333333333", -1.0 / 3.0);
            manager.AddOperator("*");
            manager.AddNumber("3");

            Assert.Equal(-1, new MathEvaluator().Evaluate(manager.RootTokens).Value);
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

        private static double Rounded(NumberFormat format, params string[] keys)
        {
            EvaluationResult result = new MathEvaluator { NumberFormat = format }.Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess);
            return result.Value;
        }

        // measured on the Casio: in Fix 2, Rnd(1÷3) is 0.33 and three of it 0.99
        [Fact]
        public void RoundsToWhatTheNumberFormatWrites()
        {
            NumberFormat fix2 = new NumberFormat(NumberNotation.Fix, 2);

            Assert.Equal(0.33, Rounded(fix2, "fn:rnd", "1", "/", "3"));
            Assert.Equal(0.99, Rounded(fix2, "fn:rnd", "1", "/", "3", "right", "*", "3"), 15);
            Assert.Equal(2.68, Rounded(fix2, "fn:rnd", "2.675"));

            Assert.Equal(1230, Rounded(new NumberFormat(NumberNotation.Sci, 3), "fn:rnd", "1234"));
            Assert.Equal(0.333333333333, Rounded(NumberFormat.Default, "fn:rnd", "1", "/", "3"));
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


        // === exact values ===

        private static ExactValue? Exact(AngleMode mode, params string[] keys)
        {
            EvaluationResult result = new MathEvaluator(mode).Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result.FirstValue.Exact;
        }

        private static ExactValue? Exact(params string[] keys)
        {
            return Exact(AngleMode.Degrees, keys);
        }

        private static ExactValue Whole(long value) => ExactValue.FromInteger(value);

        private static ExactValue Fraction(long numerator, long denominator)
        {
            return ExactValue.FromRational(new Rational(numerator, denominator));
        }

        private static ExactValue Root(long radicand) => ExactValue.SquareRoot(Whole(radicand))!;

        // the double is read off the exact value, so the two cannot disagree about a whole number
        [Fact]
        public void ReadsTheDoubleOffTheExactValue()
        {
            Assert.Equal(0.3, Value("0.1", "+", "0.2"));
            Assert.Equal(2, Value("sqrt", "2", "right", "*", "sqrt", "2"));
            Assert.Equal(1, Value("1", "/", "3", "*", "3"));
        }

        [Fact]
        public void KeepsEveryOperationWithAnExactAnswerExact()
        {
            Assert.Equal(ExactValue.FromInteger(BigInteger.Parse("15511210043330985984000000")), Exact("25", "!"));
            Assert.Equal(Whole(120), Exact("10", "ncr", "3"));
            Assert.Equal(Whole(20), Exact("5", "npr", "2"));
            Assert.Equal(Fraction(1, 100), Exact("1", "%"));
            Assert.Equal(Fraction(1, 200000), Exact("5", "pre:micro"));
            Assert.Equal(Fraction(1, 2), Exact("2", "inv"));
            Assert.Equal(Fraction(-3, 2), Exact("mixed", "1", "right", "-", "1", "right", "2"));
            Assert.Equal(Whole(-3), Exact("fn:intg", "-", "2.5"));
            Assert.Equal(Whole(6), Exact("fn:gcd", "-", "12", "right", "18"));
            Assert.Equal(Fraction(-17, 5), Exact("-", "17", "divr", "5"));
            Assert.Equal(Fraction(107, 40), Exact("fn:rndfix", "2.675", "right", "3"));
            Assert.Equal(Whole(8), Exact("2", "pow", "3"));
            Assert.Equal(Whole(2), Exact("root", "3", "right", "8"));
            Assert.Equal(Fraction(1, 4), Exact("fn:abs", "-", "0.25"));
        }

        // Rnd hands back the number the display writes, as the fraction it is
        [Fact]
        public void RoundsToAnExactDecimalWithRnd()
        {
            var evaluator = new MathEvaluator { NumberFormat = new NumberFormat(NumberNotation.Fix, 2) };
            EvaluationResult result = evaluator.Evaluate(Keys.Press("fn:rnd", "1", "/", "3").RootTokens);

            Assert.Equal(Fraction(33, 100), result.FirstValue.Exact);
        }

        [Fact]
        public void DrawsRanAsAnExactFraction()
        {
            MathEvaluator evaluator = Seeded();
            EvaluationResult result = evaluator.Evaluate(Keys.Press("rand").RootTokens);

            Assert.True(result.FirstValue.Exact!.TryGetRational(out Rational value));
            Assert.Equal(result.Value, value.ToDouble());
            Assert.Equal(0, 1000 % (int)value.Denominator);
        }

        [Fact]
        public void DropsTheExactValueWhereThereIsNone()
        {
            Assert.Null(Exact("fn:ln", "2"));
            Assert.Null(Exact("e"));
            Assert.Null(Exact("log", "100"));
            Assert.Null(Exact("fn:sinh", "0"));
            Assert.Null(Exact("4", "pow", "0.5"));
            Assert.Null(Exact("root", "3", "right", "2"));
            Assert.Null(Exact("fn:sin", "18"));
        }

        // the angle counts in steps of 15°, round the whole turn, in every unit
        [Fact]
        public void KnowsTheSineOfEveryMultipleOfFifteenDegrees()
        {
            Assert.Equal(Fraction(1, 2), Exact("fn:sin", "390"));
            Assert.Equal(ExactValue.Multiply(Root(2), Fraction(1, 2)), Exact("fn:cos", "-", "45"));
            Assert.Equal(ExactValue.Zero, Exact("fn:sin", "180"));
            Assert.Equal(Whole(-1), Exact("fn:tan", "135"));
            Assert.Equal(Whole(2), Exact("fn:sec", "60"));
            Assert.Equal(Whole(2), Exact("fn:csc", "30"));
            Assert.Equal(Whole(1), Exact("fn:cot", "45"));

            Assert.Equal(Fraction(1, 2), Exact(AngleMode.Radians, "fn:cos", "pi", "/", "3"));
            Assert.Equal(Whole(1), Exact(AngleMode.Gradians, "fn:sin", "100"));
        }

        // at an angle this large the double only sees noise; the exact value still knows where it stands
        [Fact]
        public void KnowsTheExactValueWhereTheDoubleCannotTell()
        {
            Assert.Equal(0.5, Value("fn:sin", "36000000000000000000030"));
            Assert.Equal(1, Value("fn:tan", "36000000000000000000045"));
            Assert.Equal(EvaluationError.Domain, Error("fn:tan", "36000000000000000000090"));
        }

        [Fact]
        public void LooksTheArgumentOfAnInverseFunctionUp()
        {
            Assert.Equal(Whole(45), Exact("fn:arcsin", "sqrt", "2", "right", "/", "2"));
            Assert.Equal(Whole(90), Exact("fn:arccos", "0"));
            Assert.Equal(Whole(60), Exact("fn:arcsec", "2"));
            Assert.Equal(Whole(30), Exact("fn:arccsc", "2"));
            Assert.Equal(Whole(135), Exact("fn:arccot", "-", "1"));
            Assert.Equal(Whole(15), Exact("fn:arctan", "2", "-", "sqrt", "3"));

            Assert.Equal(ExactValue.Multiply(ExactValue.Pi, Fraction(1, 4)), Exact(AngleMode.Radians, "fn:arctan", "1"));
            Assert.Equal(Fraction(100, 3), Exact(AngleMode.Gradians, "fn:arcsin", "0.5"));

            Assert.Null(Exact("fn:arcsin", "0.3"));
        }

        [Fact]
        public void KeepsBothValuesOfAPairExact()
        {
            EvaluationResult polar = new MathEvaluator().Evaluate(Keys.Press("fn:pol", "1", "right", "1").RootTokens);
            Assert.Equal(Root(2), polar.FirstValue.Exact);
            Assert.Equal(Whole(45), polar.SecondValue.Exact);

            EvaluationResult left = new MathEvaluator().Evaluate(Keys.Press("fn:pol", "-", "1", "right", "0").RootTokens);
            Assert.Equal(Whole(180), left.SecondValue.Exact);

            EvaluationResult rectangular = new MathEvaluator().Evaluate(Keys.Press("fn:rec", "2", "right", "30").RootTokens);
            Assert.Equal(Root(3), rectangular.FirstValue.Exact);
            Assert.Equal(Whole(1), rectangular.SecondValue.Exact);
        }

        [Fact]
        public void CarriesTheExactValueOfAnsAndOfASeed()
        {
            var evaluator = new MathEvaluator { LastAnswer = new MathValue(Math.Sqrt(2), Root(2)) };
            EvaluationResult withAns = evaluator.Evaluate(Keys.Press("ans", "*", "sqrt", "2").RootTokens);
            Assert.Equal(Whole(2), withAns.FirstValue.Exact);

            var manager = new MathInputManager();
            manager.SeedWithValue("1.41421356237", new MathValue(Math.Sqrt(2), Root(2)));
            manager.AddOperator("*");
            manager.StartRoot(customIndex: false);
            manager.AddNumber("2");

            Assert.Equal(Whole(2), new MathEvaluator().Evaluate(manager.RootTokens).FirstValue.Exact);
        }


        // === sexagesimal ===

        private static MathValue Angle(params string[] keys)
        {
            EvaluationResult result = new MathEvaluator().Evaluate(Keys.Press(keys).RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result.FirstValue;
        }

        // the minutes and the seconds belong to the degrees in front of them rather than being multiplied
        // with them
        [Fact]
        public void ReadsDegreesMinutesAndSeconds()
        {
            Assert.Equal(2.5, Value("2", "dms", "30", "dms"));
            Assert.Equal(2.51, Value("2", "dms", "30", "dms", "36", "dms"));
            Assert.Equal(2.01, Value("2", "dms", "0", "dms", "36", "dms"));
            Assert.Equal(0.65, Value("0", "dms", "39", "dms"));
            Assert.Equal(-2.5, Value("-", "2", "dms", "30", "dms"));

            Assert.Equal(ExactValue.FromRational(new Rational(251, 100)), Angle("2", "dms", "30", "dms", "36", "dms").Exact);
        }

        // a marker stands behind a bracket as well, and minutes left without their degrees are a sixtieth
        [Fact]
        public void ReadsAMarkerBehindABracketAndOnItsOwn()
        {
            Assert.Equal(2.5, Value("(", "1", "+", "1", ")", "dms", "30", "dms"));

            var minutes = new List<MathToken>
            {
                new MathToken(TokenType.Number, "3"), new MathToken(TokenType.Number, "0"), new PostfixToken("minutes")
            };

            Assert.Equal(0.5, new MathEvaluator().Evaluate(minutes).Value);
        }

        // the operations the Casio manual names keep an angle one: plus and minus between two of them,
        // times and divided by a plain number, and a sign
        [Fact]
        public void KeepsAnAngleThroughTheOperationsThatKeepItOne()
        {
            Assert.True(Angle("2", "dms", "+", "1", "dms", "30", "dms").IsSexagesimal);
            Assert.True(Angle("2", "dms", "30", "dms", "-", "1", "dms").IsSexagesimal);
            Assert.True(Angle("2", "dms", "30", "dms", "*", "2").IsSexagesimal);
            Assert.True(Angle("2", "*", "2", "dms", "30", "dms").IsSexagesimal);
            Assert.True(Angle("2", "dms", "30", "dms", "/", "2").IsSexagesimal);
            Assert.True(Angle("(", "2", "dms", ")", "2").IsSexagesimal);
            Assert.True(Angle("-", "2", "dms", "30", "dms").IsSexagesimal);
        }

        [Fact]
        public void DropsTheAngleEverywhereElse()
        {
            MathValue squared = Angle("2", "dms", "30", "dms", "*", "2", "dms", "30", "dms");
            Assert.False(squared.IsSexagesimal);
            Assert.Equal(ExactValue.FromRational(new Rational(25, 4)), squared.Exact);

            Assert.False(Angle("2", "dms", "30", "dms", "+", "1").IsSexagesimal);
            Assert.False(Angle("1", "/", "2", "dms").IsSexagesimal);
            Assert.False(Angle("2", "dms", "30", "dms", "divr", "2").IsSexagesimal);
            Assert.False(Angle("2", "dms", "pow", "2").IsSexagesimal);
            Assert.False(Angle("fn:sin", "30", "dms").IsSexagesimal);
            Assert.False(Angle("2", "dms", "30", "dms", "%").IsSexagesimal);
        }

        // the history line reads an angle as the one operand it is, and only a real product behind a
        // division gets its brackets
        [Fact]
        public void ReadsAnAngleAsOneOperandBehindADivision()
        {
            Assert.Equal("1/2degrees30minutes", Read("1", "/", "2", "dms", "30", "dms"));
            Assert.Equal("1/(2degrees3)", Read("1", "/", "2", "dms", "3"));

            Assert.Equal(0.4, Value("1", "/", "2", "dms", "30", "dms"));
        }

        // an angle carried on from a result is its full value behind the rounded seconds, until the group
        // is edited
        [Fact]
        public void CarriesTheFullValueOfASeededAngle()
        {
            ExactValue seventh = ExactValue.FromRational(new Rational(1, 7));
            MathValue value = new MathValue(0, seventh);

            var manager = new MathInputManager();
            manager.SeedWithTokens(ResultFormatter.SexagesimalTokens(value.Value, asInput: true)!, value);

            MathValue carried = new MathEvaluator().Evaluate(manager.RootTokens).FirstValue;
            Assert.Equal(seventh, carried.Exact);
            Assert.True(carried.IsSexagesimal);

            // a digit typed into the seconds
            manager.SetCursorPosition("@4");
            manager.AddNumber("5");

            Assert.NotEqual(seventh, new MathEvaluator().Evaluate(manager.RootTokens).FirstValue.Exact);
        }


        // === calculus ===

        // measured on the Casio: the sum of 1/x from 1 to 3 is exactly 11/6, and the product of x from 1
        // to 5 is 120
        [Fact]
        public void AddsUpAndMultipliesOutEveryWholeNumberBetweenTheBounds()
        {
            Assert.Equal(11.0 / 6.0, Value("sum", "1", "right", "3", "right", "1", "/", "x"), 15);
            Assert.Equal(ExactValue.FromRational(new Rational(11, 6)), Exact("sum", "1", "right", "3", "right", "1", "/", "x"));

            Assert.Equal(120, Value("prod", "1", "right", "5", "right", "x"));
            Assert.Equal(12, Value("sum", "1", "right", "3", "right", "2", "x"));
            Assert.Equal(2, Value("sum", "2", "right", "2", "right", "x"));
        }

        [Fact]
        public void ReadsTheBodyOfASumAsOneOperand()
        {
            Assert.Equal(15, Value("sum", "1", "right", "3", "right", "x", "pow", "2", "right", "right", "+", "1"));
            Assert.Equal(28, Value("2", "sum", "1", "right", "3", "right", "x", "pow", "2"));
        }

        // the Argument ERROR is the one the Casio names for bounds of Σ and Π that are not whole or the
        // wrong way round
        [Theory]
        [InlineData("sum", "3", "right", "1", "right", "x")]
        [InlineData("sum", "1.5", "right", "3", "right", "x")]
        [InlineData("prod", "1", "right", "2.5", "right", "x")]
        public void RefusesBoundsThatAreNotWholeOrNotInOrder(params string[] keys)
        {
            Assert.Equal(EvaluationError.Argument, Error(keys));
        }

        [Fact]
        public void GivesUpOnMoreTermsThanItMayWorkThrough()
        {
            Assert.Equal(EvaluationError.TimeOut, Error("sum", "1", "right", "100001", "right", "x"));
            Assert.Equal(100000, Value("sum", "1", "right", "100000", "right", "1"));
        }

        // x on its own is an empty variable, the way it is on a Casio before anything is stored in it
        [Fact]
        public void ReadsXAsZeroOutsideACalculusStructure()
        {
            Assert.Equal(1, Value("x", "+", "1"));
            Assert.Equal(0, Value("2", "x"));
            Assert.Equal(5, Value("sum", "x", "right", "x", "pow", "2", "right", "right", "5"));
        }

        [Theory]
        [InlineData("sum", "1", "right", "2", "right", "sum", "1", "right", "2", "right", "x")]
        [InlineData("sum", "integral", "0", "right", "1", "right", "x", "right", "right", "2", "right", "x")]
        [InlineData("integral", "0", "right", "1", "right", "deriv", "x", "right", "x")]
        [InlineData("deriv", "prod", "1", "right", "2", "right", "x", "right", "right", "1")]
        public void RefusesACalculusStructureInsideAnother(params string[] keys)
        {
            Assert.Equal(EvaluationError.Syntax, Error(keys));
        }

        [Theory]
        [InlineData("sum", "1", "right", "3")]
        [InlineData("integral", "0", "right", "1")]
        [InlineData("deriv", "right", "1")]
        [InlineData("deriv", "x")]
        public void RefusesAnEmptySlot(params string[] keys)
        {
            Assert.Equal(EvaluationError.Syntax, Error(keys));
        }

        [Fact]
        public void IntegratesAPolynomialToTheLastDigit()
        {
            Assert.Equal(1.0 / 3.0, Value("integral", "0", "right", "1", "right", "x", "pow", "2"), 15);
            Assert.Equal(-0.5, Value("integral", "1", "right", "0", "right", "x"), 15);
            Assert.Equal(0, Value("integral", "2", "right", "2", "right", "x"));
        }

        [Fact]
        public void IntegratesInTheAngleUnitSelected()
        {
            Assert.Equal(2, Value(AngleMode.Radians, "integral", "0", "right", "pi", "right", "fn:sin", "x"), 13);
            Assert.Equal(180 / Math.PI * 2, Value("integral", "0", "right", "180", "right", "fn:sin", "x"), 9);
        }

        // what cancels itself out is 0 rather than the rounding left over from the cancellation
        [Fact]
        public void LeavesNoNoiseWhereTheBodyCancelsItselfOut()
        {
            Assert.Equal(0, Value(AngleMode.Radians, "integral", "0", "right", "2", "pi", "right", "fn:sin", "x"));
            Assert.Equal(0, Value("integral", "-", "1", "right", "1", "right", "x", "pow", "3"));
        }

        [Fact]
        public void IntegratesUpToAnEndWhereTheBodyHasNoValue()
        {
            Assert.Equal(2, Value("integral", "0", "right", "1", "right", "1", "/", "sqrt", "x"), 9);
            Assert.Equal(-1, Value("integral", "0", "right", "1", "right", "fn:ln", "x"), 9);
        }

        [Fact]
        public void StopsAtAPointInsideTheRangeWhereTheBodyHasNoValue()
        {
            Assert.Equal(EvaluationError.DivideByZero, Error("integral", "-", "1", "right", "1", "right", "1", "/", "x"));
        }

        [Fact]
        public void GivesUpOnAnIntegralThatDoesNotSettle()
        {
            Assert.Equal(EvaluationError.TimeOut, Error("integral", "0", "right", "1", "right", "1", "/", "x"));
        }

        // the integral and the derivative are double only, like a logarithm
        [Fact]
        public void CarriesNoExactValueOutOfTheIntegralOrTheDerivative()
        {
            Assert.Null(Exact("integral", "0", "right", "1", "right", "x"));
            Assert.Null(Exact("deriv", "x", "pow", "2", "right", "right", "3"));
        }

        [Fact]
        public void DifferentiatesAtAPoint()
        {
            Assert.Equal(6, Value("deriv", "x", "pow", "2", "right", "right", "3"), 9);
            Assert.Equal(Math.E, Value("deriv", "powe", "x", "right", "right", "1"), 9);
            Assert.Equal(1 / 3.0, Value("deriv", "fn:ln", "x", "right", "right", "3"), 9);
            Assert.Equal(-1 / 9.0, Value("deriv", "1", "/", "x", "right", "3"), 9);
            Assert.Equal(3, Value("deriv", "x", "pow", "3", "right", "right", "1000000") / 1e12, 9);
        }

        [Fact]
        public void DifferentiatesInTheAngleUnitSelected()
        {
            Assert.Equal(Math.Cos(Math.PI / 6) * Math.PI / 180, Value("deriv", "fn:sin", "x", "right", "right", "30"), 12);
            Assert.Equal(Math.Cos(1000000), Value(AngleMode.Radians, "deriv", "fn:sin", "x", "right", "right", "1000000"), 6);
        }

        [Fact]
        public void LeavesNoNoiseWhereTheSlopeIsZero()
        {
            Assert.Equal(0, Value("deriv", "x", "pow", "3", "right", "-", "3", "x", "right", "1"));
            Assert.Equal(0, Value(AngleMode.Radians, "deriv", "fn:sin", "x", "right", "right", "pi", "/", "2"));
            Assert.Equal(0, Value("deriv", "x", "pow", "3", "right", "right", "0"));
        }

        // a step that reaches past the edge of the domain gives way to a smaller one
        [Fact]
        public void DifferentiatesCloseToTheEdgeOfTheDomain()
        {
            Assert.Equal(1 / (2 * Math.Sqrt(0.05)), Value("deriv", "sqrt", "x", "right", "right", "0.05"), 8);
        }

        [Fact]
        public void HasNoDerivativeWhereTheFunctionHasNoValue()
        {
            Assert.Equal(EvaluationError.DivideByZero, Error("deriv", "1", "/", "x", "right", "0"));
            Assert.Equal(EvaluationError.Domain, Error("deriv", "sqrt", "x", "right", "right", "0"));
        }

        [Fact]
        public void GivesUpOnASlopeThatDoesNotSettle()
        {
            Assert.Equal(EvaluationError.TimeOut, Error("deriv", "root", "3", "right", "x", "right", "right", "0"));
        }
    }
}
