using FluentMath.Engines;
using FluentMath.Models;
using FluentMath.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluentMath.Tests
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

        // a one half the width of any other digit, the way a proportional face sets it
        private sealed class ProportionalMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                double width = 0;
                foreach (char character in text) width += character == '1' ? fontSizePx * 0.5 : fontSizePx;

                return new TextMetrics(width, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        private static RowBox Laid(IReadOnlyList<MathToken> tokens)
        {
            return Laid(tokens, new FakeMeasurer());
        }

        private static RowBox Laid(IReadOnlyList<MathToken> tokens, ITextMeasurer measurer)
        {
            MathLayoutEngine engine = new MathLayoutEngine(
                measurer, new MathLayoutStyle { FontSizePx = FontSize });

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
        [InlineData("(", "1", "+", "2", ")")]
        [InlineData("(", "1", "frac", "2", "down", "3", ")")]
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

        [Fact]
        public void APointBetweenTwoDigitsTakesTheGapThatWasMeasuredRatherThanAnEqualShare()
        {
            // 1, 1, 9 at half, half and one: the gaps are at 0, 5, 10 and 20, where cutting the run into
            // three equal shares would put them at 0, 6.7, 13.3 and 20
            MathInputManager manager = Keys.Press("119");
            RowBox row = Laid(manager.RootTokens, new ProportionalMeasurer());

            string address = MathHitTest.NearestAddress(row, 8.5, row.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Equal(2, manager.ActiveCursorIndex);
        }


        // === a click lands in the line it was aimed at ===

        // a slot and the row around it cover the same piece of screen, and the row is the wider of the
        // two, so scoring their positions against each other hands most of a base, a numerator or a
        // parameter to the position in front of the whole token: the line is picked first for that reason
        [Fact]
        public void APointOnTheBaseOfAPowerLandsInTheBaseRatherThanInFrontOfTheWholePower()
        {
            MathInputManager manager = Keys.Press("2", "pow", "3");
            PowerToken power = (PowerToken)manager.RootTokens.Single();

            RowBox row = Laid(manager.RootTokens);
            MathBox baseBox = ((RowBox)row.Children.Single()).Children[0];

            // the left half of the base, which is nearer to the position in front of the power than to
            // either end of the base itself
            string address = MathHitTest.NearestAddress(row, baseBox.X + baseBox.Width * 0.3, baseBox.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Same(power.BaseTokens, manager.ActiveTokens);
        }

        [Fact]
        public void APointOnADigitInANumeratorLandsInTheNumerator()
        {
            MathInputManager manager = Keys.Press("frac", "12", "down", "3");
            FractionToken fraction = (FractionToken)manager.RootTokens.Single();

            RowBox row = Laid(manager.RootTokens);
            MathBox numerator = ((FractionBox)row.Children.Single()).Numerator;

            string address = MathHitTest.NearestAddress(row, numerator.X + 1, numerator.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Same(fraction.NumeratorTokens, manager.ActiveTokens);
        }

        [Fact]
        public void APointOnTheNameOfALogarithmLandsBesideItRatherThanInsideIt()
        {
            // the name and the brackets are drawn by the token and belong to no slot, so a click on them
            // is a click on the token as a whole
            MathInputManager manager = Keys.Press("logb", "2", "right", "8");

            RowBox row = Laid(manager.RootTokens);
            MathBox name = ((RowBox)row.Children.Single()).Children[0];

            string address = MathHitTest.NearestAddress(row, name.X + name.Width / 2, name.Baseline);

            Assert.True(manager.SetCursorPosition(address));
            Assert.Same(manager.RootTokens, manager.ActiveTokens);
        }

        [Fact]
        public void APointInTheAirAboveTheFormulaAnswersAtTheEdgeItIsNearest()
        {
            // the display is taller and wider than the formula in it, and clicking in that air is how a
            // formula that is only a few characters long is aimed at at all
            MathInputManager manager = Keys.Press("12", "+", "34");
            RowBox row = Laid(manager.RootTokens);

            Assert.True(manager.SetCursorPosition(MathHitTest.NearestAddress(row, row.Width, row.Top - 400)));
            Assert.Equal(5, manager.ActiveCursorIndex);

            Assert.True(manager.SetCursorPosition(MathHitTest.NearestAddress(row, 0, row.Bottom + 400)));
            Assert.Equal(0, manager.ActiveCursorIndex);
        }


        // === the caret and a click agree ===

        // the caret the layout reports for the cursor the manager is holding, and the row it was
        // drawn in
        private static (RowBox Row, CaretPlacement Caret) CaretOf(MathInputManager manager)
        {
            MathLayoutEngine engine = new MathLayoutEngine(new FakeMeasurer(),
                new MathLayoutStyle { FontSizePx = FontSize },
                new CaretTarget(manager.ActiveTokens, manager.ActiveCursorIndex));

            RowBox row = engine.BuildRow(manager.RootTokens);
            row.Place(0, row.Ascent);

            return (row, engine.Caret.Value);
        }

        // where the caret is drawn for the cursor the manager is holding, and the row it was drawn in
        private static (RowBox Row, double X, double Baseline) CaretPoint(MathInputManager manager)
        {
            (RowBox row, CaretPlacement caret) = CaretOf(manager);

            return (row, caret.Box.X + caret.Offset, caret.Line.Baseline);
        }

        // the one property that ties the three pieces together: click the point the caret is drawn at and
        // it has to stay on that point, for every position the cursor can walk to
        //
        // it is what a user does without thinking about it, and it catches the stops and the caret
        // drifting apart in a way no single assertion about either of them does
        //
        // the assertion is about the point rather than about the position, because two positions can be
        // drawn in one place: the start of the base of a power and the position in front of the whole
        // power are the same pixel, and a click there is free to answer with either of them
        [Theory]
        [InlineData("123", "+", "456")]
        [InlineData("1", "frac", "2", "down", "3")]
        [InlineData("2", "pow", "3")]
        [InlineData("sqrt", "9")]
        [InlineData("root", "3", "right", "8")]
        [InlineData("logb", "2", "right", "8")]
        [InlineData("fn:sin", "9")]
        [InlineData("1", "exp", "5")]
        [InlineData("frac", "down", "2")]
        [InlineData("(", "1", "+", "2", ")")]
        [InlineData("(", "1", "frac", "2", "down", "3", ")")]
        public void AClickOnTheCaretLeavesItExactlyWhereItStands(params string[] keys)
        {
            // every position the cursor reaches walking left out of the formula it just typed
            for (int steps = 0; steps < 12; steps++)
            {
                List<string> run = new List<string>(keys);
                for (int step = 0; step < steps; step++) run.Add("left");

                MathInputManager manager = Keys.Press(run.ToArray());
                (RowBox row, double x, double baseline) = CaretPoint(manager);

                string address = MathHitTest.NearestAddress(row, x, baseline);
                Assert.True(manager.SetCursorPosition(address), "the input manager refused " + address);

                (RowBox _, double clickedX, double clickedBaseline) = CaretPoint(manager);

                Assert.Equal(x, clickedX, 6);
                Assert.Equal(baseline, clickedBaseline, 6);
            }
        }


        // === a preview and the caret it previews ===

        // hovering the display draws the caret a click would leave behind, and it is drawn from
        // NearestCaret while the real one is drawn from what the engine reports after the click
        //
        // MathPanel puts both through one piece of geometry, so the whole promise rests on these two
        // agreeing: the moment they do not, the preview stands somewhere the click does not
        //
        // every sample point over the formula is tried rather than a chosen few, which is what covers
        // an exponent and an empty slot; a caret in a script is drawn smaller, and that size rides on
        // the row rather than on the position
        [Theory]
        [InlineData("1", "+", "2")]
        [InlineData("1", "frac", "2", "down", "3")]
        [InlineData("2", "pow", "3")]
        [InlineData("sqrt", "9")]
        [InlineData("root", "3", "right", "8")]
        [InlineData("fn:sin", "9")]
        [InlineData("frac", "down", "2")]
        [InlineData("(", "1", "+", "2", ")")]
        [InlineData("(", "1", "frac", "2", "down", "3", ")")]
        public void APreviewStandsExactlyWhereAClickPutsTheCaret(params string[] keys)
        {
            RowBox laid = Laid(Keys.Press(keys).RootTokens);

            double across = FontSize / 2;
            double down = (laid.Bottom - laid.Top) / 3;

            for (double x = laid.X - across; x <= laid.X + laid.Width + across; x += across)
            {
                for (double y = laid.Top; y <= laid.Bottom; y += down)
                {
                    CaretPlacement? previewed = MathHitTest.NearestCaret(laid, x, y);
                    Assert.NotNull(previewed);

                    MathInputManager manager = Keys.Press(keys);
                    string address = MathHitTest.NearestAddress(laid, x, y);
                    Assert.True(manager.SetCursorPosition(address), "the input manager refused " + address);

                    CaretPlacement preview = previewed.Value;
                    CaretPlacement landed = CaretOf(manager).Caret;

                    Assert.Equal(preview.Box.X + preview.Offset, landed.Box.X + landed.Offset, 6);
                    Assert.Equal(preview.Line.Baseline, landed.Line.Baseline, 6);
                    Assert.Equal(preview.FontSize, landed.FontSize, 6);
                }
            }
        }
    }
}
