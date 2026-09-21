using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // turning a point in the display back into a place in the tree
    //
    // the addresses the layout writes are checked against the thing that reads them rather than against a
    // string literal: every one of them is fed to MathInputManager.SetCursorPosition, which is what would
    // notice the two drifting apart
    public class MathHitTestTests
    {
        private sealed class FakeMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                return new TextMetrics(text.Length * fontSizePx, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        private static RowBox Laid(IReadOnlyList<MathToken> tokens)
        {
            MathLayoutEngine engine = new MathLayoutEngine(
                new FakeMeasurer(), new MathLayoutStyle { FontSizePx = FontSize });

            RowBox row = engine.BuildRow(tokens);
            row.Place(0, row.Ascent);

            return row;
        }

        private static List<string> EveryAddress(MathBox box)
        {
            List<string> found = new List<string>();
            Walk(box, found);

            return found.Where(address => address != null).Distinct().ToList();
        }

        private static void Walk(MathBox box, List<string> found)
        {
            found.Add(box.CursorAddress);

            switch (box)
            {
                case RowBox row:
                    found.Add(row.EndAddress);
                    foreach (MathBox child in row.Children) Walk(child, found);
                    break;

                case FractionBox fraction:
                    Walk(fraction.Numerator, found);
                    Walk(fraction.Denominator, found);
                    break;

                case RootBox root:
                    if (root.Index != null) Walk(root.Index, found);
                    Walk(root.Radicand, found);
                    break;

                case TextRunBox run when run.TokenAddresses != null:
                    found.AddRange(run.TokenAddresses);
                    break;
            }
        }


        // === the addresses are the ones the engine parses ===

        [Theory]
        [InlineData("1", "+", "2")]
        [InlineData("1", "frac", "2", "down", "3")]
        [InlineData("2", "pow", "3")]
        [InlineData("sqrt", "9")]
        [InlineData("root", "3", "right", "8")]
        [InlineData("logb", "2", "right", "8")]
        [InlineData("fn:sin", "9")]
        [InlineData("1", "exp", "5")]
        public void EveryAddressTheLayoutWritesIsOneTheInputManagerAccepts(params string[] keys)
        {
            MathInputManager manager = Keys.Press(keys);

            foreach (string address in EveryAddress(Laid(manager.RootTokens)))
            {
                Assert.True(Keys.Press(keys).SetCursorPosition(address),
                    "the input manager refused " + address);
            }
        }

        [Fact]
        public void ASlotAddressWalksIntoThatSlotAndNowhereElse()
        {
            MathInputManager manager = Keys.Press("1", "frac", "2", "down", "3");
            FractionToken fraction = (FractionToken)manager.RootTokens.Last();

            FractionBox box = (FractionBox)Laid(manager.RootTokens).Children.Last();
            string numerator = ((RowBox)box.Numerator).EndAddress;

            Assert.True(manager.SetCursorPosition(numerator));
            Assert.Same(fraction.NumeratorTokens, manager.ActiveTokens);
        }


        // === what a point answers with ===

        [Fact]
        public void APointAtTheStartAnswersWithTheFirstPosition()
        {
            MathInputManager manager = Keys.Press("12");
            RowBox row = Laid(manager.RootTokens);

            Assert.True(manager.SetCursorPosition(MathHitTest.NearestAddress(row, -50, row.Baseline)));
            Assert.Equal(0, manager.ActiveCursorIndex);
        }

        [Fact]
        public void APointPastTheEndAnswersWithTheLastPosition()
        {
            MathInputManager manager = Keys.Press("12");
            RowBox row = Laid(manager.RootTokens);

            Assert.True(manager.SetCursorPosition(MathHitTest.NearestAddress(row, 500, row.Baseline)));
            Assert.Equal(2, manager.ActiveCursorIndex);
        }

        [Fact]
        public void APointBetweenTwoDigitsOfOneNumberLandsBetweenThem()
        {
            MathInputManager manager = Keys.Press("123");
            RowBox row = Laid(manager.RootTokens);

            // just past the first digit of a run that is three digits wide
            string address = MathHitTest.NearestAddress(row, FontSize * 1.1, row.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Equal(1, manager.ActiveCursorIndex);
        }

        [Fact]
        public void APointLowUnderAFractionAnswersInTheDenominatorRatherThanTheNumerator()
        {
            MathInputManager manager = Keys.Press("frac", "1", "down", "2");
            RowBox row = Laid(manager.RootTokens);
            FractionToken fraction = (FractionToken)manager.RootTokens.Single();

            FractionBox box = (FractionBox)row.Children.Single();
            string address = MathHitTest.NearestAddress(row, box.X + box.Width / 2, box.Bottom);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Same(fraction.DenominatorTokens, manager.ActiveTokens);
        }

        [Fact]
        public void AnEmptySlotCanBeClickedInto()
        {
            // the numerator is still empty, and a slot that could not be aimed at would be unreachable
            MathInputManager manager = Keys.Press("frac");
            FractionToken fraction = (FractionToken)manager.RootTokens.Single();

            RowBox row = Laid(manager.RootTokens);
            FractionBox box = (FractionBox)row.Children.Single();
            string address = MathHitTest.NearestAddress(row, box.X + box.Width / 2, box.Numerator.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Same(fraction.NumeratorTokens, manager.ActiveTokens);
        }

        [Fact]
        public void AnEmptyFormulaAnswersWithNothingRatherThanThrowing()
        {
            RowBox row = Laid(new List<MathToken>());

            Assert.NotNull(MathHitTest.NearestAddress(row, 10, 10));
        }
    }
}
