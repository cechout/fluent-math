using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the structured tokens: fractions, powers, roots, logarithms, functions and the scientific form
    //
    // the same fake measurer as MathLayoutTests, so every expected number is a short multiplication and
    // nothing here depends on a font
    public class MathLayoutStructureTests
    {
        private sealed class FakeMeasurer : ITextMeasurer
        {
            public TextMetrics Measure(string text, double fontSizePx)
            {
                return new TextMetrics(text.Length * fontSizePx, fontSizePx * 0.75, fontSizePx * 0.25);
            }
        }

        private const double FontSize = 10;

        // a style with the shaping knobs at round numbers, so an assertion reads as arithmetic rather
        // than as a pile of defaults
        private static MathLayoutStyle Style()
        {
            return new MathLayoutStyle
            {
                FontSizePx = FontSize,
                ScriptScale = 0.5,
                ScriptScriptScale = 0.25,
                MathAxisHeight = 0.2,
                FractionBarThickness = 0.1,
                FractionNumeratorGap = 0.1,
                FractionDenominatorGap = 0.1,
                FractionSidePadding = 0.5,
                SuperscriptShift = 0.5,
                SubscriptShift = 0.3,
                DelimiterWidth = 0.4,
                DelimiterPadding = 0,
                PlaceholderSize = 0.6,
                RadicalHookWidth = 0.5,
                RadicalLeadingPad = 0.2,
                RadicalTrailingPad = 0.2,
                RadicalPadScaling = 1
            };
        }

        private static MathLayoutEngine Engine(MathLayoutStyle style = null)
        {
            return new MathLayoutEngine(new FakeMeasurer(), style ?? Style());
        }

        private static MathToken Digit(string value) => new MathToken(TokenType.Number, value);

        private static RowBox Row(params MathToken[] tokens)
        {
            return Engine().BuildRow(tokens.ToList());
        }


        // === script levels ===

        [Fact]
        public void ANumeratorIsSetAtScriptSize()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionBox box = Assert.IsType<FractionBox>(Assert.Single(Row(fraction).Children));

            RowBox numerator = Assert.IsType<RowBox>(box.Numerator);
            Assert.Equal(FontSize * 0.5, ((TextRunBox)numerator.Children.Single()).FontSize);
        }

        [Fact]
        public void ASecondLevelInDropsToScriptScriptSize()
        {
            FractionToken inner = new FractionToken();
            inner.NumeratorTokens.Add(Digit("1"));
            inner.DenominatorTokens.Add(Digit("2"));

            FractionToken outer = new FractionToken();
            outer.NumeratorTokens.Add(inner);
            outer.DenominatorTokens.Add(Digit("3"));

            FractionBox outerBox = Assert.IsType<FractionBox>(Assert.Single(Row(outer).Children));
            FractionBox innerBox = (FractionBox)((RowBox)outerBox.Numerator).Children.Single();
            RowBox innerNumerator = (RowBox)innerBox.Numerator;

            Assert.Equal(FontSize * 0.25, ((TextRunBox)innerNumerator.Children.Single()).FontSize);
        }

        [Fact]
        public void AThirdLevelInStopsShrinkingRatherThanVanishing()
        {
            FractionToken third = new FractionToken();
            third.NumeratorTokens.Add(Digit("1"));

            FractionToken second = new FractionToken();
            second.NumeratorTokens.Add(third);

            FractionToken first = new FractionToken();
            first.NumeratorTokens.Add(second);

            FractionBox box1 = (FractionBox)Row(first).Children.Single();
            FractionBox box2 = (FractionBox)((RowBox)box1.Numerator).Children.Single();
            FractionBox box3 = (FractionBox)((RowBox)box2.Numerator).Children.Single();
            RowBox deepest = (RowBox)box3.Numerator;

            // the step from scriptscript to the level below it is a factor of one
            Assert.Equal(FontSize * 0.25, ((TextRunBox)deepest.Children.Single()).FontSize);
        }

        [Fact]
        public void ADisplayFractionKeepsBothHalvesAtFullSize()
        {
            MathLayoutStyle style = Style();
            style.UseDisplayFractions = true;

            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionBox box = (FractionBox)Engine(style).BuildRow(new List<MathToken> { fraction }).Children.Single();

            Assert.Equal(FontSize, ((TextRunBox)((RowBox)box.Numerator).Children.Single()).FontSize);
        }


        // === fractions ===

        [Fact]
        public void AFractionIsAsWideAsItsWiderHalfPlusThePaddingOnBothSides()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));
            fraction.DenominatorTokens.Add(Digit("3"));

            FractionBox box = (FractionBox)Row(fraction).Children.Single();

            double half = FontSize * 0.5;              // script size
            double padding = FontSize * 0.5;           // FractionSidePadding
            Assert.Equal(half * 2 + padding * 2, box.Width);
        }

        [Fact]
        public void TheBarSitsOnTheMathAxisSoAFractionStaysLevelWithWhatIsBesideIt()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionBox box = (FractionBox)Row(fraction).Children.Single();
            box.Place(0, 100);

            double axis = FontSize * 0.2;
            double thickness = FontSize * 0.1;
            Assert.Equal(100 - axis - thickness / 2, box.BarTop);
        }

        [Fact]
        public void AFractionReachesAboveTheBarByItsNumeratorAndBelowItByItsDenominator()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionBox box = (FractionBox)Row(fraction).Children.Single();

            double axis = FontSize * 0.2;
            double half = FontSize * 0.1 / 2;
            double gap = FontSize * 0.1;
            double halfHeight = FontSize * 0.5; // script size, ascent plus descent

            Assert.Equal(axis + half + gap + halfHeight, box.Ascent);
            Assert.Equal(gap + halfHeight - (axis - half), box.Descent);
        }

        [Fact]
        public void BothHalvesAreCentredOverTheBar()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));
            fraction.DenominatorTokens.Add(Digit("3"));

            FractionBox box = (FractionBox)Row(fraction).Children.Single();
            box.Place(0, 100);

            double centre = box.Width / 2;
            Assert.Equal(centre, box.Numerator.X + box.Numerator.Width / 2, 9);
            Assert.Equal(centre, box.Denominator.X + box.Denominator.Width / 2, 9);
        }


        // === powers and the scientific form ===

        [Fact]
        public void AnExponentIsLiftedByTheAscentOfItsOwnBaseRatherThanByAFixedAmount()
        {
            PowerToken power = new PowerToken();
            power.BaseTokens.Add(Digit("9"));
            power.ExponentTokens.Add(Digit("8"));

            RowBox box = Assert.IsType<RowBox>(Row(power).Children.Single());

            double baseAscent = FontSize * 0.75;
            Assert.Equal(baseAscent * 0.5, box.Children[1].Raise);
        }

        [Fact]
        public void ATallBaseLiftsItsExponentFurtherThanAShortOne()
        {
            FractionToken tall = new FractionToken();
            tall.NumeratorTokens.Add(Digit("1"));
            tall.DenominatorTokens.Add(Digit("2"));

            PowerToken overFraction = new PowerToken();
            overFraction.BaseTokens.Add(tall);
            overFraction.ExponentTokens.Add(Digit("3"));

            PowerToken overDigit = new PowerToken();
            overDigit.BaseTokens.Add(Digit("9"));
            overDigit.ExponentTokens.Add(Digit("3"));

            RowBox tallBox = (RowBox)Row(overFraction).Children.Single();
            RowBox shortBox = (RowBox)Row(overDigit).Children.Single();

            Assert.True(tallBox.Children[1].Raise > shortBox.Children[1].Raise);
        }

        [Fact]
        public void TheScientificFormIsAMultiplicationAndAPowerLikeAnyOther()
        {
            // the EXP key spells out times, one, zero, power rather than making a shape of its own, so
            // there is nothing here the layout has to know about
            PowerToken power = new PowerToken();
            power.BaseTokens.Add(Digit("1"));
            power.BaseTokens.Add(Digit("0"));
            power.ExponentTokens.Add(Digit("5"));

            RowBox row = Row(new MathToken(TokenType.Operator, "*"), power);

            Assert.Equal("×", ((TextRunBox)row.Children[0]).Text);

            RowBox powerBox = Assert.IsType<RowBox>(row.Children[1]);
            Assert.Equal("10", ((TextRunBox)((RowBox)powerBox.Children[0]).Children.Single()).Text);
            Assert.True(powerBox.Children[1].Raise > 0);
        }


        // === roots ===

        [Fact]
        public void ARootWithNoIndexDrawsNoneRatherThanAPlaceholder()
        {
            RootToken root = new RootToken();
            root.RadicandTokens.Add(Digit("2"));

            RootBox box = Assert.IsType<RootBox>(Row(root).Children.Single());

            Assert.Null(box.Index);
            Assert.Equal(0, box.IndexWidth);
        }

        [Fact]
        public void ARootReachesAboveItsRadicandByTheGapAndTheRule()
        {
            MathLayoutStyle style = Style();
            style.RadicalVerticalGap = 0.1;
            style.RadicalRuleThickness = 0.05;

            RootToken root = new RootToken();
            root.RadicandTokens.Add(Digit("2"));

            RootBox box = (RootBox)Engine(style).BuildRow(new List<MathToken> { root }).Children.Single();

            Assert.Equal(FontSize * 0.75 + FontSize * 0.1 + FontSize * 0.05, box.SignAscent);
        }

        [Fact]
        public void TheRadicandSitsAfterTheIndexAndTheHookAndTheAirBehindIt()
        {
            MathLayoutStyle style = Style();

            RootToken root = new RootToken();
            root.IndexTokens.Add(Digit("3"));
            root.RadicandTokens.Add(Digit("2"));

            RootBox box = (RootBox)Engine(style).BuildRow(new List<MathToken> { root }).Children.Single();
            box.Place(0, 100);

            // the sign fills the hook to its last pixel, so the pad is the only thing between it and the
            // first glyph of what it encloses
            Assert.Equal(box.IndexWidth + FontSize * 0.5 + FontSize * 0.2, box.Radicand.X);
        }

        [Fact]
        public void TheBarReachesPastBothEndsOfTheRadicand()
        {
            RootToken root = new RootToken();
            root.RadicandTokens.Add(Digit("2"));

            RootBox box = (RootBox)Row(root).Children.Single();

            // hook, the air in front, the digit, the air behind
            Assert.Equal(FontSize * 0.5 + FontSize * 0.2 + FontSize + FontSize * 0.2, box.Width);
        }

        // a root set inside a fraction is drawn at script size, and air written in em alone shrinks with
        // it until the sign sits on its content
        [Theory]
        [InlineData(1.0, 0.5)]  // fully proportional: half the size, half the air
        [InlineData(0.0, 1.0)]  // held: the air a full size root keeps, whatever size this one is
        [InlineData(0.5, 0.75)] // half way between the two
        public void TheAirAroundARadicandFollowsTheSizeOnlyAsFarAsItsScalingSays(
            double scaling, double expectedShare)
        {
            MathLayoutStyle style = Style();
            style.RadicalPadScaling = scaling;

            RootToken root = new RootToken();
            root.RadicandTokens.Add(Digit("2"));

            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(root);
            fraction.DenominatorTokens.Add(Digit("1"));

            FractionBox box = (FractionBox)Engine(style)
                .BuildRow(new List<MathToken> { fraction }).Children.Single();

            RootBox nested = (RootBox)((RowBox)box.Numerator).Children.Single();
            nested.Place(0, 100);

            // the numerator is set at ScriptScale, which this style pins at a half
            double size = FontSize * 0.5;
            double pad = nested.Radicand.X - nested.X - nested.IndexWidth - size * 0.5;

            Assert.Equal(FontSize * 0.2 * expectedShare, pad, 9);
        }

        [Fact]
        public void AnIndexThatStandsHigherThanTheSignSetsTheHeightOfTheRoot()
        {
            MathLayoutStyle style = Style();
            style.RadicalIndexRaise = 1.0; // the index starts at the very top of the sign

            RootToken root = new RootToken();
            root.IndexTokens.Add(Digit("3"));
            root.RadicandTokens.Add(Digit("2"));

            RootBox box = (RootBox)Engine(style).BuildRow(new List<MathToken> { root }).Children.Single();

            Assert.True(box.Ascent > box.SignAscent);
        }


        // === logarithms and functions ===

        [Fact]
        public void ALogarithmBaseHangsBelowTheBaseline()
        {
            LogarithmToken logarithm = new LogarithmToken();
            logarithm.BaseTokens.Add(Digit("2"));
            logarithm.ParameterTokens.Add(Digit("8"));

            RowBox box = Assert.IsType<RowBox>(Row(logarithm).Children.Single());

            Assert.Equal("log", ((TextRunBox)box.Children[0]).Text);
            Assert.Equal(-FontSize * 0.3, box.Children[1].Raise);
        }

        [Fact]
        public void ALogarithmWithNoBaseDrawsNone()
        {
            LogarithmToken logarithm = new LogarithmToken();
            logarithm.ParameterTokens.Add(Digit("8"));

            RowBox box = (RowBox)Row(logarithm).Children.Single();

            // name, open bracket, parameter, close bracket, and nothing between the name and the bracket
            Assert.Equal(4, box.Children.Count);
            Assert.IsType<DelimiterBox>(box.Children[1]);
        }

        [Fact]
        public void AFunctionIsItsNameAndItsParameterInBrackets()
        {
            FunctionToken function = new FunctionToken("sin");
            function.ParameterTokens.Add(Digit("9"));

            RowBox box = Assert.IsType<RowBox>(Row(function).Children.Single());

            Assert.Equal("sin", ((TextRunBox)box.Children[0]).Text);
            Assert.Equal(DelimiterKind.ParenthesisOpen, ((DelimiterBox)box.Children[1]).Kind);
            Assert.Equal(DelimiterKind.ParenthesisClose, ((DelimiterBox)box.Children[3]).Kind);
        }

        [Fact]
        public void AnInverseHyperbolicDrawsThePlainFunctionCarryingARaisedMinusOne()
        {
            FunctionToken function = new FunctionToken("arsinh");
            function.ParameterTokens.Add(Digit("9"));

            RowBox box = (RowBox)Row(function).Children.Single();

            Assert.Equal("sinh", ((TextRunBox)box.Children[0]).Text);
            Assert.Equal("−1", ((TextRunBox)box.Children[1]).Text);
            Assert.True(box.Children[1].Raise > 0);
        }

        [Fact]
        public void TheAbsoluteValueDrawsBarsInsteadOfAName()
        {
            FunctionToken function = new FunctionToken("abs");
            function.ParameterTokens.Add(Digit("9"));

            RowBox box = (RowBox)Row(function).Children.Single();

            Assert.Equal(3, box.Children.Count);
            Assert.Equal(DelimiterKind.Bar, ((DelimiterBox)box.Children[0]).Kind);
            Assert.Equal(DelimiterKind.Bar, ((DelimiterBox)box.Children[2]).Kind);
        }

        [Fact]
        public void ADelimiterTakesItsHeightFromWhatItEncloses()
        {
            FractionToken tall = new FractionToken();
            tall.NumeratorTokens.Add(Digit("1"));
            tall.DenominatorTokens.Add(Digit("2"));

            FunctionToken function = new FunctionToken("sin");
            function.ParameterTokens.Add(tall);

            RowBox box = (RowBox)Row(function).Children.Single();
            DelimiterBox open = (DelimiterBox)box.Children[1];
            MathBox content = box.Children[2];

            Assert.Equal(content.Ascent, open.Ascent);
            Assert.Equal(content.Descent, open.Descent);
        }


        // === postfix ===

        [Fact]
        public void TheFactorialAndThePercentStayPlainSigns()
        {
            RowBox row = Row(new PostfixToken("!"), new PostfixToken("%"));

            Assert.Equal("!", ((TextRunBox)row.Children[0]).Text);
            Assert.Equal("%", ((TextRunBox)row.Children[1]).Text);
            Assert.Equal(0, row.Children[0].Raise);
        }

        [Fact]
        public void TheReciprocalIsARaisedMinusOne()
        {
            RowBox row = Row(new PostfixToken("inv"));

            TextRunBox box = Assert.IsType<TextRunBox>(row.Children.Single());
            Assert.Equal("−1", box.Text);
            Assert.Equal(FontSize * 0.5, box.FontSize); // script size
            Assert.True(box.Raise > 0);
        }


        // === empty slots ===

        [Fact]
        public void AnEmptySlotDrawsABoxSoItCanBeSeenAndReached()
        {
            FractionToken fraction = new FractionToken();
            fraction.DenominatorTokens.Add(Digit("2"));

            FractionBox box = (FractionBox)Row(fraction).Children.Single();

            PlaceholderBox placeholder = Assert.IsType<PlaceholderBox>(box.Numerator);
            Assert.Equal(FontSize * 0.5 * 0.6, placeholder.Width); // script size times PlaceholderSize
            Assert.True(placeholder.Width > 0);
        }

        [Fact]
        public void AnEmptySlotIsExactlyAsTallAsOneHoldingADigit()
        {
            // a fresh fraction used to be half the height of a filled one, because the placeholder box
            // was as tall as its own square rather than as the text that would replace it; the two halves
            // then sat visibly closer together than they would a keystroke later
            FractionToken empty = new FractionToken();

            FractionToken filled = new FractionToken();
            filled.NumeratorTokens.Add(Digit("1"));
            filled.DenominatorTokens.Add(Digit("2"));

            FractionBox emptyBox = (FractionBox)Row(empty).Children.Single();
            FractionBox filledBox = (FractionBox)Row(filled).Children.Single();

            Assert.Equal(filledBox.Numerator.Ascent, emptyBox.Numerator.Ascent);
            Assert.Equal(filledBox.Numerator.Descent, emptyBox.Numerator.Descent);
            Assert.Equal(filledBox.Ascent, emptyBox.Ascent);
            Assert.Equal(filledBox.Descent, emptyBox.Descent);
        }

        [Fact]
        public void EveryKindOfEmptySlotKeepsTheHeightOfItsText()
        {
            // it goes through BuildSlot for all of them, so a root, a logarithm and a function have to
            // agree with a fraction
            RootToken root = new RootToken();
            FunctionToken function = new FunctionToken("sin");
            LogarithmToken logarithm = new LogarithmToken();

            RootBox rootBox = (RootBox)Row(root).Children.Single();
            RowBox functionBox = (RowBox)Row(function).Children.Single();
            RowBox logarithmBox = (RowBox)Row(logarithm).Children.Single();

            double ascent = FontSize * 0.75;
            double descent = FontSize * 0.25;

            Assert.Equal(ascent, rootBox.Radicand.Ascent);
            Assert.Equal(descent, rootBox.Radicand.Descent);
            Assert.Equal(ascent, functionBox.Children[2].Ascent);
            Assert.Equal(descent, functionBox.Children[2].Descent);
            Assert.Equal(ascent, logarithmBox.Children[2].Ascent);
            Assert.Equal(descent, logarithmBox.Children[2].Descent);
        }

        [Fact]
        public void EveryStructuredTokenFallsBackToAPlaceholderRatherThanCollapsing()
        {
            RowBox row = Row(new FractionToken(), new PowerToken(), new RootToken(), new FunctionToken("sin"));

            foreach (MathBox child in row.Children)
            {
                Assert.True(child.Width > 0);
                Assert.True(child.Height > 0);
            }
        }
    }
}
