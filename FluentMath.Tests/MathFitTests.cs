using FluentMath.Models.Layout;
using Xunit;

namespace FluentMath.Tests
{
    // the scale a formula taller than its box is drawn at
    public class MathFitTests
    {
        private const double Floor = 0.45;

        [Fact]
        public void AFormulaThatFitsIsDrawnAtFullSize()
        {
            Assert.Equal(1, MathFit.ScaleFor(contentHeight: 40, availableHeight: 60, Floor));
        }

        [Fact]
        public void AFormulaExactlyAsTallAsItsBoxIsStillFullSize()
        {
            Assert.Equal(1, MathFit.ScaleFor(60, 60, Floor));
        }

        [Fact]
        public void AShortFormulaIsNeverScaledUpToFillTheBox()
        {
            Assert.Equal(1, MathFit.ScaleFor(10, 100, Floor));
        }

        [Fact]
        public void ATallFormulaIsScaledToTheHeightOfItsBox()
        {
            // 68 into 34 is exactly half, which is above the floor
            Assert.Equal(0.5, MathFit.ScaleFor(68, 34, Floor));
        }

        [Fact]
        public void TheScaleStopsAtTheFloorRatherThanShrinkingToNothing()
        {
            Assert.Equal(Floor, MathFit.ScaleFor(contentHeight: 1000, availableHeight: 10, Floor));
        }

        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NaN)]
        [InlineData(0)]
        [InlineData(-5)]
        public void ABoxWithNoUsableHeightAsksForNoScaling(double availableHeight)
        {
            // an unmeasured or unbounded box arrives during the first layout pass and must not collapse
            // the formula on the way through
            Assert.Equal(1, MathFit.ScaleFor(100, availableHeight, Floor));
        }

        [Fact]
        public void AnEmptyFormulaAsksForNoScaling()
        {
            Assert.Equal(1, MathFit.ScaleFor(contentHeight: 0, availableHeight: 10, Floor));
        }
    }
}
