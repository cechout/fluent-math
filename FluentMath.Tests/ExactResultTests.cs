using FluentMath.Models;
using FluentMath.ViewModels;
using Xunit;

namespace FluentMath.Tests
{
    // the exact results a Casio shows, keyed in the way they were measured on the fx-87DE X and read off
    // the display the way a user reads them
    //
    // every case names what the Casio showed; the few where the app deliberately differs say so
    public class ExactResultTests
    {
        // === helpers ===

        private static string Shown(params string[] keys)
        {
            return Shown(new CalculatorSettings(), keys);
        }

        private static string ShownInRadians(params string[] keys)
        {
            return Shown(new CalculatorSettings { AngleMode = AngleMode.Radians }, keys);
        }

        private static string Shown(CalculatorSettings settings, params string[] keys)
        {
            var viewModel = new StandardViewModel(settings);
            StandardViewModelTests.Press(viewModel, keys);
            StandardViewModelTests.Press(viewModel, "=");

            return viewModel.InputAndResultText;
        }


        // === trigonometry ===

        [Fact]
        public void KnowsTheValuesOfEveryMultipleOfFifteenDegrees()
        {
            Assert.Equal("\\frac{\\sqrt{2}}{2}", Shown("cmd_sin", "45"));
            Assert.Equal("\\frac{\\sqrt{3}}{2}", Shown("cmd_cos", "30"));
            Assert.Equal("\\sqrt{3}", Shown("cmd_tan", "60"));
            Assert.Equal("\\frac{\\sqrt{6}-\\sqrt{2}}{4}", Shown("cmd_sin", "15"));
            Assert.Equal("2-\\sqrt{3}", Shown("cmd_tan", "15"));
        }

        // (√5−1)/4 would fit the forms, and the Casio still shows a decimal
        [Fact]
        public void KnowsNothingInBetween()
        {
            Assert.Equal("0.309016994375", Shown("cmd_sin", "18"));
            Assert.Equal("0.809016994375", Shown("cmd_cos", "36"));
        }

        [Fact]
        public void KnowsTheSameAnglesInRadiansAndGradians()
        {
            Assert.Equal("\\frac{1}{2}", ShownInRadians("cmd_sin", "cmd_pi", "/", "6"));
            Assert.Equal("\\frac{\\sqrt{2}}{2}", Shown(new CalculatorSettings { AngleMode = AngleMode.Gradians }, "cmd_cos", "50"));

            // a whole number of radians is no multiple of π, and has no exact value
            Assert.StartsWith("0.841470984808", ShownInRadians("cmd_sin", "1"));
        }

        [Fact]
        public void LooksTheArgumentOfAnInverseFunctionUp()
        {
            Assert.Equal("30", Shown("cmd_asin", "0.5"));
            Assert.Equal("\\frac{1}{6}\\pi", ShownInRadians("cmd_asin", "0.5"));
            Assert.Equal("\\pi", ShownInRadians("cmd_acos", "-", "1"));

            Assert.Equal("45", Shown("cmd_asin", "cmd_sqrt", "2", "cmd_nav_right", "/", "2"));
            Assert.Equal("15", Shown("cmd_atan", "2", "-", "cmd_sqrt", "3"));
            Assert.Equal("135", Shown("cmd_acot", "-", "1"));
        }


        // === roots ===

        [Fact]
        public void PullsTheSquaresOutOfARoot()
        {
            Assert.Equal("2\\sqrt{2}", Shown("cmd_sqrt", "8"));
            Assert.Equal("\\sqrt{6}", Shown("cmd_sqrt", "2", "cmd_nav_right", "*", "cmd_sqrt", "3"));
            Assert.Equal("\\frac{\\sqrt{2}}{2}", Shown("1", "/", "cmd_sqrt", "2"));
        }

        [Fact]
        public void AddsRootsUpToTwoTerms()
        {
            Assert.Equal("\\sqrt{3}+\\sqrt{2}", Shown("cmd_sqrt", "2", "cmd_nav_right", "+", "cmd_sqrt", "3"));

            Assert.StartsWith("5.38233234744", Shown("cmd_sqrt", "2", "cmd_nav_right", "+", "cmd_sqrt", "3", "cmd_nav_right",
                "+", "cmd_sqrt", "5"));
        }

        [Fact]
        public void SquaresAndDividesASumOfRoots()
        {
            Assert.Equal("5+2\\sqrt{6}", Shown("cmd_paren_open", "cmd_sqrt", "2", "cmd_nav_right", "+", "cmd_sqrt", "3",
                "cmd_nav_right", "cmd_paren_close", "cmd_pow_2"));

            Assert.Equal("\\sqrt{3}-\\sqrt{2}", Shown("1", "/", "cmd_paren_open", "cmd_sqrt", "2", "cmd_nav_right", "+",
                "cmd_sqrt", "3", "cmd_nav_right", "cmd_paren_close"));
        }

        // radicands below 1000 and coefficients below 100
        [Fact]
        public void ShowsAFormOnlyWithinTheRangesOfTheCasio()
        {
            Assert.Equal("\\sqrt{997}", Shown("cmd_sqrt", "997"));
            Assert.StartsWith("31.67", Shown("cmd_sqrt", "1003"));

            Assert.Equal("99\\sqrt{2}", Shown("99", "cmd_sqrt", "2"));
            Assert.StartsWith("141.42", Shown("100", "cmd_sqrt", "2"));
        }

        // the rational term first, then the roots by descending radicand, each with its own sign
        [Fact]
        public void OrdersTheTermsTheWayTheCasioDoes()
        {
            Assert.Equal("-2+\\sqrt{3}", Shown("cmd_sqrt", "3", "cmd_nav_right", "-", "2"));
            Assert.Equal("-\\sqrt{3}+\\sqrt{2}", Shown("cmd_sqrt", "2", "cmd_nav_right", "-", "cmd_sqrt", "3"));
            Assert.Equal("-\\sqrt{3}-\\sqrt{2}", Shown("-", "cmd_sqrt", "2", "cmd_nav_right", "-", "cmd_sqrt", "3"));
        }


        // === π ===

        [Fact]
        public void WritesTheCoefficientOfPiAsAFractionInFrontOfIt()
        {
            Assert.Equal("\\frac{1}{3}\\pi", Shown("cmd_pi", "/", "3"));
            Assert.Equal("\\frac{2}{3}\\pi", Shown("2", "cmd_pi", "/", "3"));
            Assert.Equal("1234\\pi", Shown("1234", "cmd_pi"));
            Assert.Equal("-\\frac{1}{6}\\pi", Shown("-", "cmd_pi", "/", "6"));
        }

        [Fact]
        public void ShowsPiWithAnythingButARationalAsADecimal()
        {
            Assert.StartsWith("9.86960440109", Shown("cmd_pi", "cmd_pow_2"));
            Assert.StartsWith("4.14159265359", Shown("cmd_pi", "+", "1"));
            Assert.StartsWith("4.44288293816", Shown("cmd_pi", "cmd_sqrt", "2"));
        }

        // a deliberate difference: the Casio shows π÷1234 as a decimal for a reason nobody found, here the
        // coefficient follows the budget a fraction has
        [Fact]
        public void KeepsASmallCoefficientOfPiWithinTheFractionBudget()
        {
            Assert.Equal("\\frac{1}{1234}\\pi", Shown("cmd_pi", "/", "1234"));
        }


        // === fractions ===

        // the Casio takes ten characters written as a mixed number, and here twelve: 12345÷67891 and
        // 1234567÷89 are a fraction here and a decimal there
        [Fact]
        public void ShowsAFractionOnlyWithinTwelveCharacters()
        {
            Assert.Equal("\\frac{1}{12345}", Shown("1", "/", "12345"));
            Assert.Equal("\\frac{12345}{67891}", Shown("12345", "/", "67891"));
            Assert.Equal("\\frac{1234567}{89}", Shown("1234567", "/", "89"));

            // 1387154 83/89 is thirteen
            Assert.StartsWith("1387154.93", Shown("123456789", "/", "89"));
        }

        [Fact]
        public void KeepsATypedDecimalExact()
        {
            Assert.Equal("\\frac{3}{10}", Shown("0.1", "+", "0.2"));
            Assert.Equal("1", Shown("1", "/", "3", "*", "3"));
        }


        // === carrying on ===

        // the exactness is carried through the calculation and never guessed from the digits
        [Fact]
        public void CarriesTheExactValueThroughAns()
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "cmd_sqrt", "2", "=", "cmd_ans", "+", "cmd_sqrt", "2", "=");
            Assert.Equal("2\\sqrt{2}", viewModel.InputAndResultText);

            Assert.Equal("2.82842712475", Shown("1.41421356237309", "+", "cmd_sqrt", "2"));
        }

        [Fact]
        public void ContinuesFromAnExactFormAsTheRootsItShows()
        {
            var squared = new StandardViewModel();
            StandardViewModelTests.Press(squared, "cmd_sin", "45", "=", "cmd_pow_2", "=");
            Assert.Equal("\\frac{1}{2}", squared.InputAndResultText);

            var root = new StandardViewModel();
            StandardViewModelTests.Press(root, "cmd_sqrt", "2", "=", "*");
            Assert.IsType<RootToken>(root.InputTokens[0]);

            // a sum of two terms is squared as a whole
            var sum = new StandardViewModel();
            StandardViewModelTests.Press(sum, "cmd_sqrt", "3", "cmd_nav_right", "-", "2", "=", "cmd_pow_2", "=");
            Assert.Equal("7-4\\sqrt{3}", sum.InputAndResultText);

            var pi = new StandardViewModel();
            StandardViewModelTests.Press(pi, "cmd_pi", "/", "3", "=", "*", "3", "=");
            Assert.Equal("\\pi", pi.InputAndResultText);
        }

        // measured on the Casio: 1÷3, S⇔D to the decimal, then ×3 is 1
        [Fact]
        public void CarriesTheExactValueBehindTheShownDecimal()
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "1", "/", "3", "=", "sd", "sd", "*", "3", "=");
            Assert.Equal("1", viewModel.InputAndResultText);

            var root = new StandardViewModel();
            StandardViewModelTests.Press(root, "cmd_sqrt", "2", "=", "sd", "*", "cmd_sqrt", "2", "=");
            Assert.Equal("2", root.InputAndResultText);
        }

        // a recurring decimal cannot be typed, so it carries on as its fraction
        [Fact]
        public void ContinuesFromARecurringDecimalAsItsFraction()
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "1", "/", "3", "=", "sd", "+");

            Assert.IsType<FractionToken>(viewModel.InputTokens[0]);
        }

        // a result exactly zero is one, so dividing by it is the Math ERROR it is on a Casio
        [Fact]
        public void TreatsAnExactZeroAsZero()
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, "1", "/", "cmd_paren_open", "cmd_sqrt", "2", "cmd_nav_right", "*",
                "cmd_sqrt", "2", "cmd_nav_right", "-", "2", "cmd_paren_close", "=");

            Assert.Contains("Math ERROR", viewModel.InputAndResultText);
        }


        // === recurring decimals ===

        [Fact]
        public void DrawsABarOverThePeriod()
        {
            Assert.Equal("2.\\overline{3}", RecurringOf("7", "/", "3"));
            Assert.Equal("0.\\overline{142857}", RecurringOf("1", "/", "7"));
            Assert.Equal("0.\\overline{0588235294117647}", RecurringOf("1", "/", "17"));
            Assert.Equal("0.41\\overline{6}", RecurringOf("5", "/", "12"));
        }

        // a period of 96 digits has no bar, and S⇔D goes straight to the decimal
        [Fact]
        public void LeavesTheBarOutWhereThePeriodIsTooLong()
        {
            Assert.StartsWith("0.0103092783", RecurringOf("1", "/", "97"));
        }

        private static string RecurringOf(params string[] keys)
        {
            var viewModel = new StandardViewModel();
            StandardViewModelTests.Press(viewModel, keys);
            StandardViewModelTests.Press(viewModel, "=", "sd");

            return viewModel.InputAndResultText;
        }


        // === pairs ===

        [Fact]
        public void ShowsAnExactPair()
        {
            Assert.Equal("r=\\sqrt{2}, θ=45", Shown("cmd_pol", "1", "cmd_nav_right", "1"));
            Assert.Equal("r=\\sqrt{2}, θ=\\frac{1}{4}\\pi", ShownInRadians("cmd_pol", "1", "cmd_nav_right", "1"));
            Assert.Equal("r=2, θ=-\\frac{2}{3}\\pi", ShownInRadians("cmd_pol", "-", "1", "cmd_nav_right", "-", "cmd_sqrt", "3"));
        }
    }
}
