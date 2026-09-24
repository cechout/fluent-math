using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FluentMath.Engines;
using FluentMath.Models;
using FluentMath.ViewModels;
using Xunit;

namespace FluentMath.Tests
{
    // random key sequences against the invariants that have to hold for any input at all
    //
    // mathematical input is far too varied to enumerate, so this backs the written tests up: it will
    // not say what a formula should come out as, only that nothing throws, that the display always has
    // something in it, and that every address the renderer hands the page can be handed back
    //
    // the seed is fixed, so a failure is reproducible and the sequence is printed with it
    public class KeySequenceFuzzTests
    {
        private const int Seed = 20260920;
        private const int Sequences = 2000;
        private const int LongestSequence = 20;

        [Fact]
        public void TheEngineSurvivesAnySequenceOfKeys()
        {
            var random = new Random(Seed);
            int addressesChecked = 0;

            for (int run = 0; run < Sequences; run++)
            {
                string[] keys = NextSequence(random, Keys.All);
                string trail = string.Join(" ", keys);

                MathInputManager manager = Keys.Press(keys);

                string latex = manager.GetLatexString(withCursor: true, withAddresses: true);
                Assert.False(string.IsNullOrEmpty(latex), trail);
                Assert.Equal(CountOf(latex, '{'), CountOf(latex, '}'));

                // the history line asks for the same tree without a cursor and without addresses
                Assert.False(string.IsNullOrEmpty(manager.GetLatexString(withCursor: false)), trail);

                EvaluationResult result = new MathEvaluator().Evaluate(manager.RootTokens);
                if (result.IsSuccess)
                {
                    Assert.False(double.IsNaN(result.Value), trail);
                    Assert.False(double.IsInfinity(result.Value), trail);
                    Assert.False(string.IsNullOrEmpty(ResultFormatter.ToLatex(result.Value)), trail);
                }
                else
                {
                    Assert.NotEqual(EvaluationError.None, result.Error);
                }

                addressesChecked += AssertEveryAddressResolves(manager, latex, trail);
            }

            // without this the address check above could quietly be walking an empty match set and the
            // whole invariant would pass on nothing
            Assert.True(addressesChecked > 1000, "only " + addressesChecked + " addresses were checked");
        }

        // a click can only be turned back into a cursor position if the address the renderer wrote is
        // one SetCursorPosition accepts, so every one of them is handed straight back
        private static int AssertEveryAddressResolves(MathInputManager manager, string latex, string trail)
        {
            int checkedAddresses = 0;

            foreach (Match match in Regex.Matches(latex, @"htmlData\{p=([^}]+)\}"))
            {
                string address = match.Groups[1].Value;
                Assert.True(manager.SetCursorPosition(address), trail + "  |  address " + address);
                checkedAddresses++;
            }

            return checkedAddresses;
        }

        [Fact]
        public void TheKeypadSurvivesAnySequenceOfKeys()
        {
            var random = new Random(Seed);
            var vocabulary = new List<string>(Vocabulary.Everything()) { "=", "AC", "back", "sd" };

            for (int run = 0; run < Sequences; run++)
            {
                string[] keys = NextSequence(random, vocabulary.ToArray());

                var viewModel = new CalculatorViewModel();
                CalculatorViewModelTests.Press(viewModel, keys);

                string trail = string.Join(" ", keys);
                Assert.False(string.IsNullOrEmpty(viewModel.InputAndResultText), trail);
                Assert.NotNull(viewModel.CalculationText);
            }
        }

        private static string[] NextSequence(Random random, string[] vocabulary)
        {
            var keys = new string[random.Next(1, LongestSequence + 1)];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = vocabulary[random.Next(vocabulary.Length)];
            }

            return keys;
        }

        private static int CountOf(string text, char character)
        {
            int count = 0;
            foreach (char candidate in text)
            {
                if (candidate == character) count++;
            }

            return count;
        }
    }
}
