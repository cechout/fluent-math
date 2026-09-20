using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calculator_WinUI.Engines;
using Calculator_WinUI.ViewModels;
using Xunit;

namespace Calculator_WinUI.Tests
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

        private static StandardViewModel WithAShownResult()
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "1", "+", "1", "=");

            Assert.Equal("2", viewModel.InputAndResultText);
            return viewModel;
        }


        // === the markup and the ViewModel agree ===

        [Fact]
        public void EveryKeyInTheMarkupIsInTheVocabulary()
        {
            foreach (string command in CommandsInMarkup())
            {
                Assert.Contains(command, Vocabulary.Commands);
            }
        }

        [Fact]
        public void EveryCommandInTheVocabularyHasAButton()
        {
            List<string> inMarkup = CommandsInMarkup();

            foreach (string command in Vocabulary.Commands)
            {
                if (Vocabulary.Parked.Contains(command)) continue;

                Assert.True(inMarkup.Contains(command), command + " has no button in StandardPage.xaml");
            }
        }

        private static List<string> CommandsInMarkup()
        {
            string markup = File.ReadAllText(Vocabulary.PageMarkupPath());
            var found = new List<string>();

            foreach (Match match in Regex.Matches(markup, "CommandParameter=\"(cmd_[a-z_0-9]+)\""))
            {
                found.Add(match.Groups[1].Value);
            }

            Assert.NotEmpty(found);
            return found;
        }


        // === no key is dead ===

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeyThatIsNotAModeOrAnArrowChangesTheFormula(string key)
        {
            if (Vocabulary.ModeOnly.Contains(key)) return;
            if (Vocabulary.Navigation.Contains(key)) return;

            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "5");

            string before = viewModel.InputAndResultText;
            StandardViewModelTests.Press(viewModel, key);

            Assert.NotEqual(before, viewModel.InputAndResultText);
        }


        // === no key blanks a shown result ===

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void NoKeyLeavesAShownResultAsABareZero(string key)
        {
            StandardViewModel viewModel = WithAShownResult();
            StandardViewModelTests.Press(viewModel, key);

            Assert.NotEqual(BlankDisplay, viewModel.InputAndResultText);
        }

        [Theory]
        [MemberData(nameof(EveryKey))]
        public void EveryKeyThatContinuesKeepsTheResultOnScreen(string key)
        {
            if (!Vocabulary.ContinuesFromResult.Contains(key) && !Vocabulary.Operators.Contains(key)) return;

            StandardViewModel viewModel = WithAShownResult();
            StandardViewModelTests.Press(viewModel, key);

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
            var viewModel = new StandardViewModel();

            StandardViewModelTests.Press(viewModel, before);
            StandardViewModelTests.Press(viewModel, key);

            // the display always holds something; an empty string is what used to crash the JS side
            Assert.False(string.IsNullOrEmpty(viewModel.InputAndResultText));
        }
    }
}
