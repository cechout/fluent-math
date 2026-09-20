using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Xunit;

namespace Calculator_WinUI.Tests
{
    public class MathInputManagerTests
    {
        private static double Value(MathInputManager manager)
        {
            EvaluationResult result = new MathEvaluator().Evaluate(manager.RootTokens);

            Assert.True(result.IsSuccess, "expected a value but got " + result.Error);
            return result.Value;
        }


        // === plain input ===

        [Fact]
        public void KeepsOneTokenPerDigit()
        {
            Assert.Equal(3, Keys.Press("125").RootTokens.Count);
        }

        [Fact]
        public void RefusesASecondDecimalPointInTheSameNumber()
        {
            Assert.Equal(3, Keys.Press("1.2.").RootTokens.Count);
        }

        [Fact]
        public void RefusesALeadingOperatorThatIsNotASign()
        {
            Assert.Empty(Keys.Press("+").RootTokens);
            Assert.Single(Keys.Press("-").RootTokens);
        }

        [Fact]
        public void ReplacesAnOperatorTypedTwice()
        {
            MathInputManager manager = Keys.Press("6", "+", "*", "3");

            Assert.Equal(18, Value(manager));
        }

        [Fact]
        public void RefusesAPostfixKeyWithNoOperand()
        {
            Assert.Empty(Keys.Press("!").RootTokens);
            Assert.Empty(Keys.Press("inv").RootTokens);
        }

        [Fact]
        public void RefusesTheExponentKeyWithoutANumberInFrontOfIt()
        {
            Assert.Empty(Keys.Press("exp").RootTokens);
            Assert.Single(Keys.Press("pi", "exp").RootTokens);
        }


        // === backspace ===

        [Fact]
        public void DissolvesAStructureWithOnlyOneSlotInUse()
        {
            MathInputManager manager = Keys.Press("5", "pow", "7", "back", "back");

            Assert.Single(manager.RootTokens);
            Assert.Equal(5, Value(manager));
        }

        [Fact]
        public void FallsBackIntoThePreviousSlotWhenBothAreInUse()
        {
            // the cursor sits at the start of the denominator, so Backspace steps into the numerator
            // rather than deleting the fraction; the 9 then lands in front of the 1
            MathInputManager manager = Keys.Press("1", "frac", "2", "left", "back", "9");

            Assert.Equal(19.0 / 2.0, Value(manager));
        }

        [Fact]
        public void DeletesAPlainTokenFromTheRight()
        {
            Assert.Equal(12, Value(Keys.Press("123", "back")));
        }

        [Fact]
        public void DoesNothingAtTheStartOfTheRootScope()
        {
            Assert.Empty(Keys.Press("back", "back").RootTokens);
        }


        // === navigation ===

        [Fact]
        public void StepsIntoAStructureFromTheLeft()
        {
            // four Lefts walk out of the fraction entirely, then Right walks back into the numerator
            // instead of stepping over the fraction, so the 9 lands in front of the 1
            MathInputManager manager = Keys.Press("1", "frac", "2", "left", "left", "left", "left", "right", "9");

            Assert.Equal(91.0 / 2.0, Value(manager));
        }

        [Fact]
        public void CrossesTheTwoSlotsThatSitAboveEachOther()
        {
            MathInputManager manager = Keys.Press("frac", "3", "down", "4");

            Assert.Equal(0.75, Value(manager));
        }

        [Fact]
        public void ReachesTheIndexOfAPlainSquareRoot()
        {
            // Left out of the radicand lands in the empty index, which is the only way to fill it
            MathInputManager manager = Keys.Press("sqrt", "8", "left", "left", "3");

            Assert.Equal(2, Value(manager), 10);
        }


        // === click addresses ===

        [Fact]
        public void WritesAnAddressOntoEveryTokenWhenAsked()
        {
            string latex = Keys.Press("1", "frac", "2").GetLatexString(withCursor: true, withAddresses: true);

            Assert.Contains("p=", latex);
            Assert.DoesNotContain("p=", Keys.Press("1", "frac", "2").GetLatexString(withCursor: false));
        }

        [Fact]
        public void PlacesTheCursorFromAnAddress()
        {
            MathInputManager manager = Keys.Press("1", "frac", "2");

            Assert.True(manager.SetCursorPosition("0.0@0"));
            manager.AddNumber("9");

            Assert.Equal(91.0 / 2.0, Value(manager));
        }

        [Fact]
        public void LeavesTheCursorAloneOnAnUnusableAddress()
        {
            MathInputManager manager = Keys.Press("1", "frac", "2");

            Assert.False(manager.SetCursorPosition("nonsense"));
            Assert.False(manager.SetCursorPosition("5.0@0"));
            Assert.False(manager.SetCursorPosition("0.7@0"));
            Assert.False(manager.SetCursorPosition(""));

            manager.AddNumber("9");
            Assert.Equal(1.0 / 29.0, Value(manager));
        }


        // === continuing from a result ===

        [Fact]
        public void SeedsTheNextCalculationWithTheDigitsOfTheResult()
        {
            MathInputManager manager = Keys.Press("1", "+", "2");
            manager.SeedWithValue("42");

            // one token per digit, so the seeded result stays editable the same way a typed one is
            Assert.Equal(2, manager.RootTokens.Count);
            Assert.Equal(42, Value(manager));
        }

        [Fact]
        public void SeedsALeadingMinusAsASign()
        {
            MathInputManager manager = new MathInputManager();
            manager.SeedWithValue("-7");

            Assert.Equal(-7, Value(manager));
        }

        [Fact]
        public void SeedsAShownFractionAsAFraction()
        {
            MathInputManager manager = new MathInputManager();
            manager.SeedWithFraction(5, 4);

            Assert.Single(manager.RootTokens);
            Assert.IsType<FractionToken>(manager.RootTokens[0]);
            Assert.Equal(1.25, Value(manager));
        }

        [Fact]
        public void KeepsTypingAfterASeededResult()
        {
            MathInputManager manager = new MathInputManager();
            manager.SeedWithValue("2");
            manager.AddOperator("+");
            manager.AddNumber("3");

            Assert.Equal(5, Value(manager));
        }

        [Fact]
        public void ClearsEverythingIncludingTheScopeStack()
        {
            MathInputManager manager = Keys.Press("1", "frac", "2");
            manager.Clear();
            manager.AddNumber("7");

            Assert.Single(manager.RootTokens);
            Assert.Equal(7, Value(manager));
        }
    }
}
