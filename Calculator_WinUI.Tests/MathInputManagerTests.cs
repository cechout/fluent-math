using System;
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

        private static EvaluationError ErrorOf(MathInputManager manager)
        {
            EvaluationResult result = new MathEvaluator().Evaluate(manager.RootTokens);

            Assert.False(result.IsSuccess, "expected a failure but got " + result.Value);
            return result.Error;
        }


        // === plain input ===

        [Fact]
        public void KeepsOneTokenPerDigit()
        {
            Assert.Equal(3, Keys.Press("125").RootTokens.Count);
        }

        // the input is a sandbox: every one of these used to be refused at the keypress, which threw the
        // key away with nothing on screen saying why
        // now it all goes in as typed and the formula is judged once, on =

        [Fact]
        public void TakesASecondDecimalPointInTheSameNumber()
        {
            Assert.Equal(4, Keys.Press("1.2.").RootTokens.Count);
            Assert.Equal(EvaluationError.Syntax, ErrorOf(Keys.Press("1.2.3")));
        }

        [Fact]
        public void TakesAnOperatorWithNothingOnItsLeft()
        {
            Assert.Single(Keys.Press("+").RootTokens);
            Assert.Single(Keys.Press("*").RootTokens);
            Assert.Equal(EvaluationError.Syntax, ErrorOf(Keys.Press("*", "5")));

            // a leading minus still reads as a sign, and now it needs no special case to get in
            Assert.Equal(-5, Value(Keys.Press("-", "5")));
        }

        [Fact]
        public void TakesAsManyOperatorsInARowAsAreTyped()
        {
            // the newer one used to overwrite the older, so a mistyped plus could never be seen again
            Assert.Equal(4, Keys.Press("6", "+", "*", "3").RootTokens.Count);
            Assert.Equal(EvaluationError.Syntax, ErrorOf(Keys.Press("6", "+", "*", "3")));

            // a run of signs is still a run of signs and evaluates
            Assert.Equal(5, Value(Keys.Press("-", "-", "5")));
        }

        [Fact]
        public void TakesAPostfixKeyWithNoOperand()
        {
            Assert.Single(Keys.Press("!").RootTokens);
            Assert.Equal(EvaluationError.Syntax, ErrorOf(Keys.Press("!")));
        }

        [Fact]
        public void TakesTheExponentKeyAnywhere()
        {
            // times and the power token; the one and the zero moved into the base
            Assert.Equal(2, Keys.Press("exp").RootTokens.Count);
            Assert.Equal(3, Keys.Press("pi", "exp").RootTokens.Count);
            Assert.Equal(Math.PI * 1000, Value(Keys.Press("pi", "exp", "3")), 9);
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
        public void PutsTheTimesTenBackWhenAScientificTokenDissolves()
        {
            // the times sign and the ten are drawn by the token rather than typed, so dropping them
            // would turn 3x10^5 into 35, a different number with nothing on screen saying so
            MathInputManager manager = Keys.Press("3", "exp", "5", "left", "back");

            Assert.Equal(3.0 * 105.0, Value(manager));

            // the caret stays where the structure boundary was, between the ten and the exponent
            manager.AddNumber("9");
            Assert.Equal(3.0 * 1095.0, Value(manager));
        }

        // every caret position in 3x10^5 and what Backspace does from it, as one table
        //
        // the trap the old table warned about is gone with the token: the ten is typed rather than drawn,
        // so every place the caret appears to stand is a place it really stands, and Backspace does there
        // what the picture says it will
        [Fact]
        public void BackspacesOutOfATimesTenPowerFromEveryCaretPosition()
        {
            // row 0, caret right of the 5, inside the exponent: the 5 goes and the empty power stays
            MathInputManager insideAtEnd = Keys.Press("3", "exp", "5", "back");
            Assert.Equal(EvaluationError.Syntax, ErrorOf(insideAtEnd));

            // row 1, caret left of the 5: the power dissolves into 3*105
            Assert.Equal(315, Value(Keys.Press("3", "exp", "5", "left", "back")));

            // row 2, caret right of the ten: a digit comes off it, leaving 3*1^5
            Assert.Equal(3, Value(Keys.Press("3", "exp", "5", "left", "left", "back")));

            // row 3, caret between the one and the zero: the one goes, leaving 3*0^5
            Assert.Equal(0, Value(Keys.Press("3", "exp", "5", "left", "left", "left", "back")));

            // row 4, caret left of the ten: the power dissolves the same way row 1 does
            Assert.Equal(315, Value(Keys.Press("3", "exp", "5", "left", "left", "left", "left", "back")));

            // row 5, caret behind the whole power: it goes in one press and the times is left standing
            Assert.Equal(EvaluationError.Syntax, ErrorOf(Keys.Press("3", "exp", "5", "right", "back")));
        }

        [Fact]
        public void LeavesTheTimesTenBehindWhenAnUntouchedExponentIsDeleted()
        {
            // the EXP key is four keystrokes taken off you, so undoing it takes four too; it used to be
            // one token that vanished in one press, and that was the special case that cost a cursor
            // position between the ten and the exponent
            MathInputManager manager = Keys.Press("3", "exp", "back");

            Assert.Equal(4, manager.RootTokens.Count);
            Assert.Equal(30, Value(manager));
        }

        [Fact]
        public void RunsTwoNumbersTogetherWhenAFunctionDissolves()
        {
            // confirmed against the FX-991: deleting the sin out of 2sin(30) leaves 230, because the 2
            // and the 30 end up side by side
            MathInputManager manager = Keys.Press("2", "fn:sin", "30", "left", "left", "back");

            Assert.Equal(230, Value(manager));
        }

        [Fact]
        public void LeavesTheCaretWhereTheDissolvedSlotBegan()
        {
            // out of the argument of 2sin(30) the caret belongs in front of the 30, where the argument
            // began, not behind everything that was salvaged
            MathInputManager function = Keys.Press("2", "fn:sin", "30", "left", "left", "back");
            function.AddNumber("9");
            Assert.Equal(2930, Value(function));

            // out of an exponent it belongs behind the base, since the base is what stood in front of
            // the caret before the structure went away
            MathInputManager power = Keys.Press("5", "pow", "7", "back", "back");
            power.AddNumber("9");
            Assert.Equal(59, Value(power));
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


        // === backspace out of the remaining slots ===

        [Fact]
        public void DissolvesTheStructureFromEitherSlot()
        {
            // out of the denominator: the two halves come to stand side by side and the caret stays
            // where the denominator began
            MathInputManager denominator = Keys.Press("1", "frac", "2", "left", "back");
            Assert.Equal(12, Value(denominator));
            denominator.AddNumber("9");
            Assert.Equal(192, Value(denominator));

            // out of the numerator: the caret stays in front of everything
            MathInputManager numerator = Keys.Press("1", "frac", "2", "left", "left", "left", "back");
            Assert.Equal(12, Value(numerator));
            numerator.AddNumber("9");
            Assert.Equal(912, Value(numerator));

            // out of an exponent: the base is what stood in front of the caret, so it stays there
            MathInputManager power = Keys.Press("3", "pow", "5", "left", "back");
            Assert.Equal(35, Value(power));
        }

        [Fact]
        public void DissolvesARootAndALogarithmTheSameWay()
        {
            Assert.Equal(38, Value(Keys.Press("sqrt", "8", "up", "3", "down", "back")));
            Assert.Equal(28, Value(Keys.Press("logb", "2", "right", "8", "left", "back")));
        }


        // === operand capture ===

        [Fact]
        public void TakesAPostfixAlongIntoTheNumerator()
        {
            Assert.Equal(60, Value(Keys.Press("5", "!", "frac", "2")));
        }

        [Fact]
        public void TakesAScientificExponentAlongIntoTheNumerator()
        {
            Assert.Equal(150000, Value(Keys.Press("3", "exp", "5", "right", "frac", "2")));
        }

        [Fact]
        public void TakesAWholeNestedStructureAsThePowerBase()
        {
            Assert.Equal(0.25, Value(Keys.Press("1", "frac", "2", "right", "pow", "2")), 12);
        }


        // === navigation across the slots that sit above each other ===

        [Fact]
        public void CrossesUpIntoARootIndex()
        {
            Assert.Equal(2, Value(Keys.Press("sqrt", "8", "up", "3")), 10);
        }

        [Fact]
        public void CrossesDownIntoALogarithmBase()
        {
            Assert.Equal(3, Value(Keys.Press("log", "8", "down", "2")), 10);
        }

        [Fact]
        public void CrossesDownIntoAPowerBase()
        {
            // arriving from above lands at the start of the base, so the 3 goes in front of the 5
            Assert.Equal(1225, Value(Keys.Press("5", "pow", "2", "down", "3")));
        }

        [Fact]
        public void WalksBetweenTheTenAndItsExponentWithUpAndDown()
        {
            // the exponent is an ordinary power now, so Down reaches the ten it stands on, landing at
            // its start; Up from the exponent does nothing, since that is already the upper slot
            MathInputManager manager = Keys.Press("3", "exp", "5", "up", "down", "9");

            Assert.Equal(3 * Math.Pow(910, 5), Value(manager), 0);
        }


        // === click addresses ===

        [Fact]
        public void PlacesTheCursorInEverySlotOfAStructure()
        {
            MathInputManager fraction = Keys.Press("1", "frac", "2");
            Assert.True(fraction.SetCursorPosition("0.1@0"));
            fraction.AddNumber("9");
            Assert.Equal(1.0 / 92.0, Value(fraction), 12);

            MathInputManager root = Keys.Press("sqrt", "8");
            Assert.True(root.SetCursorPosition("0.0@0"));
            root.AddNumber("3");
            Assert.Equal(2, Value(root), 10);

            // three tokens now: the 3, the times, and the power whose base holds the ten
            MathInputManager scientific = Keys.Press("3", "exp", "5");
            Assert.True(scientific.SetCursorPosition("2.1@0"));
            scientific.AddNumber("1");
            Assert.Equal(3e15, Value(scientific));
        }

        [Fact]
        public void RefusesAnAddressThatTheTreeNoLongerHas()
        {
            MathInputManager manager = Keys.Press("1", "frac", "2");
            manager.Clear();

            // a click on a render that has already been replaced must not move anything
            Assert.False(manager.SetCursorPosition("0.0@0"));
        }


        // === guards ===

        [Fact]
        public void TakesAnExponentInTheMiddleOfANumber()
        {
            // the digits in front of the caret are the mantissa and the rest multiplies on afterwards
            Assert.Equal(12e5 * 3, Value(Keys.Press("123", "left", "exp", "5")));
        }

        [Fact]
        public void AllowsADecimalPointOnceInEverySlot()
        {
            Assert.Equal(0.6, Value(Keys.Press("1.5", "frac", "2.5")), 12);
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
