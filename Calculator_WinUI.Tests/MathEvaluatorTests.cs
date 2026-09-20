using System;
using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Xunit;

namespace Calculator_WinUI.Tests
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
        public void BindsAScientificExponentTighterThanADivision()
        {
            Assert.Equal(1.0 / 300000, Value("1", "/", "3", "exp", "5"), 15);
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

        [Fact]
        public void HasNoTangentPoleToFindInRadians()
        {
            // no double lands exactly on pi over two, so there is nothing to catch and the huge value
            // it produces is the honest answer
            EvaluationResult result = new MathEvaluator(AngleMode.Radians)
                .Evaluate(Keys.Press("fn:tan", "pi", "/", "2").RootTokens);

            Assert.True(result.IsSuccess);
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
            Assert.Equal(EvaluationError.Syntax, Error("3", "exp", "5", "left", "left", "back"));
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
