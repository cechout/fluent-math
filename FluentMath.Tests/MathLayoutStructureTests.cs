using FluentMath.Models;
using FluentMath.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluentMath.Tests
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
                MathAxisRaise = 0,
                FractionBarThickness = 0.1,
                FractionNumeratorGap = 0.1,
                FractionDenominatorGap = 0.1,
                FractionSidePadding = 0.5,
                SuperscriptShift = 0.5,
                SubscriptShift = 0.3,
                DelimiterWidth = 0.4,
                DelimiterPadding = 0,
                DelimiterHeightScale = 1,
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


        // === mixed fractions ===

        private static MixedFractionToken Mixed(string whole, string numerator, string denominator)
        {
            MixedFractionToken mixed = new MixedFractionToken();
            mixed.WholeTokens.Add(Digit(whole));
            mixed.NumeratorTokens.Add(Digit(numerator));
            mixed.DenominatorTokens.Add(Digit(denominator));

            return mixed;
        }

        // nothing is drawn in front of the whole part, which is what lets the cursor skip the place in front
        // of the token the way it skips the one in front of a power
        [Fact]
        public void AMixedFractionIsItsWholePartAtFullSizeBesideAnOrdinaryFraction()
        {
            RowBox box = Assert.IsType<RowBox>(Row(Mixed("2", "1", "3")).Children.Single());

            Assert.Equal(2, box.Children.Count);

            RowBox whole = Assert.IsType<RowBox>(box.Children[0]);
            Assert.Equal("2", ((TextRunBox)whole.Children.Single()).Text);
            Assert.Equal(FontSize, whole.FontSize);

            FractionBox fraction = Assert.IsType<FractionBox>(box.Children[1]);
            Assert.Equal(FontSize * 0.5, ((RowBox)fraction.Numerator).FontSize);
        }

        [Fact]
        public void TheWholePartStandsItsGapAwayFromTheBar()
        {
            MathLayoutStyle style = Style();
            style.MixedFractionGap = 0.3;

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Mixed("2", "1", "3") });
            RowBox box = (RowBox)row.Children.Single();

            Assert.Equal(FontSize * 0.3, box.Children[0].TrailingGap);
        }

        [Fact]
        public void TheThreePartsCarryTheSlotAddressesInReadingOrder()
        {
            RowBox box = (RowBox)Row(Mixed("2", "1", "3")).Children.Single();
            FractionBox fraction = (FractionBox)box.Children[1];

            Assert.Equal("0.0@1", ((RowBox)box.Children[0]).EndAddress);
            Assert.Equal("0.1@1", ((RowBox)fraction.Numerator).EndAddress);
            Assert.Equal("0.2@1", ((RowBox)fraction.Denominator).EndAddress);
        }

        [Fact]
        public void AnEmptyTemplateHoldsAPlaceholderInEachPart()
        {
            RowBox box = (RowBox)Row(new MixedFractionToken()).Children.Single();
            FractionBox fraction = (FractionBox)box.Children[1];

            Assert.IsType<PlaceholderBox>(box.Children[0]);
            Assert.IsType<PlaceholderBox>(fraction.Numerator);
            Assert.IsType<PlaceholderBox>(fraction.Denominator);
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

        [Theory]
        [InlineData("floor", DelimiterKind.FloorOpen, DelimiterKind.FloorClose)]
        [InlineData("ceil", DelimiterKind.CeilingOpen, DelimiterKind.CeilingClose)]
        public void FloorAndCeilingDrawTheirOwnBrackets(string name, DelimiterKind open, DelimiterKind close)
        {
            FunctionToken function = new FunctionToken(name);
            function.ParameterTokens.Add(Digit("9"));

            RowBox box = (RowBox)Row(function).Children.Single();

            Assert.Equal(3, box.Children.Count);
            Assert.Equal(open, ((DelimiterBox)box.Children[0]).Kind);
            Assert.Equal(close, ((DelimiterBox)box.Children[2]).Kind);
        }

        // the comma is drawn rather than typed, and it is what keeps the end of the first argument and the
        // start of the second apart on screen
        [Fact]
        public void ATwoArgumentFunctionDrawsACommaBetweenItsArguments()
        {
            FunctionToken function = new FunctionToken("gcd");
            function.Arguments[0].Add(Digit("4"));
            function.Arguments[1].Add(Digit("6"));

            RowBox box = (RowBox)Row(function).Children.Single();
            RowBox arguments = Assert.IsType<RowBox>(box.Children[2]);

            Assert.Equal("GCD", ((TextRunBox)box.Children[0]).Text);
            Assert.Equal(3, arguments.Children.Count);
            Assert.Equal(",", ((TextRunBox)arguments.Children[1]).Text);
            Assert.Equal("0.0@1", ((RowBox)arguments.Children[0]).EndAddress);
            Assert.Equal("0.1@1", ((RowBox)arguments.Children[2]).EndAddress);
        }

        [Fact]
        public void AnEmptySecondArgumentStillHoldsItsPlace()
        {
            FunctionToken function = new FunctionToken("ranint");

            RowBox box = (RowBox)Row(function).Children.Single();
            RowBox arguments = (RowBox)box.Children[2];

            Assert.IsType<PlaceholderBox>(arguments.Children[0]);
            Assert.IsType<PlaceholderBox>(arguments.Children[2]);
        }

        // the way a Casio prints them and the way their keys are labelled, sin⁻¹ rather than arcsin
        [Theory]
        [InlineData("arcsin", "sin")]
        [InlineData("arccot", "cot")]
        [InlineData("arcoth", "coth")]
        public void EveryInverseDrawsThePlainFunctionCarryingARaisedMinusOne(string name, string drawn)
        {
            FunctionToken function = new FunctionToken(name);
            function.ParameterTokens.Add(Digit("1"));

            RowBox box = (RowBox)Row(function).Children.Single();

            Assert.Equal(drawn, ((TextRunBox)box.Children[0]).Text);
            Assert.Equal("−1", ((TextRunBox)box.Children[1]).Text);
        }

        // a letter stands on the baseline beside the digits, where a plus is lifted to the math axis
        [Fact]
        public void TheLetterOfACombinationStandsOnTheBaseline()
        {
            MathLayoutStyle style = Style();
            style.MathAxisRaise = 0.3;

            RowBox row = Engine(style).BuildRow(new List<MathToken>
            {
                Digit("5"), new MathToken(TokenType.Operator, "C"), Digit("2"),
                new MathToken(TokenType.Operator, "+"), Digit("1")
            });

            TextRunBox letter = (TextRunBox)row.Children[1];

            Assert.Equal("C", letter.Text);
            Assert.Equal(0, letter.Raise);
            Assert.True(letter.LeadingGap > 0);
            Assert.True(row.Children[3].Raise > 0);
        }

        // the sign on the axis like a divided by, the R on the baseline like the letter of nCr, and the
        // operator gaps around the two of them
        [Fact]
        public void TheDivisionWithRemainderIsADivisionSignWithAnROnTheBaseline()
        {
            MathLayoutStyle style = Style();
            style.MathAxisRaise = 0.3;

            RowBox row = Engine(style).BuildRow(new List<MathToken>
            {
                Digit("7"), new MathToken(TokenType.Operator, "÷R"), Digit("2")
            });

            RowBox pair = Assert.IsType<RowBox>(row.Children[1]);
            TextRunBox sign = (TextRunBox)pair.Children[0];
            TextRunBox letter = (TextRunBox)pair.Children[1];

            Assert.Equal("÷", sign.Text);
            Assert.True(sign.Raise > 0);
            Assert.Equal("R", letter.Text);
            Assert.Equal(0, letter.Raise);

            Assert.True(pair.LeadingGap > 0);
            Assert.Equal(pair.LeadingGap, pair.TrailingGap);
            Assert.Equal(0, sign.LeadingGap);
            Assert.Equal(0, sign.TrailingGap);
        }

        [Fact]
        public void APrefixIsWrittenAsItsSymbolAndRanAsItsName()
        {
            RowBox row = Row(Digit("5"), new PostfixToken("micro"), new RandomToken());

            Assert.Equal("μ", ((TextRunBox)row.Children[1]).Text);
            Assert.Equal("Ran#", ((TextRunBox)row.Children[2]).Text);
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

        // === typed brackets ===

        // a bracket that was typed grows with what it encloses exactly the way the bracket of a
        // function does; it is the same box and the same reach, worked out one step later because only
        // the row knows what stands between a bracket and its partner

        private static MathToken Open() => new MathToken(TokenType.BracketOpen, "(");

        private static MathToken Close() => new MathToken(TokenType.BracketClose, ")");

        private static FractionToken Half()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            return fraction;
        }

        [Fact]
        public void ATypedBracketIsADelimiterAndNotAGlyph()
        {
            RowBox row = Row(Open(), Digit("1"), Close());

            Assert.Equal(DelimiterKind.ParenthesisOpen, ((DelimiterBox)row.Children[0]).Kind);
            Assert.Equal(DelimiterKind.ParenthesisClose, ((DelimiterBox)row.Children[2]).Kind);
        }

        [Fact]
        public void ATypedBracketTakesItsHeightFromWhatStandsBetweenItAndItsPartner()
        {
            RowBox row = Row(Open(), Half(), Close());

            MathBox fraction = row.Children[1];
            DelimiterBox open = (DelimiterBox)row.Children[0];
            DelimiterBox close = (DelimiterBox)row.Children[2];

            Assert.Equal(fraction.Ascent, open.Ascent);
            Assert.Equal(fraction.Descent, open.Descent);
            Assert.Equal(open.Ascent, close.Ascent);
            Assert.Equal(open.Descent, close.Descent);
        }

        [Fact]
        public void ATypedBracketAroundADigitStaysAsShortAsTheDigit()
        {
            RowBox tall = Row(Open(), Half(), Close());
            RowBox flat = Row(Open(), Digit("1"), Close());

            Assert.True(((DelimiterBox)flat.Children[0]).Ascent < ((DelimiterBox)tall.Children[0]).Ascent);
        }

        // the pairs resolve from the inside out, so an outer bracket is measured against an inner one
        // that has already grown rather than against the one it was built at
        [Fact]
        public void AnOuterTypedBracketReachesPastTheOneInsideIt()
        {
            MathLayoutStyle style = Style();
            style.DelimiterPadding = 0.1;

            RowBox row = Engine(style).BuildRow(
                new List<MathToken> { Open(), Open(), Half(), Close(), Close() });

            DelimiterBox outer = (DelimiterBox)row.Children[0];
            DelimiterBox inner = (DelimiterBox)row.Children[1];

            Assert.True(outer.Ascent > inner.Ascent);
            Assert.True(outer.Descent > inner.Descent);
        }

        [Fact]
        public void AnOpeningBracketWithNothingToCloseItReachesToTheEndOfTheRow()
        {
            RowBox row = Row(Open(), Half());

            Assert.Equal(row.Children[1].Ascent, ((DelimiterBox)row.Children[0]).Ascent);
        }

        [Fact]
        public void AClosingBracketWithNothingToOpenItReachesBackToTheStart()
        {
            RowBox row = Row(Half(), Close());

            Assert.Equal(row.Children[0].Ascent, ((DelimiterBox)row.Children[1]).Ascent);
        }

        // a pair around nothing has no content to measure, and a bracket of no height at all is not
        // what an empty pair should look like
        [Fact]
        public void AnEmptyPairStandsAsTallAsOneAroundADigit()
        {
            RowBox empty = Row(Open(), Close());
            RowBox digit = Row(Open(), Digit("1"), Close());

            Assert.Equal(((DelimiterBox)digit.Children[0]).Ascent, ((DelimiterBox)empty.Children[0]).Ascent);
            Assert.Equal(((DelimiterBox)digit.Children[0]).Descent, ((DelimiterBox)empty.Children[0]).Descent);
        }

        [Fact]
        public void ADelimiterKeepsItsSidePaddingBetweenItselfAndWhatItEncloses()
        {
            MathLayoutStyle style = Style();
            style.DelimiterSidePadding = 0.2;

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Open(), Digit("1"), Close() });

            double air = FontSize * 0.2;
            double bracket = FontSize * 0.4; // DelimiterWidth

            Assert.Equal(air, row.Children[0].TrailingGap);
            Assert.Equal(air, row.Children[2].LeadingGap);
            Assert.Equal(bracket + air + FontSize + air + bracket, row.Width);
        }

        [Fact]
        public void ADelimiterTakesOnlyTheShareOfTheReachItsScaleSays()
        {
            MathLayoutStyle style = Style();
            style.DelimiterHeightScale = 0.5;

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Open(), Half(), Close() });

            Assert.Equal(row.Children[1].Ascent * 0.5, ((DelimiterBox)row.Children[0]).Ascent, 9);
        }


        // === recurring decimals ===

        [Fact]
        public void ThePeriodStandsUnderABarThatSpansExactlyIt()
        {
            MathLayoutStyle style = Style();
            style.RecurringBarThickness = 0.1;
            style.RecurringBarGap = 0.2;

            RowBox row = Engine(style).BuildRow(new List<MathToken> { Digit("0"), Digit("."), new RecurringToken("36") });
            Assert.IsType<TextRunBox>(row.Children[0]);

            OverlineBox period = Assert.IsType<OverlineBox>(row.Children[1]);
            TextRunBox digits = Assert.IsType<TextRunBox>(period.Content);
            Assert.Equal("36", digits.Text);
            Assert.Equal(digits.Width, period.Width);

            // the digits reach 0.75 em up, the gap and the bar sit on top of that
            double reach = FontSize * (0.75 + 0.2 + 0.1);
            Assert.Equal(reach, period.Ascent, 9);

            period.Place(0, 100);
            Assert.Equal(100 - reach, period.BarTop, 9);
        }

        // the period is one token, so the caret has a place in front of it and one behind it
        [Fact]
        public void ThePeriodIsOnePlaceForTheCaret()
        {
            RowBox row = Row(Digit("0"), Digit("."), new RecurringToken("3"));

            Assert.Equal("@2", row.Children[1].CursorAddress);
            Assert.Equal("@3", row.EndAddress);
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
        public void TheSexagesimalMarkersAreTheirSigns()
        {
            RowBox row = Row(Digit("2"), new PostfixToken("degrees"), Digit("3"), Digit("0"), new PostfixToken("minutes"),
                Digit("0"), new PostfixToken("seconds"));

            Assert.Equal(new[] { "2", "°", "30", "′", "0", "″" }, row.Children.Select(child => Assert.IsType<TextRunBox>(child).Text));
            Assert.All(row.Children, child => Assert.Equal(0, child.Raise));
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


        // === calculus ===

        // round knobs for the calculus structures on top of the round ones above
        private static MathLayoutStyle CalculusStyle()
        {
            MathLayoutStyle style = Style();
            style.SumSignScale = 2;
            style.SumSignRaise = -0.2;
            style.SumUpperBoundRaise = 1;
            style.SumLowerBoundDrop = 0.5;
            style.IntegralSignScale = 2;
            style.IntegralSignRaise = -0.2;
            style.IntegralUpperBoundRaise = 1;
            style.IntegralLowerBoundDrop = 0.4;
            style.DerivativePointDrop = 0.3;

            return style;
        }

        private static LargeOperatorToken LargeOperator(LargeOperatorKind kind)
        {
            LargeOperatorToken token = new LargeOperatorToken(kind);
            token.LowerTokens.Add(Digit("1"));
            token.UpperTokens.Add(Digit("3"));
            token.BodyTokens.Add(new VariableToken());

            return token;
        }

        [Fact]
        public void ASumIsItsSignWithTheBoundsOverAndUnderItAndTheBodyInBrackets()
        {
            RowBox box = (RowBox)Engine(CalculusStyle()).BuildRow(new List<MathToken> { LargeOperator(LargeOperatorKind.Sum) }).Children.Single();

            StackBox stack = Assert.IsType<StackBox>(box.Children[0]);
            Assert.Equal(DelimiterKind.ParenthesisOpen, ((DelimiterBox)box.Children[1]).Kind);
            Assert.Equal(DelimiterKind.ParenthesisClose, ((DelimiterBox)box.Children[3]).Kind);

            MathBox upper = stack.Children[0];
            TextRunBox sign = Assert.IsType<TextRunBox>(stack.Children[1]);
            RowBox lower = Assert.IsType<RowBox>(stack.Children[2]);

            Assert.Equal("Σ", sign.Text);
            Assert.Equal(FontSize * 2, sign.FontSize);
            Assert.Equal(-FontSize * 0.2, sign.Raise, 9);
            Assert.Equal("x=", ((TextRunBox)lower.Children[0]).Text);

            // the bounds are scripts, measured from the baseline of the sign to their own
            Assert.Equal(FontSize * 0.5, ((TextRunBox)((RowBox)upper).Children.Single()).FontSize);
            Assert.Equal(sign.Raise + FontSize * 1, upper.Raise, 9);
            Assert.Equal(sign.Raise - FontSize * 0.5, lower.Raise, 9);

            // and all three are centred over each other
            Assert.Equal(stack.Width / 2, upper.LeadingGap + upper.Width / 2, 9);
            Assert.Equal(stack.Width / 2, sign.LeadingGap + sign.Width / 2, 9);
            Assert.Equal(stack.Width / 2, lower.LeadingGap + lower.Width / 2, 9);
        }

        // a fraction reaches further below its baseline than a digit, and the bound moves up by the difference
        [Fact]
        public void ABoundTallerThanADigitMovesAwayFromTheSign()
        {
            FractionToken fraction = new FractionToken();
            fraction.NumeratorTokens.Add(Digit("1"));
            fraction.DenominatorTokens.Add(Digit("2"));

            LargeOperatorToken sum = LargeOperator(LargeOperatorKind.Sum);
            sum.UpperTokens.Clear();
            sum.UpperTokens.Add(fraction);

            RowBox box = (RowBox)Engine(CalculusStyle()).BuildRow(new List<MathToken> { sum }).Children.Single();
            StackBox stack = (StackBox)box.Children[0];
            MathBox upper = stack.Children[0];

            double digitDescent = FontSize * 0.5 * 0.25;
            Assert.True(upper.Descent > digitDescent);
            Assert.Equal(stack.Children[1].Raise + FontSize * 1 + upper.Descent - digitDescent, upper.Raise, 9);
        }

        [Fact]
        public void AProductDrawsACapitalPi()
        {
            RowBox box = (RowBox)Row(LargeOperator(LargeOperatorKind.Product)).Children.Single();

            Assert.Equal("Π", ((TextRunBox)((StackBox)box.Children[0]).Children[1]).Text);
        }

        [Fact]
        public void TheBoundsOfAnIntegralStandBesideTheTopAndTheFootOfItsSign()
        {
            RowBox row = Engine(CalculusStyle()).BuildRow(new List<MathToken> { LargeOperator(LargeOperatorKind.Integral) });
            row.Place(0, row.Ascent);

            RowBox box = (RowBox)row.Children.Single();
            TextRunBox sign = Assert.IsType<TextRunBox>(box.Children[0]);
            StackBox bounds = Assert.IsType<StackBox>(box.Children[1]);
            Assert.Equal("dx", ((TextRunBox)box.Children[3]).Text);

            Assert.Equal("∫", sign.Text);

            MathBox upper = bounds.Children[0];
            MathBox lower = bounds.Children[1];

            Assert.Equal(sign.Raise + FontSize * 1, upper.Raise, 9);
            Assert.Equal(sign.Raise - FontSize * 0.4, lower.Raise, 9);
            Assert.Equal(upper.X, lower.X, 9);
            Assert.True(upper.Bottom <= lower.Top);
        }

        [Fact]
        public void ADerivativeIsDOverDxTheFunctionInBracketsAndThePointAtTheFootOfABar()
        {
            DerivativeToken derivative = new DerivativeToken();
            derivative.FunctionTokens.Add(new VariableToken());
            derivative.PointTokens.Add(Digit("2"));

            RowBox box = (RowBox)Engine(CalculusStyle()).BuildRow(new List<MathToken> { derivative }).Children.Single();

            FractionBox operatorBox = Assert.IsType<FractionBox>(box.Children[0]);
            Assert.Equal("d", ((TextRunBox)operatorBox.Numerator).Text);
            Assert.Equal("dx", ((TextRunBox)operatorBox.Denominator).Text);

            DelimiterBox open = (DelimiterBox)box.Children[1];
            DelimiterBox bar = (DelimiterBox)box.Children[4];
            Assert.Equal(DelimiterKind.Bar, bar.Kind);
            Assert.Equal(open.Ascent, bar.Ascent);
            Assert.Equal(open.Descent, bar.Descent);

            RowBox point = Assert.IsType<RowBox>(box.Children[5]);
            Assert.Equal("x=", ((TextRunBox)point.Children[0]).Text);
            Assert.Equal(-FontSize * 0.3, point.Raise, 9);
            Assert.True(point.TrailingGap > 0);
        }

        [Fact]
        public void AnEmptyCalculusStructureStillHoldsEverySlotOpen()
        {
            RowBox row = Row(new LargeOperatorToken(LargeOperatorKind.Sum), new LargeOperatorToken(LargeOperatorKind.Integral),
                new DerivativeToken());

            foreach (MathBox child in row.Children)
            {
                Assert.True(child.Width > 0);
                Assert.True(child.Height > 0);
            }
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

        // the square is centred on the middle of a digit rather than on the middle of the box, which reaches
        // well above the digits
        [Fact]
        public void TheSquareOfAnEmptySlotStandsWhereADigitWould()
        {
            MathLayoutStyle style = Style();
            style.PlaceholderRaise = 0.35;

            RowBox row = Engine(style).BuildRow(new List<MathToken> { new FractionToken() });
            row.Place(0, row.Ascent);

            PlaceholderBox placeholder = (PlaceholderBox)((FractionBox)row.Children.Single()).Numerator;

            double side = FontSize * 0.5 * 0.6;
            Assert.Equal(placeholder.Baseline - FontSize * 0.5 * 0.35 - side / 2, placeholder.SquareTop, 9);
        }

        // x= and the bound behind it are one row, so a digit typed into the bound stands on the baseline of
        // the x=
        [Fact]
        public void TheLowerBoundOfASumStandsOnTheBaselineOfItsXEquals()
        {
            RowBox row = Engine(CalculusStyle()).BuildRow(new List<MathToken> { LargeOperator(LargeOperatorKind.Sum) });
            row.Place(0, row.Ascent);

            RowBox lower = (RowBox)((StackBox)((RowBox)row.Children.Single()).Children[0]).Children[2];
            MathBox digit = ((RowBox)lower.Children[1]).Children.Single();

            Assert.Equal(lower.Children[0].Baseline, digit.Baseline, 9);
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
