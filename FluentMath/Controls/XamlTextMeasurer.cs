using FluentMath.Models.Layout;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.Foundation;

namespace FluentMath.Controls
{
    // measures with the text stack the panel draws with, so a run takes exactly what the layout was told
    // (one reused TextBlock and a cache; every keystroke asks for the same few runs again)
    public sealed class XamlTextMeasurer : ITextMeasurer
    {
        private readonly TextBlock _probe = new TextBlock();
        private readonly Dictionary<(string Text, double Size), TextMetrics> _cache = new();

        // upright sans digits, and every sign the display needs: minus, dot operator, division sign, pi
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

            // BaselineOffset comes from the same pass as the size, so the two never disagree
            Size size = _probe.DesiredSize;
            TextMetrics metrics = new TextMetrics(size.Width, _probe.BaselineOffset, size.Height - _probe.BaselineOffset);

            _cache[(text, fontSizePx)] = metrics;
            return metrics;
        }
    }
}
