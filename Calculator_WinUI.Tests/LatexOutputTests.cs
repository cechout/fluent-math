using Calculator_WinUI.Engines;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the shape of the LaTeX the tokens hand to KaTeX
    //
    // these are not cosmetic: every one of them is a command KaTeX either knows or does not, and a
    // malformed one renders as nothing at all rather than as an error, which is invisible until someone
    // presses the key in the running app
    public class LatexOutputTests
    {
        private static string Latex(params string[] keys)
        {
            return Keys.Press(keys).GetLatexString(withCursor: false);
        }

        [Fact]
        public void WritesAFactorialAsATrailingBang()
        {
            Assert.Contains("{5}\\mathord{!}", Latex("5", "!"));
        }

        [Fact]
        public void WritesAReciprocalAsARaisedMinusOne()
        {
            Assert.Contains("{{}^{-1}}", Latex("4", "inv"));
        }

        [Fact]
        public void EscapesThePercentSign()
        {
            Assert.Contains("\\mathord{\\%}", Latex("50", "%"));
        }

        [Fact]
        public void WritesAnAbsoluteValueAsAPairOfBars()
        {
            string latex = Latex("fn:abs", "7");

            Assert.Contains("\\left|", latex);
            Assert.Contains("\\right|", latex);
        }

        [Fact]
        public void WritesAnInverseHyperbolicAsARaisedMinusOne()
        {
            // KaTeX has no arsinh command, so the Casio spelling is the one that renders at all
            Assert.Contains("\\sinh^{-1}(", Latex("fn:arsinh", "1"));
            Assert.DoesNotContain("\\arsinh", Latex("fn:arsinh", "1"));
        }

        [Fact]
        public void WritesAScientificExponentAsAPlainMultiplicationAndPower()
        {
            // the EXP key spells out times, one, zero, power, so there is no command of its own left
            string latex = Latex("3", "exp", "5");

            Assert.Contains("\\htmlClass{m-op}{\\cdot}", latex);
            Assert.Contains("m-pow", latex);
        }

        [Fact]
        public void WritesAnsAsAWord()
        {
            Assert.Contains("\\text{Ans}", Latex("ans"));
        }

        [Fact]
        public void BracesThePowerBaseSoTheExponentCannotSlipOff()
        {
            // an unbraced base hands the exponent only the last atom in it, which is what used to drop
            // the exponent onto the height of a caret standing at the end of the base
            Assert.Contains("{\\mathord{1}\\mathord{0}}^{", Latex("10", "pow", "3"));
        }

        [Fact]
        public void LeavesOutALogarithmBaseThatWasNeverTyped()
        {
            Assert.Contains("\\log(", Latex("log", "100"));
            Assert.Contains("\\log_{", Latex("logb", "2", "right", "8"));
        }

        [Fact]
        public void ShowsAnEmptySlotAsABox()
        {
            Assert.Contains("\\square", Keys.Press("3", "exp").GetLatexString(withCursor: true));
        }

        [Fact]
        public void DrawsTheCursorOnlyWhenAskedFor()
        {
            MathInputManager manager = Keys.Press("1", "+", "2");

            Assert.Contains("htmlClass{cursor}", manager.GetLatexString(withCursor: true));
            Assert.DoesNotContain("htmlClass{cursor}", manager.GetLatexString(withCursor: false));
        }

        [Fact]
        public void ShowsAnEmptyFormulaAsZero()
        {
            Assert.Equal("0", new MathInputManager().GetLatexString(withCursor: false));
        }
    }
}
