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
            Assert.Contains("10^", viewModel.InputAndResultText);
        }
    }
}
