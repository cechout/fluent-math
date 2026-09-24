using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace FluentMath.Tests
{
    // the command vocabulary as data, so a test can press every key there is instead of the handful
    // somebody thought of
    //
    // the list is maintained here on purpose rather than scraped out of the ViewModel: it is the
    // contract, and CommandVocabularyTests holds both the markup and the ViewModel against it
    internal static class Vocabulary
    {
        public static readonly string[] Commands =
        {
            "cmd_shift",
            "cmd_nav_left", "cmd_nav_right", "cmd_nav_up", "cmd_nav_down",
            "cmd_frac", "cmd_sqrt", "cmd_root_n", "cmd_pow_2", "cmd_pow_n",
            "cmd_ln", "cmd_log", "cmd_log_b", "cmd_e", "cmd_pi", "cmd_ans",
            "cmd_sin", "cmd_cos", "cmd_tan", "cmd_asin", "cmd_acos", "cmd_atan",
            "cmd_sinh", "cmd_cosh", "cmd_tanh", "cmd_asinh", "cmd_acosh", "cmd_atanh",
            "cmd_sec", "cmd_csc", "cmd_cot", "cmd_asec", "cmd_acsc", "cmd_acot",
            "cmd_sech", "cmd_csch", "cmd_coth", "cmd_asech", "cmd_acsch", "cmd_acoth",
            "cmd_trig_inv", "cmd_trig_hyp",
            "cmd_floor", "cmd_ceil", "cmd_rand", "cmd_dms", "cmd_degrees",
            "cmd_paren_open", "cmd_paren_close",
            "cmd_fact", "cmd_inv", "cmd_percent", "cmd_abs",
            "cmd_pow_e", "cmd_exp", "cmd_frac_mixed",
            "cmd_gcd", "cmd_lcm", "cmd_div_r", "cmd_prime", "cmd_int", "cmd_intg",
            "cmd_npr", "cmd_ncr", "cmd_ranint", "cmd_rnd", "cmd_rndfix",
            "cmd_pol", "cmd_rec", "cmd_eng", "cmd_eng_back", "cmd_frac_swap", "cmd_prefix_milli", "cmd_prefix_micro", "cmd_prefix_nano", "cmd_prefix_pico", "cmd_prefix_femto",
            "cmd_prefix_kilo", "cmd_prefix_mega", "cmd_prefix_giga", "cmd_prefix_tera", "cmd_prefix_peta", "cmd_prefix_exa",
            "cmd_integral", "cmd_derivative", "cmd_sum", "cmd_product", "cmd_x",
            "cmd_history", "cmd_memory",
            "cmd_angle_cycle", "cmd_angle_deg", "cmd_angle_rad", "cmd_angle_gra"
        };

        // handled by the ViewModel but with no button anywhere
        //
        // the selector in the caret bar sends cmd_angle_cycle, because a button that shows
        // the current unit can only offer the next one; these three set a unit outright, and the settings
        // page writes the unit into the settings itself rather than sending them
        public static readonly string[] Parked =
        {
            "cmd_angle_deg", "cmd_angle_rad", "cmd_angle_gra"
        };

        // drawn on a key but computing nothing yet; mirrors CalculatorViewModel.NotImplementedKeys and
        // the revisit tag above it
        //
        // the header carries the history and memory keys ahead of what they need; a name leaves this list
        // as it lands
        public static readonly string[] NotImplemented =
        {
            "cmd_history", "cmd_memory"
        };

        // the keys that read an operand to their left, so pressing one on a shown result carries on
        // from it; mirrors CalculatorViewModel.ContinuesFromResult
        public static readonly string[] ContinuesFromResult =
        {
            "cmd_fact", "cmd_inv", "cmd_percent", "cmd_pow_2", "cmd_pow_n", "cmd_frac", "cmd_frac_mixed", "cmd_exp",
            "cmd_prefix_milli", "cmd_prefix_micro", "cmd_prefix_nano", "cmd_prefix_pico", "cmd_prefix_femto",
            "cmd_prefix_kilo", "cmd_prefix_mega", "cmd_prefix_giga", "cmd_prefix_tera", "cmd_prefix_peta", "cmd_prefix_exa"
        };

        // the keys that stand between two operands the way the arithmetic signs do, and carry a shown
        // result on as their left one; mirrors the commands in CalculatorViewModel.IsOperator
        public static readonly string[] OperatorCommands =
        {
            "cmd_npr", "cmd_ncr", "cmd_div_r"
        };

        // the keys that change how a result is shown and leave the formula behind it alone; pressed during
        // input they evaluate first
        //
        // the °′″ key is one on a result only, since during input it types a marker
        public static readonly string[] ViewKeys =
        {
            "cmd_prime", "cmd_eng", "cmd_eng_back", "cmd_frac_swap", "cmd_degrees"
        };

        // the keys that change a mode instead of the formula, and must leave both the input and a shown
        // result exactly where they are
        public static readonly string[] ModeOnly =
        {
            "cmd_shift", "cmd_trig_inv", "cmd_trig_hyp",
            "cmd_angle_cycle", "cmd_angle_deg", "cmd_angle_rad", "cmd_angle_gra"
        };

        public static readonly string[] Navigation =
        {
            "cmd_nav_left", "cmd_nav_right", "cmd_nav_up", "cmd_nav_down"
        };

        public static readonly string[] Operators = { "+", "-", "*", "/" };

        public static readonly string[] Digits =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "."
        };

        // everything a keypad button can send, commands and characters alike
        public static IEnumerable<string> Everything()
        {
            foreach (string command in Commands) yield return command;
            foreach (string op in Operators) yield return op;
            foreach (string digit in Digits) yield return digit;
        }

        // xunit wants each case as its own row
        public static IEnumerable<object[]> EveryKey()
        {
            foreach (string key in Everything()) yield return new object[] { key };
        }


        // === source layout ===

        // the markup is read straight off disk so the test can compare it against the list above
        //
        // CallerFilePath resolves when the test project is compiled, which is what makes this work both
        // here and on a runner without anything having to know a working directory
        public static string PageMarkupPath()
        {
            return Path.Combine(RepositoryRoot(), "FluentMath", "Views", "ScientificPage.xaml");
        }

        private static string RepositoryRoot([CallerFilePath] string testSourcePath = "")
        {
            // this file sits in FluentMath.Tests, one level under the repository root
            string testProject = Path.GetDirectoryName(testSourcePath)!;

            return Path.GetDirectoryName(testProject)!;
        }
    }
}
