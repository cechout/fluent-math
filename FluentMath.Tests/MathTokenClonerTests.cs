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
            Assert.Equal(FunctionShape.Bars, abs.Shape);
        }

        // the second argument is a slot like the first and has to be copied, not recreated empty
        [Fact]
        public void ATwoArgumentFunctionKeepsBothArguments()
        {
            FunctionToken gcd = new FunctionToken("gcd");
            gcd.Arguments[0].Add(Digit("4"));
            gcd.Arguments[1].Add(Digit("6"));

            FunctionToken copy = (FunctionToken)MathTokenCloner.Clone(gcd);

            Assert.Equal("4", copy.Arguments[0].Single().Value);
            Assert.Equal("6", copy.Arguments[1].Single().Value);
            Assert.NotSame(gcd.Arguments[1], copy.Arguments[1]);
        }

        // the seeded value is what makes a result carry on in full, so a copy keeps pointing at it
        [Fact]
        public void ADigitKeepsTheValueItWasSeededWith()
        {
            SeededValue seed = new SeededValue(1.0 / 3.0, 1);
            MathToken digit = new MathToken(TokenType.Number, "0") { Seed = seed };

            Assert.Same(seed, MathTokenCloner.Clone(digit).Seed);
        }

        [Fact]
        public void AMixedFractionKeepsItsThreeParts()
        {
            MixedFractionToken mixed = new MixedFractionToken();
            mixed.WholeTokens.Add(Digit("2"));
            mixed.NumeratorTokens.Add(Digit("1"));
            mixed.DenominatorTokens.Add(Digit("3"));

            MixedFractionToken copy = (MixedFractionToken)MathTokenCloner.Clone(mixed);

            Assert.Equal("2", copy.WholeTokens.Single().Value);
            Assert.Equal("1", copy.NumeratorTokens.Single().Value);
            Assert.Equal("3", copy.DenominatorTokens.Single().Value);
            Assert.NotSame(mixed.WholeTokens, copy.WholeTokens);
        }

        [Fact]
        public void ACalculusStructureKeepsItsKindAndEverySlot()
        {
            LargeOperatorToken product = new LargeOperatorToken(LargeOperatorKind.Product);
            product.LowerTokens.Add(Digit("1"));
            product.UpperTokens.Add(Digit("5"));
            product.BodyTokens.Add(new VariableToken());

            LargeOperatorToken copy = (LargeOperatorToken)MathTokenCloner.Clone(product);

            Assert.Equal(LargeOperatorKind.Product, copy.Kind);
            Assert.Equal("1", copy.LowerTokens.Single().Value);
            Assert.Equal("5", copy.UpperTokens.Single().Value);
            Assert.IsType<VariableToken>(copy.BodyTokens.Single());
            Assert.NotSame(product.BodyTokens, copy.BodyTokens);

            DerivativeToken derivative = new DerivativeToken();
            derivative.FunctionTokens.Add(new VariableToken());
            derivative.PointTokens.Add(Digit("2"));

            DerivativeToken derivativeCopy = (DerivativeToken)MathTokenCloner.Clone(derivative);

            Assert.IsType<VariableToken>(derivativeCopy.FunctionTokens.Single());
            Assert.Equal("2", derivativeCopy.PointTokens.Single().Value);
        }

        [Fact]
        public void ThePanelLeavesComeBackAsWhatTheyWere()
        {
            Assert.IsType<RandomToken>(MathTokenCloner.Clone(new RandomToken()));
            Assert.Equal("142857", Assert.IsType<RecurringToken>(MathTokenCloner.Clone(new RecurringToken("142857"))).Value);
            Assert.Equal("k", ((PostfixToken)MathTokenCloner.Clone(new PostfixToken("kilo"))).Symbol);
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
