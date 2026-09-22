using Calculator_WinUI.ViewModels;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the keypad path: which key does what to a formula being typed, and what each of them does to a
    // result that is already on screen
    //
    // this is the layer the engine tests cannot reach, because the decision of whether a key continues
    // from the result, edits the old formula or starts over lives here and nowhere else
    public class StandardViewModelTests
    {
        // === helpers ===

        // a multi character argument is split, since every keypad button sends exactly one character and
        // AddNumber would otherwise take "12" as a single token
        internal static void Press(StandardViewModel viewModel, params string[] keys)
        {
            foreach (string key in keys)
            {
                switch (key)
                {
                    case "=":
                        viewModel.CalculateCommand.Execute(null);
                        continue;

                    case "AC":
                        viewModel.ClearCommand.Execute(null);
                        continue;

                    case "back":
                        viewModel.BackspaceCommand.Execute(null);
                        continue;

                    case "sd":
                        viewModel.ToggleAnswerFormCommand.Execute(null);
                        continue;
                }

                if (key.StartsWith("cmd_") || key.Length == 1)
                {
                    viewModel.InputCommand.Execute(key);
                    continue;
                }

                foreach (char character in key)
                {
                    viewModel.InputCommand.Execute(character.ToString());
                }
            }
        }

        private static StandardViewModel AfterOnePlusOne()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "+", "1", "=");

            return viewModel;
        }


        // === continuing from a result ===

        [Fact]
        public void ShowsThePlainResultAfterEquals()
        {
            Assert.Equal("2", AfterOnePlusOne().InputAndResultText);
        }

        [Fact]
        public void ContinuesFromTheResultWhenTheExponentKeyIsPressed()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, "cmd_exp");

            // the 2 has to still be standing with the exponent hanging off it; clearing it first and
            // then refusing the key leaves an empty tree, which renders as a bare 0
            Assert.Contains("2", viewModel.InputAndResultText);
            Assert.Contains("m-pow", viewModel.InputAndResultText);
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("cmd_pow_2")]
        [InlineData("cmd_pow_n")]
        [InlineData("cmd_fact")]
        [InlineData("cmd_inv")]
        [InlineData("cmd_percent")]
        [InlineData("cmd_frac")]
        [InlineData("cmd_exp")]
        public void KeepsTheResultOnScreenForEveryKeyThatReadsAnOperand(string key)
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, key);

            Assert.Contains("2", viewModel.InputAndResultText);
        }

        [Theory]
        [InlineData("7")]
        [InlineData("cmd_pi")]
        [InlineData("cmd_sin")]
        [InlineData("cmd_sqrt")]
        [InlineData("cmd_root_n")]
        [InlineData("cmd_log")]
        [InlineData("cmd_ln")]
        [InlineData("cmd_paren_open")]
        public void StartsOverForEveryKeyThatOpensSomethingNew(string key)
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, key);

            Assert.DoesNotContain("2", viewModel.InputAndResultText);
        }

        [Fact]
        public void GoesBackToEditingTheOldFormulaOnAnArrowKey()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, "cmd_nav_left");

            // the tree still holds 1+1, so the arrow key brings the formula back rather than the result
            Assert.Contains("1", viewModel.InputAndResultText);
            Assert.Contains("+", viewModel.InputAndResultText);
        }

        [Fact]
        public void EditsTheOldFormulaOnBackspace()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, "back", "3", "=");

            Assert.Equal("4", viewModel.InputAndResultText);
        }

        [Fact]
        public void ClearsEverythingOnAllClear()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, "AC");

            Assert.Equal("", viewModel.CalculationText);
            Assert.Contains("0", viewModel.InputAndResultText);
        }

        [Fact]
        public void RepeatsTheSameResultOnASecondEquals()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Press(viewModel, "=");

            Assert.Equal("2", viewModel.InputAndResultText);
        }


        // === modes leave the formula alone ===

        [Fact]
        public void SwapsTheKeyboardLayerWithoutTouchingTheInput()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "+", "1");

            string before = viewModel.InputAndResultText;

            Assert.True(viewModel.IsNormalLayer);
            Assert.False(viewModel.IsShiftLayer);

            Press(viewModel, "cmd_shift");
            Assert.False(viewModel.IsNormalLayer);
            Assert.True(viewModel.IsShiftLayer);
            Assert.Equal(before, viewModel.InputAndResultText);

            Press(viewModel, "cmd_shift");
            Assert.True(viewModel.IsNormalLayer);
            Assert.Equal(before, viewModel.InputAndResultText);
        }

        // the two latches in the trigonometry flyout pick one of four grids, and exactly one of the four
        // has to be up at any time or the panel shows nothing or shows two layers at once
        [Fact]
        public void PicksOneTrigonometryGridForEveryCombinationOfTheTwoLatches()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "+", "1");

            string before = viewModel.InputAndResultText;

            AssertOneTrigGrid(viewModel, plain: true);

            Press(viewModel, "cmd_trig_inv");
            AssertOneTrigGrid(viewModel, inverse: true);
            Assert.True(viewModel.IsTrigInverseLatched);

            Press(viewModel, "cmd_trig_hyp");
            AssertOneTrigGrid(viewModel, inverseHyperbolic: true);

            Press(viewModel, "cmd_trig_inv");
            AssertOneTrigGrid(viewModel, hyperbolic: true);

            // the panel closing is what takes the latches with it, whether a key was pressed or not
            viewModel.ResetTrigLatches();
            AssertOneTrigGrid(viewModel, plain: true);
            Assert.False(viewModel.IsTrigHyperbolicLatched);

            Assert.Equal(before, viewModel.InputAndResultText);
        }

        private static void AssertOneTrigGrid(StandardViewModel viewModel,
                                              bool plain = false,
                                              bool inverse = false,
                                              bool hyperbolic = false,
                                              bool inverseHyperbolic = false)
        {
            Assert.Equal(plain, viewModel.ShowTrigPlain);
            Assert.Equal(inverse, viewModel.ShowTrigInverse);
            Assert.Equal(hyperbolic, viewModel.ShowTrigHyperbolic);
            Assert.Equal(inverseHyperbolic, viewModel.ShowTrigInverseHyperbolic);
        }

        [Fact]
        public void LeavesAShownResultAloneWhenTheAngleUnitChanges()
        {
            StandardViewModel viewModel = AfterOnePlusOne();

            Press(viewModel, "cmd_angle_rad");
            Assert.Equal("RAD", viewModel.AngleModeLabel);
            Assert.Equal("2", viewModel.InputAndResultText);

            Press(viewModel, "cmd_angle_gra");
            Assert.Equal("GRA", viewModel.AngleModeLabel);

            Press(viewModel, "cmd_angle_deg");
            Assert.Equal("DEG", viewModel.AngleModeLabel);
            Assert.Equal("2", viewModel.InputAndResultText);
        }

        // the selector button shows one unit and offers the next, so the cycle is the only way the
        // keypad reaches radians and gradians at all
        [Fact]
        public void CyclesTheAngleUnitInOneDirectionAndComesBack()
        {
            StandardViewModel viewModel = AfterOnePlusOne();
            Assert.Equal("DEG", viewModel.AngleModeLabel);

            Press(viewModel, "cmd_angle_cycle");
            Assert.Equal("RAD", viewModel.AngleModeLabel);

            Press(viewModel, "cmd_angle_cycle");
            Assert.Equal("GRA", viewModel.AngleModeLabel);

            Press(viewModel, "cmd_angle_cycle");
            Assert.Equal("DEG", viewModel.AngleModeLabel);
            Assert.Equal("2", viewModel.InputAndResultText);
        }

        [Fact]
        public void AppliesTheAngleUnitToTheNextEvaluation()
        {
            var viewModel = new StandardViewModel();

            Press(viewModel, "cmd_sin", "3", "0", "=");
            Assert.Equal("0.5", viewModel.InputAndResultText);

            Press(viewModel, "AC", "cmd_angle_gra", "cmd_sin", "1", "0", "0", "=");
            Assert.Equal("1", viewModel.InputAndResultText);
        }


        // === the zero an empty display shows ===

        [Theory]
        [InlineData("cmd_pow_n", "2", "0")]
        [InlineData("cmd_pow_2", "", "0")]
        [InlineData("cmd_frac", "2", "0")]
        [InlineData("cmd_exp", "5", "0")]
        [InlineData("cmd_fact", "", "1")]
        public void TakesTheZeroOnAnEmptyDisplayAsATypedOne(string key, string followUp, string expected)
        {
            // an empty formula is drawn as a 0, so a key that reads an operand has to find it; without
            // that the 0 vanishes and the key opens on an empty box instead
            var viewModel = new StandardViewModel();

            Press(viewModel, key);
            if (followUp.Length > 0) Press(viewModel, followUp);
            Press(viewModel, "=");

            Assert.Equal(expected, viewModel.InputAndResultText);
        }

        [Fact]
        public void TakesTheReciprocalOfTheZeroOnAnEmptyDisplay()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_inv", "=");

            Assert.Contains("Math ERROR", viewModel.InputAndResultText);
        }

        [Fact]
        public void LeavesAnEmptySlotAloneWhereNoZeroWasPromised()
        {
            // inside a structure an empty slot draws a box rather than a 0, so nothing was promised and
            // the power opens on an empty base the way it always did
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_sqrt", "cmd_pow_n", "2", "=");

            Assert.Contains("ERROR", viewModel.InputAndResultText);
        }


        // === the exponent key ===

        // the one way to get a power of ten, and it is a multiplication like any other: the key spells
        // out times, one, zero, power rather than making a token that binds tighter than it looks
        [Fact]
        public void ReadsTheExponentKeyAsAnOrdinaryMultiplication()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "/", "3", "cmd_exp", "5", "=");

            Assert.Equal("33333.3333333", viewModel.InputAndResultText);
        }

        [Fact]
        public void DissolvesTheExponentOnBackspace()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "3", "cmd_exp", "5", "cmd_nav_left", "back", "=");

            Assert.Equal("315", viewModel.InputAndResultText);
        }


        // === the history line ===

        [Fact]
        public void PutsTheEvaluatedFormulaOnTheHistoryLine()
        {
            StandardViewModel viewModel = AfterOnePlusOne();

            Assert.Contains("1", viewModel.CalculationText);
            Assert.EndsWith("=", viewModel.CalculationText);
        }


        // === S to D ===

        [Fact]
        public void DoesNothingWhileAFormulaIsBeingTyped()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "+", "1");

            string before = viewModel.InputAndResultText;
            Press(viewModel, "sd");

            Assert.Equal(before, viewModel.InputAndResultText);
        }

        [Fact]
        public void DoesNothingOnAResultWithNoFraction()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_sqrt", "2", "=");

            string before = viewModel.InputAndResultText;
            Press(viewModel, "sd");

            Assert.Equal(before, viewModel.InputAndResultText);
        }

        [Fact]
        public void CyclesStraightBackWhenThereIsNoMixedForm()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "/", "3", "=");

            string asDecimal = viewModel.InputAndResultText;

            Press(viewModel, "sd");
            Assert.Contains("frac{1}{3}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal(asDecimal, viewModel.InputAndResultText);
        }

        [Fact]
        public void CyclesThroughAllThreeFormsWhenTheyExist()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "5", "/", "4", "=");
            Assert.Equal("1.25", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Contains("frac{5}{4}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.StartsWith("1", viewModel.InputAndResultText);
            Assert.Contains("frac{1}{4}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("1.25", viewModel.InputAndResultText);
        }

        [Fact]
        public void StartsEveryResultBackAtTheDecimalForm()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "5", "/", "4", "=", "sd");
            Assert.Contains("frac", viewModel.InputAndResultText);

            Press(viewModel, "AC", "5", "/", "4", "=");
            Assert.Equal("1.25", viewModel.InputAndResultText);
        }

        [Fact]
        public void CarriesTheShownFractionIntoTheNextCalculation()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "5", "/", "4", "=", "sd");

            Press(viewModel, "+");
            Assert.Contains("frac", viewModel.InputAndResultText);

            Press(viewModel, "1", "=");
            Assert.Equal("2.25", viewModel.InputAndResultText);
        }

        [Fact]
        public void LosesThePrecisionASeededResultCannotHoldButAnsCan()
        {
            // the seeded digits are what the display showed, so a third of a whole comes back short
            var seeded = new StandardViewModel();
            Press(seeded, "1", "/", "3", "=", "*", "3", "=");
            Assert.Equal("0.999999999999", seeded.InputAndResultText);

            // Ans resolves against the untouched double instead; a fresh calculator, since the run
            // above has already moved the last answer on
            var carried = new StandardViewModel();
            Press(carried, "1", "/", "3", "=", "AC", "cmd_ans", "*", "3", "=");
            Assert.Equal("1", carried.InputAndResultText);
        }


        // === clicking into the display ===

        // the zero on an empty formula is drawn rather than typed: there is one place the cursor can
        // stand in it, and a click on the other side of it must not pretend otherwise
        //
        // the next digit is what proves it: a cursor that really had moved in front of the zero would
        // leave a formula with the zero still in it, and the = is only there because the text while a
        // formula is being typed is the whole LaTeX of it rather than the digits
        [Fact]
        public void AClickBesideTheZeroOnAnEmptyDisplayIsNotAPlace()
        {
            StandardViewModel viewModel = new StandardViewModel();

            Assert.False(viewModel.CanPlaceCursor);

            viewModel.PlaceCursor("@0");
            Press(viewModel, "5", "=");

            Assert.Equal("5", viewModel.InputAndResultText);
        }

        // a click on a shown result carries it into the next calculation the way an operator does, and
        // lands the cursor at the place that was clicked
        //
        // the address was worked out against the result that is on screen, and seeding it is what puts
        // those very tokens into the tree, which is what makes the address mean what it looked like
        [Fact]
        public void AClickOnAShownResultCarriesItAndTakesTheCursorWithIt()
        {
            StandardViewModel viewModel = AfterOnePlusOne();

            // in front of the 2 the display is showing
            viewModel.PlaceCursor("@0");
            Press(viewModel, "3", "=");

            Assert.Equal("32", viewModel.InputAndResultText);
        }

        [Fact]
        public void AClickBehindAShownResultCarriesItAndWritesOnTheEnd()
        {
            StandardViewModel viewModel = AfterOnePlusOne();

            viewModel.PlaceCursor("@1");
            Press(viewModel, "3", "=");

            Assert.Equal("23", viewModel.InputAndResultText);
        }
    }
}
