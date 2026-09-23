using FluentMath.Engines;
using FluentMath.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluentMath.Tests
{
    // the copy the history line holds after = , which has to survive the tree being edited on
    public class MathTokenClonerTests
    {
        private static MathToken Digit(string value) => new MathToken(TokenType.Number, value);


        // === detachment ===

        [Fact]
        public void ACopyOutlivesTheListItWasTakenFrom()
        {
            MathInputManager manager = Keys.Press("1", "+", "2");
            List<MathToken> snapshot = MathTokenCloner.CloneList(manager.RootTokens);

            // the manager clears its root list in place, so a reference to it would come back empty here
            manager.Clear();

            Assert.Empty(manager.RootTokens);
            Assert.Equal(3, snapshot.Count);
        }

        [Fact]
        public void EditingASlotAfterwardsDoesNotReachIntoTheCopy()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionToken copy = (FractionToken)MathTokenCloner.Clone(fraction);
            fraction.NumeratorTokens.Add(Digit("9"));

            Assert.Single(copy.NumeratorTokens);
            Assert.Equal(2, fraction.NumeratorTokens.Count);
        }

        [Fact]
        public void NothingInTheCopyIsTheSameObjectAsInTheOriginal()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));

            FractionToken copy = (FractionToken)MathTokenCloner.Clone(fraction);

            Assert.NotSame(fraction, copy);
            Assert.NotSame(fraction.NumeratorTokens[0], copy.NumeratorTokens[0]);
        }


        // === every kind ===

        [Fact]
        public void EveryStructuredTokenKeepsItsSlotsAndTheirOrder()
        {
            RootToken root = new RootToken();
            root.IndexTokens.Add(Digit("3"));
            root.RadicandTokens.Add(Digit("8"));

            LogarithmToken logarithm = new LogarithmToken();
            logarithm.BaseTokens.Add(Digit("2"));
            logarithm.ParameterTokens.Add(Digit("8"));

            PowerToken power = new PowerToken();
            power.BaseTokens.Add(Digit("9"));
            power.ExponentTokens.Add(Digit("2"));

            FunctionToken function = new FunctionToken("sin");
            function.ParameterTokens.Add(Digit("0"));

            List<MathToken> copy = MathTokenCloner.CloneList(
                new List<MathToken> { root, logarithm, power, function });

            Assert.Equal("3", ((RootToken)copy[0]).IndexTokens.Single().Value);
            Assert.Equal("8", ((RootToken)copy[0]).RadicandTokens.Single().Value);
            Assert.Equal("2", ((LogarithmToken)copy[1]).BaseTokens.Single().Value);
            Assert.Equal("8", ((LogarithmToken)copy[1]).ParameterTokens.Single().Value);
            Assert.Equal("9", ((PowerToken)copy[2]).BaseTokens.Single().Value);
            Assert.Equal("2", ((PowerToken)copy[2]).ExponentTokens.Single().Value);
            Assert.Equal("0", ((FunctionToken)copy[3]).ParameterTokens.Single().Value);
        }

        [Fact]
        public void ALeafRebuildsThePrivateStateItKeepsBesideItsValue()
        {
            // a constant carries its numeric value and a function its bar flag in fields the copy never
            // sees, so both have to come back out of the name
            ConstantToken pi = (ConstantToken)MathTokenCloner.Clone(new ConstantToken("pi"));
            FunctionToken abs = (FunctionToken)MathTokenCloner.Clone(new FunctionToken("abs"));

            Assert.Equal(Math.PI, pi.NumericValue);
            Assert.True(abs.DrawsAsBars);
        }

        [Fact]
        public void NestingIsCopiedAllTheWayDown()
        {
            FractionToken inner = new FractionToken();
            inner.NumeratorTokens.Add(Digit("1"));

            PowerToken power = new PowerToken();
            power.ExponentTokens.Add(inner);

            FractionToken outer = new FractionToken();
            outer.NumeratorTokens.Add(power);

            FractionToken copy = (FractionToken)MathTokenCloner.Clone(outer);
            PowerToken copiedPower = (PowerToken)copy.NumeratorTokens.Single();
            FractionToken copiedInner = (FractionToken)copiedPower.ExponentTokens.Single();

            Assert.NotSame(inner, copiedInner);
            Assert.Equal("1", copiedInner.NumeratorTokens.Single().Value);
        }


        // === the whole vocabulary ===

        [Fact]
        public void EveryKeyTheAppAcceptsProducesATreeThatCanBeCopied()
        {
            // the cloner switches on the token type from outside the classes, so a kind added later would
            // be missed; walking the vocabulary is what makes that fail here rather than on screen
            foreach (string key in Keys.All)
            {
                MathInputManager manager = Keys.Press(key);
                List<MathToken> copy = MathTokenCloner.CloneList(manager.RootTokens);

                Assert.Equal(manager.RootTokens.Count, copy.Count);
            }
        }
    }
}
