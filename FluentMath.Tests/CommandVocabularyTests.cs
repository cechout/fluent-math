using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentMath.Engines;
using FluentMath.ViewModels;
using Xunit;

namespace FluentMath.Tests
{
    // presses every key there is, in every state the calculator can be in
    //
    // the bug this exists for: a key that is missing from ContinuesFromResult clears the shown result,
    // then finds nothing to work on and refuses, and the display is left as a bare 0 with the formula
    // gone; nothing throws and nothing looks wrong from the inside, so only pressing all of them catches
    // the next one
    public class CommandVocabularyTests
    {
        // what the input line renders when the token tree is empty
        private static readonly string BlankDisplay =
            new MathInputManager().GetLatexString(withCursor: true, withAddresses: true);

        public static IEnumerable<object[]> EveryKey() => Vocabulary.EveryKey();

        private static CalculatorViewModel WithAShownResult()
        {
            var viewModel = new CalculatorViewModel();
            CalculatorViewModelTests.Press(viewModel, "1", "+", "1", "=");

            Assert.Equal("2", viewModel.InputAndResultText);
            return viewModel;
        }


        // === the markup and the ViewModel agree ===

        // a typo on either calculator page is a dead key, so both are read; only the scientific pad has to
        // carry every command, the standard one holds a subset of them by design
        [Theory]
        [InlineData("ScientificPage")]
        [InlineData("StandardPage")]
        public void EveryKeyInTheMarkupIsInTheVocabulary(string page)
        {
            foreach (string command in CommandsInMarkup(page))
            {
                Assert.Contains(command, Vocabulary.Commands);
            }
        }

        [Fact]
        public void EveryCommandInTheVocabularyHasAButton()
        {
            List<string> inMarkup = CommandsInMarkup("ScientificPage");

            foreach (string command in Vocabulary.Commands)
            {
                if (Vocabulary.Parked.Contains(command)) continue;

                Assert.True(inMarkup.Contains(command), command + " has no button in ScientificPage.xaml");
            }
        }

        private static List<string> CommandsInMarkup(string page)
        {
            string markup = File.ReadAllText(Vocabulary.PageMarkupPath(page));
            var found = new List<string>();

            foreach (Match match in Regex.Matches(markup, "CommandParameter=\"(cmd_[a-z_0-9]+)\""))
            {
                found.Add(match.Groups[1].Value);
            }

            Assert.NotEmpty(found);
            return found;
        }


        // === no key is dead, except the ones that are declared to be ===

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeyThatIsNotAModeOrAnArrowChangesTheFormula(string key)
        {
            if (Vocabulary.ModeOnly.Contains(key)) return;
            if (Vocabulary.Navigation.Contains(key)) return;
            if (Vocabulary.NotImplemented.Contains(key)) return;

            var viewModel = new CalculatorViewModel();
            CalculatorViewModelTests.Press(viewModel, "5");

            string before = viewModel.InputAndResultText;
            CalculatorViewModelTests.Press(viewModel, key);

            Assert.NotEqual(before, viewModel.InputAndResultText);
        }

        // the skip above is a hole, so this closes it from the other side: a key on the list has to do
        // nothing at all rather than something nobody looked at
        //
        // the second half is the one that matters, since an unguarded key would reach
        // BeginInputAfterResult, clear the tree and leave the display as a bare 0
        [Theory]
        [MemberData(nameof(NotImplementedKey))]
        public void ANotImplementedKeyLeavesEverythingWhereItIs(string key)
        {
            var viewModel = new CalculatorViewModel();
            CalculatorViewModelTests.Press(viewModel, "5");

            string afterInput = viewModel.InputAndResultText;
            CalculatorViewModelTests.Press(viewModel, key);

            Assert.Equal(afterInput, viewModel.InputAndResultText);

            CalculatorViewModel onAResult = WithAShownResult();
            CalculatorViewModelTests.Press(onAResult, key);

            Assert.Equal("2", onAResult.InputAndResultText);
        }

        // a view key only changes the display, so Left afterwards walks back into the formula the result
        // came from, whether the key was pressed on the result or instead of the =
        [Theory]
        [MemberData(nameof(ViewKey))]
        public void AViewKeyLeavesTheFormulaBehindTheResultAlone(string key)
        {
            CalculatorViewModel onAResult = WithAShownResult();
            CalculatorViewModelTests.Press(onAResult, key, "cmd_nav_left");

            Assert.Equal(3, onAResult.InputTokens.Count);

            var duringInput = new CalculatorViewModel();
            CalculatorViewModelTests.Press(duringInput, "1", "+", "1", key);
            Assert.Null(duringInput.CaretTokens);

            CalculatorViewModelTests.Press(duringInput, "cmd_nav_left");
            Assert.Equal(3, duringInput.InputTokens.Count);
        }

        public static IEnumerable<object[]> ViewKey()
        {
            foreach (string key in Vocabulary.ViewKeys) yield return new object[] { key };
        }

        public static IEnumerable<object[]> NotImplementedKey()
        {
            foreach (string key in Vocabulary.NotImplemented) yield return new object[] { key };
        }


        // === no key blanks a shown result ===

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void NoKeyLeavesAShownResultAsABareZero(string key)
        {
            CalculatorViewModel viewModel = WithAShownResult();
            CalculatorViewModelTests.Press(viewModel, key);

            Assert.NotEqual(BlankDisplay, viewModel.InputAndResultText);
        }

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeyThatContinuesKeepsTheResultOnScreen(string key)
        {
            if (!Vocabulary.ContinuesFromResult.Contains(key)
                && !Vocabulary.Operators.Contains(key)
                && !Vocabulary.OperatorCommands.Contains(key)) return;

            CalculatorViewModel viewModel = WithAShownResult();
            CalculatorViewModelTests.Press(viewModel, key);

            Assert.Contains("2", viewModel.InputAndResultText);
        }


        // === nothing throws, whatever the state ===

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeySurvivesEveryState(string key)
        {
            PressOn(key);                                    // empty input
            PressOn(key, "5");                               // a plain number
            PressOn(key, "1", "+", "1", "=");                // a shown result
            PressOn(key, "3", "frac");                       // an empty denominator, a syntax error
            PressOn(key, "3", "frac", "=");                  // an error on screen
            PressOn(key, "cmd_sqrt");                        // inside an empty radicand
            PressOn(key, "2", "cmd_exp", "5");               // inside a scientific exponent
            PressOn(key, "1", "cmd_frac", "2", "cmd_nav_up"); // inside a numerator
        }

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeySurvivesBeingPressedTwice(string key)
        {
            PressOn(key, key);
            PressOn(key, "5", key);
        }

        private static void PressOn(string key, params string[] before)
        {
            var viewModel = new CalculatorViewModel();

            CalculatorViewModelTests.Press(viewModel, before);
            CalculatorViewModelTests.Press(viewModel, key);

            // the display always holds something; an empty string is what used to crash the JS side
            Assert.False(string.IsNullOrEmpty(viewModel.InputAndResultText));
        }
    }
}
