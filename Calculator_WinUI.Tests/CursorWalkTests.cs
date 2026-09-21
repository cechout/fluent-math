using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // walking the cursor through a formula with the arrow keys, and where it is drawn while doing it
    //
    // one press of an arrow key has to move the caret somewhere the eye can follow. Two cursor positions
    // that are drawn on the same pixel therefore cost a press that looks like nothing happened, and
    // crossing that place takes two presses in both directions; that is a bug the engine alone cannot
    // see, because the positions are perfectly distinct in the tree
    //
    // this file is the categorical check: it walks every position of one formula per structured token
    // and holds each step against the one before it
    public class CursorWalkTests
    {
        private sealed class FakeMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                return new TextMetrics(text.Length * fontSizePx, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        // where the caret is drawn for the cursor the manager is holding
        //
        // the layout is rebuilt for every step on purpose: a slot that is only drawn while the caret
        // stands in it changes the formula around it, and the point has to be read out of the picture
        // the user is actually looking at
        private static (double X, double Baseline) CaretPoint(MathInputManager manager)
        {
            MathLayoutEngine engine = new MathLayoutEngine(new FakeMeasurer(),
                new MathLayoutStyle { FontSizePx = FontSize },
                new CaretTarget(manager.ActiveTokens, manager.ActiveCursorIndex));

            RowBox row = engine.BuildRow(manager.RootTokens);
            row.Place(0, row.Ascent);

            CaretPlacement caret = engine.Caret.Value;

            return (caret.Box.X + caret.Offset, caret.Line.Baseline);
        }

        // the cursor as the engine sees it: which list it is in, by identity, and where in it
        private static (IReadOnlyList<MathToken> Tokens, int Index) Position(MathInputManager manager)
        {
            return (manager.ActiveTokens, manager.ActiveCursorIndex);
        }

        private static bool Same((IReadOnlyList<MathToken> Tokens, int Index) first,
            (IReadOnlyList<MathToken> Tokens, int Index) second)
        {
            return ReferenceEquals(first.Tokens, second.Tokens) && first.Index == second.Index;
        }

        private static string Describe((IReadOnlyList<MathToken> Tokens, int Index) position)
        {
            return "[" + string.Join(" ", position.Tokens) + "] at " + position.Index;
        }


        // === one press, one step ===

        [Theory]
        [InlineData("2", "pow", "3")]                  // a power, whose base begins where the token does
        [InlineData("3", "*", "10", "pow", "5")]       // the same inside a formula, which is where it was found
        [InlineData("1", "frac", "2", "down", "3")]
        [InlineData("sqrt", "9")]
        [InlineData("root", "3", "right", "8")]
        [InlineData("logb", "2", "right", "8")]
        [InlineData("log", "8")]
        [InlineData("fn:sin", "9")]
        [InlineData("fn:abs", "9")]
        [InlineData("2", "powe")]
        [InlineData("5", "!")]
        [InlineData("(", "1", "+", "2", ")")]
        [InlineData("1", "frac", "2", "pow", "3")]     // a power nested in a numerator
        public void EveryPressOfAnArrowKeyMovesTheCaretSomewhereTheEyeCanFollow(params string[] keys)
        {
            MathInputManager manager = Keys.Press(keys);

            // to the far left first, so the walk covers the whole formula
            for (int step = 0; step < 40; step++) manager.Move(NavDirection.Left);

            (double X, double Baseline) previous = CaretPoint(manager);
            var previousPosition = Position(manager);

            for (int step = 0; step < 40; step++)
            {
                manager.Move(NavDirection.Right);

                var position = Position(manager);
                if (Same(position, previousPosition)) break; // the end, where Right does nothing at all

                (double X, double Baseline) point = CaretPoint(manager);

                Assert.False(point == previous,
                    "stepping from " + Describe(previousPosition) + " to " + Describe(position)
                    + " left the caret on the same pixel");

                previous = point;
                previousPosition = position;
            }
        }


        // === the press count, as it was reported ===

        // typed as 3, times, 10, x^n, 5, which leaves 3x10^5 with the cursor in the exponent
        //
        // walking left out of it reaches the gap between the 3 and the times sign in five presses, and
        // from there two presses have to land between the 1 and the 0; the position in front of the power
        // used to make it three, and every one of them is drawn on the same pixel as the next
        [Fact]
        public void CrossingIntoTheBaseOfAPowerCostsOnePressOfAnArrowKey()
        {
            MathInputManager manager = Keys.Press("3", "*", "10", "pow", "5");
            PowerToken power = (PowerToken)manager.RootTokens[2];

            for (int step = 0; step < 5; step++) manager.Move(NavDirection.Left);

            Assert.Same(manager.RootTokens, manager.ActiveTokens);
            Assert.Equal(1, manager.ActiveCursorIndex);

            manager.Move(NavDirection.Right); // over the times sign, and straight into the base behind it
            manager.Move(NavDirection.Right); // past the one

            Assert.Same(power.BaseTokens, manager.ActiveTokens);
            Assert.Equal(1, manager.ActiveCursorIndex);
        }

        // where the cursor comes to rest decides where the next key goes, and the two places it could
        // rest on here are one pixel: a digit typed against the left edge of the ten has to join the ten,
        // because nothing on screen says it could land beside it instead
        [Fact]
        public void ADigitTypedAgainstTheTenJoinsItRatherThanStandingBesideIt()
        {
            MathInputManager manager = Keys.Press("3", "*", "10", "pow", "5");
            PowerToken power = (PowerToken)manager.RootTokens[2];

            for (int step = 0; step < 5; step++) manager.Move(NavDirection.Left);

            manager.Move(NavDirection.Right); // over the times sign
            manager.AddNumber("7");

            Assert.Equal("710", string.Concat(power.BaseTokens.Select(token => token.Value)));
            Assert.Equal(3, manager.RootTokens.Count); // the 3, the times sign and the power
        }

        [Fact]
        public void TheSameHoldsWalkingBackOutOfThatBase()
        {
            MathInputManager manager = Keys.Press("3", "*", "10", "pow", "5");
            PowerToken power = (PowerToken)manager.RootTokens[2];

            Assert.True(manager.SetCursorPosition("2.0@1")); // between the one and the zero
            Assert.Same(power.BaseTokens, manager.ActiveTokens);

            manager.Move(NavDirection.Left); // in front of the one, which is in front of the power
            manager.Move(NavDirection.Left); // over the times sign

            Assert.Same(manager.RootTokens, manager.ActiveTokens);
            Assert.Equal(1, manager.ActiveCursorIndex);
        }
    }
}
