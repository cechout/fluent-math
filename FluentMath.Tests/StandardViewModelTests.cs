using FluentMath.Models;
using FluentMath.ViewModels;
using Xunit;

namespace FluentMath.Tests
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

        // every result opens as its decimal, the way the display worked before exact came first; the
        // tests about the S to D cycle and about decimal digits start from there
        private static StandardViewModel DecimalFirst()
        {
            return new StandardViewModel(new CalculatorSettings { ExactFirst = false });
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
        [InlineData("cmd_frac_mixed")]
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
            var viewModel = DecimalFirst();

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
            var viewModel = DecimalFirst();
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

        // === panel keys ===

        // a Casio writes AnsC here; the shown 5 is carried on as n the way a plus would carry it
        [Fact]
        public void ACombinationContinuesFromAShownResult()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "5", "=", "cmd_ncr", "2", "=");

            Assert.Equal("10", viewModel.InputAndResultText);
        }

        [Fact]
        public void APrefixContinuesFromAShownResult()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "5", "=", "cmd_prefix_kilo", "=");

            Assert.Equal("5000", viewModel.InputAndResultText);
        }

        [Fact]
        public void ATwoArgumentFunctionTakesItsSecondArgumentAfterRight()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_gcd", "12", "cmd_nav_right", "18", "=");

            Assert.Equal("6", viewModel.InputAndResultText);
        }

        [Fact]
        public void NamesAnArgumentErrorTheWayACasioDoes()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_ranint", "6", "cmd_nav_right", "1", "=");

            Assert.Equal("Argument ERROR", viewModel.InputErrorText);
        }

        [Fact]
        public void ShowsHowACombinationAfterADivisionWasRead()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "12", "/", "2", "cmd_ncr", "2", "=");

            Assert.Equal("12", viewModel.InputAndResultText);
            Assert.Equal(8, viewModel.CalculationTokens.Count); // 1, 2, the division, and 2C2 in its brackets
        }

        // the history line carries the brackets a Casio writes into the input on =, while the formula an
        // arrow key goes back to edit is still the one that was typed
        [Fact]
        public void ShowsHowAnImplicitProductWasReadWithoutRewritingTheFormula()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "6", "/", "2", "cmd_paren_open", "1", "+", "2", "cmd_paren_close", "=");

            Assert.Equal("1", viewModel.InputAndResultText);
            Assert.Equal(10, viewModel.CalculationTokens.Count);

            Press(viewModel, "cmd_nav_left");

            Assert.Equal(8, viewModel.InputTokens.Count);
        }


        // === S to D ===

        // a deliberate difference to the Casio, where S to D instead of = does nothing: it evaluates and
        // then switches, which saves the =
        [Fact]
        public void EvaluatesFirstWhenPressedWhileAFormulaIsBeingTyped()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "7", "/", "3", "sd");

            Assert.Contains("frac{7}{3}", viewModel.InputAndResultText);
        }

        [Fact]
        public void SwitchesBothValuesOfAPairTogether()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "cmd_rec", "1", "cmd_nav_right", "60", "=");
            Assert.StartsWith("x=0.5, y=0.866025403784", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("x=\\frac{1}{2}, y=\\frac{\\sqrt{3}}{2}", viewModel.InputAndResultText);
        }

        // a logarithm has no exact value and its decimal no fraction, so there is nothing to switch to
        [Fact]
        public void DoesNothingOnAResultWithNoFraction()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_ln", "2", "=");

            string before = viewModel.InputAndResultText;
            Press(viewModel, "sd");

            Assert.Equal(before, viewModel.InputAndResultText);
        }

        // measured on the Casio: 1÷3 goes through 0.3 with a bar on its way to the decimal
        [Fact]
        public void CyclesThroughTheRecurringDecimal()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "1", "/", "3", "=");

            string asDecimal = viewModel.InputAndResultText;

            Press(viewModel, "sd");
            Assert.Contains("frac{1}{3}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("0.\\overline{3}", viewModel.InputAndResultText);
            Assert.IsType<RecurringToken>(viewModel.InputTokens[2]);

            Press(viewModel, "sd");
            Assert.Equal(asDecimal, viewModel.InputAndResultText);
        }

        [Fact]
        public void CyclesStraightBackWhenTheDecimalEnds()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "/", "4", "=");
            Assert.Equal("1.25", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Contains("frac{5}{4}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("1.25", viewModel.InputAndResultText);
        }

        [Fact]
        public void LeavesTheRecurringDecimalOutWhenTheSettingsSaySo()
        {
            var viewModel = new StandardViewModel(new CalculatorSettings { RecurringDecimals = false });
            Press(viewModel, "1", "/", "3", "=", "sd");

            Assert.Equal("0.333333333333", viewModel.InputAndResultText);
        }

        // measured on the Casio: 7÷3 is 7/3, then 2.3 with a bar, then the decimal, and the mixed 2 1/3 is
        // on the shift of S⇔D
        [Fact]
        public void SwapsBetweenImproperAndMixedOnTheShiftOfSToD()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "7", "/", "3", "=");
            Assert.IsType<FractionToken>(viewModel.InputTokens[0]);

            Press(viewModel, "cmd_frac_swap");
            Assert.IsType<MixedFractionToken>(viewModel.InputTokens[0]);

            // the cycle keeps the form that was swapped to
            Press(viewModel, "sd", "sd", "sd");
            Assert.IsType<MixedFractionToken>(viewModel.InputTokens[0]);

            Press(viewModel, "cmd_frac_swap");
            Assert.IsType<FractionToken>(viewModel.InputTokens[0]);

            // from the decimal it shows the fraction in the other form
            Press(viewModel, "sd", "sd", "cmd_frac_swap");
            Assert.IsType<MixedFractionToken>(viewModel.InputTokens[0]);
        }

        // a proper fraction has no mixed form and a root no fraction; neither changes on the shift
        [Fact]
        public void LeavesAResultWithoutAMixedFormAloneOnTheShiftOfSToD()
        {
            var fraction = new StandardViewModel();
            Press(fraction, "1", "/", "4", "=", "cmd_frac_swap");
            Assert.Equal("\\frac{1}{4}", fraction.InputAndResultText);

            var root = new StandardViewModel();
            Press(root, "cmd_sqrt", "2", "=", "sd", "cmd_frac_swap");
            Assert.StartsWith("1.41421356237", root.InputAndResultText);
        }

        [Fact]
        public void StartsEveryResultBackAtTheDecimalForm()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "/", "4", "=", "sd");
            Assert.Contains("frac", viewModel.InputAndResultText);

            Press(viewModel, "AC", "5", "/", "4", "=");
            Assert.Equal("1.25", viewModel.InputAndResultText);
        }

        [Fact]
        public void CarriesTheShownFractionIntoTheNextCalculation()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "/", "4", "=", "sd");

            Press(viewModel, "+");
            Assert.Contains("frac", viewModel.InputAndResultText);

            Press(viewModel, "1", "=");
            Assert.Equal("2.25", viewModel.InputAndResultText);
        }

        // a deliberate difference to the Casio, which starts over with an empty template here: the shown
        // number becomes the whole part, the same as a typed one would
        [Fact]
        public void ContinuesAMixedFractionFromTheResult()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "=", "cmd_frac_mixed", "1", "cmd_nav_right", "2", "=");

            Assert.Equal("5.5", viewModel.InputAndResultText);
        }

        // the 0 an empty display shows is not lifted into the whole part, since that is the slot the
        // template is typed from
        [Fact]
        public void OpensAnEmptyMixedFractionOnAnEmptyDisplay()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "cmd_frac_mixed", "1", "cmd_nav_right", "1", "cmd_nav_right", "2", "=");

            Assert.Equal("1.5", viewModel.InputAndResultText);
        }

        [Fact]
        public void CarriesAShownMixedNumberOnAsAMixedFraction()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "/", "4", "=", "cmd_frac_swap", "+");

            Assert.IsType<MixedFractionToken>(viewModel.InputTokens[0]);

            Press(viewModel, "1", "=");
            Assert.Equal("2.25", viewModel.InputAndResultText);

            // the sign is part of the whole number, so squaring a negative one comes out positive
            var negative = DecimalFirst();
            Press(negative, "-", "5", "/", "4", "=", "cmd_frac_swap", "cmd_pow_2", "=");
            Assert.Equal("1.5625", negative.InputAndResultText);
        }

        // the address is worked out against the drawn result, so the seeded tree has to be that shape
        [Fact]
        public void PlacesAClickInsideAShownMixedNumber()
        {
            var viewModel = DecimalFirst();
            Press(viewModel, "5", "/", "4", "=", "cmd_frac_swap");

            viewModel.PlaceCursor("0.0@1");
            Press(viewModel, "0", "=");

            Assert.Equal("10.25", viewModel.InputAndResultText);
        }

        // measured on the Casio: 1÷3, then ×3 is 1; the seeded digits carry the value behind them
        [Fact]
        public void CarriesTheFullValueBehindTheShownDigits()
        {
            var seeded = DecimalFirst();
            Press(seeded, "1", "/", "3", "=", "*", "3", "=");
            Assert.Equal("1", seeded.InputAndResultText);

            // until a digit is edited: the last 3 taken off and typed again is a number typed by hand
            var edited = DecimalFirst();
            Press(edited, "1", "/", "3", "=", "+", "back", "back", "3", "*", "3", "=");
            Assert.Equal("0.999999999999", edited.InputAndResultText);

            var carried = DecimalFirst();
            Press(carried, "1", "/", "3", "=", "AC", "cmd_ans", "*", "3", "=");
            Assert.Equal("1", carried.InputAndResultText);
        }


        // === division with remainder, Pol and Rec ===

        // measured on the Casio: 17÷R5 shows the pair, and +1 carries on from the quotient to 4
        [Fact]
        public void ShowsTheRemainderAndCarriesOnFromTheQuotient()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "17", "cmd_div_r", "5", "=");
            Assert.Equal("Q=3, R=2", viewModel.InputAndResultText);

            Press(viewModel, "+", "1", "=");
            Assert.Equal("4", viewModel.InputAndResultText);
        }

        [Fact]
        public void ContinuesADivisionWithRemainderFromAResult()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "17", "=", "cmd_div_r", "5", "=");

            Assert.Equal("Q=3, R=2", viewModel.InputAndResultText);
        }

        [Fact]
        public void ShowsPolarCoordinatesAndCarriesOnFromR()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "cmd_pol", "3", "cmd_nav_right", "4", "=");
            Assert.Equal("r=5, θ=53.1301023542", viewModel.InputAndResultText);

            Press(viewModel, "+", "1", "=");
            Assert.Equal("6", viewModel.InputAndResultText);
        }


        // === FACT ===

        [Fact]
        public void ShowsThePrimeFactorsAndGoesBackOnASecondPress()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1440", "=", "cmd_prime");
            Assert.StartsWith("2^{5}", viewModel.InputAndResultText);

            Press(viewModel, "cmd_prime");
            Assert.Equal("1440", viewModel.InputAndResultText);
        }

        [Fact]
        public void CarriesThePrimeFactorsOnAsTheProductTheyAre()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1440", "=", "cmd_prime", "+");
            Assert.IsType<PowerToken>(viewModel.InputTokens[0]);

            Press(viewModel, "1", "=");
            Assert.Equal("1441", viewModel.InputAndResultText);
        }

        // a deliberate difference to the Casio, where FACT during input does nothing
        [Fact]
        public void EvaluatesFirstWhenFactIsPressedDuringInput()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1440", "cmd_prime");

            Assert.StartsWith("2^{5}", viewModel.InputAndResultText);
        }

        [Fact]
        public void AnswersAResultWithoutPrimeFactorsWithAMathError()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "3", "/", "2", "=", "cmd_prime");
            Assert.Equal("Math ERROR", viewModel.InputErrorText);

            // the formula is still there to be corrected
            Press(viewModel, "cmd_nav_left");
            Assert.Equal(3, viewModel.InputTokens.Count);
        }

        [Fact]
        public void LeavesTheFactorsForTheDecimalOnSToD()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1440", "=", "cmd_prime", "sd");

            Assert.Equal("1440", viewModel.InputAndResultText);
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


        // === the calculator setup ===

        // measured on the Casio: a result opens as its fraction, and S to D shows the decimal
        [Fact]
        public void OpensAResultAsItsFractionWhenExactComesFirst()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "/", "4", "=");
            Assert.Contains("frac{1}{4}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("0.25", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Contains("frac{1}{4}", viewModel.InputAndResultText);

            // a root opens in its exact form as well, and a logarithm, which has none, as the decimal
            Press(viewModel, "AC", "cmd_sqrt", "2", "=");
            Assert.Equal("\\sqrt{2}", viewModel.InputAndResultText);

            Press(viewModel, "AC", "cmd_ln", "2", "=");
            Assert.StartsWith("0.69314718056", viewModel.InputAndResultText);
        }

        [Fact]
        public void OpensTheMixedFormFirstWhenTheSettingsSaySo()
        {
            var viewModel = new StandardViewModel(new CalculatorSettings { MixedFirst = true });
            Press(viewModel, "5", "/", "4", "=");
            Assert.IsType<MixedFractionToken>(viewModel.InputTokens[0]);

            Press(viewModel, "cmd_frac_swap");
            Assert.IsType<FractionToken>(viewModel.InputTokens[0]);

            // a proper fraction has no mixed form and opens as the improper one
            Press(viewModel, "AC", "1", "/", "4", "=");
            Assert.IsType<FractionToken>(viewModel.InputTokens[0]);
        }

        [Fact]
        public void WritesTheDecimalInTheNumberFormatAndCarriesTheFullValueOn()
        {
            var settings = new CalculatorSettings { ExactFirst = false, NumberFormat = new NumberFormat(NumberNotation.Fix, 2) };
            var viewModel = new StandardViewModel(settings);

            Press(viewModel, "1", "/", "3", "=");
            Assert.Equal("0.33", viewModel.InputAndResultText);

            Press(viewModel, "*", "3", "=");
            Assert.Equal("1.00", viewModel.InputAndResultText);

            // a power of ten is carried on as it is written, with the full mantissa behind its digits
            settings.NumberFormat = new NumberFormat(NumberNotation.Sci, 3);
            Press(viewModel, "AC", "1", "/", "3", "=");
            Assert.StartsWith("3.33", viewModel.InputAndResultText);

            Press(viewModel, "*", "3", "=");
            Assert.StartsWith("1.00", viewModel.InputAndResultText);
        }

        // measured on the Casio: in Fix 2, Rnd(1÷3) is 33/100 and three of it 99/100
        [Fact]
        public void RoundsToTheNumberFormatWithRnd()
        {
            var viewModel = new StandardViewModel(new CalculatorSettings { NumberFormat = new NumberFormat(NumberNotation.Fix, 2) });

            Press(viewModel, "cmd_rnd", "1", "/", "3", "=");
            Assert.Contains("frac{33}{100}", viewModel.InputAndResultText);

            Press(viewModel, "*", "3", "=");
            Assert.Contains("frac{99}{100}", viewModel.InputAndResultText);
        }

        [Fact]
        public void KeepsTheAngleUnitForTheNextViewModel()
        {
            var settings = new CalculatorSettings();
            Press(new StandardViewModel(settings), "cmd_angle_cycle");

            Assert.Equal("RAD", new StandardViewModel(settings).AngleModeLabel);
        }

        [Fact]
        public void LabelsTheDecimalKeyWithTheMarkTheDisplayDraws()
        {
            Assert.Equal(",", new StandardViewModel(new CalculatorSettings { DecimalMark = DecimalMark.Comma }).DecimalMarkLabel);
            Assert.Equal(".", new StandardViewModel(new CalculatorSettings { DecimalMark = DecimalMark.Dot }).DecimalMarkLabel);
        }


        // === ENG ===

        // measured on the Casio: 1234, then ENG, ENG and the shift of ENG
        [Fact]
        public void StepsThroughTheEngineeringForms()
        {
            var viewModel = new StandardViewModel();

            Press(viewModel, "1234", "=", "cmd_eng");
            Assert.StartsWith("1.234", viewModel.InputAndResultText);
            Assert.EndsWith("10^{3}", viewModel.InputAndResultText);

            Press(viewModel, "cmd_eng");
            Assert.StartsWith("1234", viewModel.InputAndResultText);
            Assert.EndsWith("10^{0}", viewModel.InputAndResultText);

            Press(viewModel, "cmd_eng_back");
            Assert.StartsWith("1.234", viewModel.InputAndResultText);

            // the shift pressed first goes one power above, 0.123×10³ for 123
            Press(viewModel, "AC", "123", "=", "cmd_eng_back");
            Assert.StartsWith("0.123", viewModel.InputAndResultText);
            Assert.EndsWith("10^{3}", viewModel.InputAndResultText);

            Press(viewModel, "sd");
            Assert.Equal("123", viewModel.InputAndResultText);
        }

        [Fact]
        public void WritesTheEngineeringFormWithAPrefixAndCarriesItOn()
        {
            var viewModel = new StandardViewModel(new CalculatorSettings { UsePrefixes = true });

            Press(viewModel, "1234", "=", "cmd_eng");
            Assert.Equal("1.234\\mathrm{k}", viewModel.InputAndResultText);

            Press(viewModel, "+");
            Assert.IsType<PostfixToken>(viewModel.InputTokens[5]);

            Press(viewModel, "1", "=");
            Assert.Equal("1235", viewModel.InputAndResultText);
        }


        // === sexagesimal ===

        private const string TwoThirty = "2{}^{\\circ}30{}'0{}''";

        private static string ResultOf(params string[] keys)
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, keys);
            Press(viewModel, "=");

            return viewModel.InputAndResultText;
        }

        // measured on the Casio: 2°30′ shows 2°30′0″, and 2.2583 followed by °′″ shows 2°15′29.88″
        [Fact]
        public void ShowsAnAngleInDegreesMinutesAndSeconds()
        {
            Assert.Equal(TwoThirty, ResultOf("2", "cmd_dms", "30", "cmd_dms"));

            var viewModel = new StandardViewModel();
            Press(viewModel, "2.2583", "=", "cmd_dms");
            Assert.Equal("2{}^{\\circ}15{}'29.88{}''", viewModel.InputAndResultText);
        }

        // measured on the Casio: °′″ plus °′″ stays one, and so does one times a plain number or with a
        // minus in front; two of them multiplied are a plain 25/4
        [Fact]
        public void KeepsAnAngleWhereTheCasioDoes()
        {
            Assert.Equal("3{}^{\\circ}0{}'0{}''", ResultOf("1", "cmd_dms", "30", "cmd_dms", "+", "1", "cmd_dms", "30", "cmd_dms"));
            Assert.Equal("5{}^{\\circ}0{}'0{}''", ResultOf("2", "cmd_dms", "30", "cmd_dms", "*", "2"));
            Assert.Equal("-" + TwoThirty, ResultOf("-", "2", "cmd_dms", "30", "cmd_dms"));
            Assert.Equal("\\frac{25}{4}", ResultOf("2", "cmd_dms", "30", "cmd_dms", "*", "2", "cmd_dms", "30", "cmd_dms"));
        }

        // the key switches a result between the angle and the decimal, deg and S⇔D go to the decimal
        [Fact]
        public void SwitchesBetweenTheAngleAndTheDecimal()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "2", "cmd_dms", "30", "cmd_dms", "=", "cmd_dms");
            Assert.Equal("2.5", viewModel.InputAndResultText);

            Press(viewModel, "cmd_dms");
            Assert.Equal(TwoThirty, viewModel.InputAndResultText);

            Press(viewModel, "cmd_degrees");
            Assert.Equal("2.5", viewModel.InputAndResultText);

            Press(viewModel, "cmd_dms", "sd");
            Assert.Equal("2.5", viewModel.InputAndResultText);
        }

        // during input the key types a marker and deg evaluates first; on an empty display the marker
        // stands behind the 0, which is how minutes alone are typed
        [Fact]
        public void TypesAMarkerDuringInputAndEvaluatesOnDeg()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "2", "cmd_dms", "30", "cmd_dms", "cmd_degrees");
            Assert.Equal("2.5", viewModel.InputAndResultText);

            Assert.Equal("0{}^{\\circ}39{}'0{}''", ResultOf("cmd_dms", "39", "cmd_dms"));
        }

        // the angle carries on as real markers with the full value behind the rounded seconds, so a
        // seventh of a degree times seven is one degree again
        [Fact]
        public void CarriesAnAngleOnAtItsFullValue()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1", "/", "7", "=", "cmd_dms");
            Assert.Equal("0{}^{\\circ}8{}'34.29{}''", viewModel.InputAndResultText);

            Press(viewModel, "*");
            Assert.IsType<PostfixToken>(viewModel.InputTokens[1]);

            Press(viewModel, "7", "=");
            Assert.Equal("1{}^{\\circ}0{}'0{}''", viewModel.InputAndResultText);
        }

        // a negative angle goes in brackets for a key that takes the operand on its left, like any
        // negative result
        [Fact]
        public void SquaresANegativeAngleAsAWhole()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "-", "2", "cmd_dms", "30", "cmd_dms", "=", "cmd_pow_2", "=");

            Assert.Equal("\\frac{25}{4}", viewModel.InputAndResultText);
        }

        // a click lands where it was aimed, since the angle is seeded as the tokens it is drawn as
        [Fact]
        public void PlacesTheCaretInAShownAngle()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "2", "cmd_dms", "30", "cmd_dms", "=");
            viewModel.PlaceCursor("@3");

            Assert.Equal(7, viewModel.InputTokens.Count);
            Assert.Equal(3, viewModel.CaretIndex);
        }

        // a pair is shown by its first value, and a value past the largest angle the form writes stays
        // as it is
        [Fact]
        public void ShowsAPairByItsFirstValueAndLeavesAValueTooLargeAlone()
        {
            var pair = new StandardViewModel();
            Press(pair, "cmd_pol", "1", "cmd_nav_right", "1", "=", "cmd_dms");
            Assert.Equal("1{}^{\\circ}24{}'51.17{}''", pair.InputAndResultText);

            var tooLarge = new StandardViewModel();
            Press(tooLarge, "1", "cmd_exp", "8", "=", "cmd_dms");
            Assert.Equal("100000000", tooLarge.InputAndResultText);
        }


        // === a result taken as the operand of the next key ===

        // a Casio squares Ans, so a negative result squares to a positive number; −5² typed by hand is
        // still −25
        [Fact]
        public void SquaresANegativeResultAsAWhole()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "-", "5", "=", "cmd_pow_2", "=");

            Assert.Equal("25", viewModel.InputAndResultText);
        }

        [Fact]
        public void SquaresThePrimeFactorsAsAWhole()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "1440", "=", "cmd_prime", "cmd_pow_2", "=");

            Assert.Equal("2073600", viewModel.InputAndResultText);
        }

        [Fact]
        public void SquaresAPowerOfTenAsAWhole()
        {
            var viewModel = new StandardViewModel();
            Press(viewModel, "3", "cmd_exp", "20", "=", "cmd_pow_2", "=");

            Assert.StartsWith("9", viewModel.InputAndResultText);
            Assert.EndsWith("10^{40}", viewModel.InputAndResultText);
        }

        // a single operand needs no brackets, and an operator key needs none either
        [Fact]
        public void LeavesTheBracketsOutWhereTheyChangeNothing()
        {
            var positive = new StandardViewModel();
            Press(positive, "5", "=", "cmd_pow_2");
            Assert.IsType<PowerToken>(Assert.Single(positive.InputTokens));

            var negative = new StandardViewModel();
            Press(negative, "-", "5", "=", "+");
            Assert.Equal(TokenType.Operator, negative.InputTokens[0].Type);
        }
    }
}
