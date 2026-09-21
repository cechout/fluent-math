using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Calculator_WinUI.Tests
{
    // the hand-off from the knobs to the layout
    //
    // ToLayoutStyle is the only way a value in MathDisplayStyle reaches the screen, so a knob that is not
    // copied there does nothing at all while still looking like it works; that is the failure this file
    // exists to catch
    public class MathDisplayStyleTests
    {
        // knobs MathLayoutStyle has on its own, with no counterpart to copy from
        private static readonly HashSet<string> LayoutOnly = new HashSet<string>
        {
            nameof(MathLayoutStyle.ScriptScale),
            nameof(MathLayoutStyle.ScriptScriptScale),
            nameof(MathLayoutStyle.MathAxisHeight),
            nameof(MathLayoutStyle.FractionNumeratorGap),
            nameof(MathLayoutStyle.FractionDenominatorGap),
            nameof(MathLayoutStyle.FractionSidePadding),
            nameof(MathLayoutStyle.SuperscriptShift),
            nameof(MathLayoutStyle.SubscriptShift),
            nameof(MathLayoutStyle.RadicalHookWidth),
            nameof(MathLayoutStyle.RadicalRuleThickness),
            nameof(MathLayoutStyle.RadicalVerticalGap),
            nameof(MathLayoutStyle.RadicalIndexRaise),
            nameof(MathLayoutStyle.DelimiterWidth),
            nameof(MathLayoutStyle.DelimiterPadding),
            nameof(MathLayoutStyle.DelimiterThickness),
            nameof(MathLayoutStyle.PlaceholderSize),
            nameof(MathLayoutStyle.PlaceholderThickness)
        };

        [Fact]
        public void EveryKnobTheTwoStylesShareIsCarriedAcross()
        {
            // a distinctive value per knob, so a copy that reads the wrong field is caught as well as one
            // that was forgotten
            MathDisplayStyle display = new MathDisplayStyle();
            List<PropertyInfo> shared = typeof(MathLayoutStyle).GetProperties()
                .Where(property => !LayoutOnly.Contains(property.Name))
                .ToList();

            double seed = 0.11;
            foreach (PropertyInfo property in shared)
            {
                PropertyInfo source = typeof(MathDisplayStyle).GetProperty(property.Name);
                Assert.True(source != null, property.Name + " has no counterpart on MathDisplayStyle");

                if (source.PropertyType == typeof(double)) source.SetValue(display, seed += 0.13);
                else if (source.PropertyType == typeof(int)) source.SetValue(display, 321);
                else if (source.PropertyType == typeof(bool)) source.SetValue(display, true);
            }

            MathLayoutStyle layout = display.ToLayoutStyle();

            foreach (PropertyInfo property in shared)
            {
                object expected = typeof(MathDisplayStyle).GetProperty(property.Name).GetValue(display);

                Assert.Equal(expected, property.GetValue(layout));
            }
        }

        [Fact]
        public void TheTwoDisplayLinesDifferInBaseSizeAndNothingElse()
        {
            MathLayoutStyle input = MathDisplayStyle.ForInputLine().ToLayoutStyle();
            MathLayoutStyle history = MathDisplayStyle.ForHistoryLine().ToLayoutStyle();

            Assert.Equal(36, input.FontSizePx);
            Assert.Equal(18, history.FontSizePx);

            foreach (PropertyInfo property in typeof(MathLayoutStyle).GetProperties())
            {
                if (property.Name == nameof(MathLayoutStyle.FontSizePx)) continue;

                Assert.Equal(property.GetValue(history), property.GetValue(input));
            }
        }
    }
}
