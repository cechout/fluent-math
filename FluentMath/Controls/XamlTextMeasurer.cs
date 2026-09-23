using FluentMath.Models.Layout;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.Foundation;

namespace FluentMath.Controls
{
    // measures with the same text stack the panel draws with, so a run takes up on screen exactly what
    // the layout was told it would
    //
    // one TextBlock is reused rather than one per call, and every answer is kept: a formula asks for the
    // same handful of runs again on every keystroke, and the measurement is the expensive half of a
    // relayout
    public sealed class XamlTextMeasurer : ITextMeasurer
    {
        private readonly TextBlock _probe = new TextBlock();
        private readonly Dictionary<(string Text, double Size), TextMetrics> _cache = new();

        // upright sans digits, the way a pocket calculator draws them, and a family that carries every
        // sign the display needs: the real minus, the dot operator, the division sign and pi
        private FontFamily _fontFamily = new FontFamily("Segoe UI");

        public FontFamily FontFamily
        {
            get => _fontFamily;
            set
            {
                if (_fontFamily == value) return;

                _fontFamily = value;
                _cache.Clear();
            }
        }

        public TextMetrics Measure(string text, double fontSizePx)
        {
            if (_cache.TryGetValue((text, fontSizePx), out TextMetrics cached)) return cached;

            _probe.FontFamily = _fontFamily;
            _probe.FontSize = fontSizePx;
            _probe.Text = text;
            _probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // the split at the baseline is the whole reason this goes through a TextBlock rather than
            // through a font table: BaselineOffset is measured in the same pass as the size, so the two
            // can never disagree
            Size size = _probe.DesiredSize;
            TextMetrics metrics = new TextMetrics(size.Width, _probe.BaselineOffset, size.Height - _probe.BaselineOffset);

            _cache[(text, fontSizePx)] = metrics;
            return metrics;
        }
    }
}
