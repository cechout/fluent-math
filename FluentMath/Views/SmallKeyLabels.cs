using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace FluentMath.Views
{
    // draws the labels of a keypad smaller once the keypad itself is too short for them, whatever made it
    // short: a small window, compact mode, or the splitter on the scientific page; one per page with a
    // keypad, each with its own threshold and scale, the way PadEntrance serves every pad
    //
    // the sizes come from the markup, read once on the first switch, and every switch writes them back as
    // local values; the display needs nothing of the kind, since it scales its formula down to the height it
    // gets
    internal sealed class SmallKeyLabels
    {
        private readonly Panel _keypad;
        private readonly double _belowHeight;
        private readonly double _scale;

        private bool _isSmall;

        // every label on the keypad with the size the markup gave it; filled on the first switch
        private List<(DependencyObject Label, DependencyProperty Property, double Size)>? _labels;

        // the handler on the keypad is what keeps the instance alive, so a page only attaches it
        public static void Attach(Panel keypad, double belowHeight, double scale)
        {
            _ = new SmallKeyLabels(keypad, belowHeight, scale);
        }

        // the keypad takes its height from the row it sits in and not from its keys, so the labels changing
        // size never feeds back into the height that decided it
        private SmallKeyLabels(Panel keypad, double belowHeight, double scale)
        {
            _keypad = keypad;
            _belowHeight = belowHeight;
            _scale = scale;

            _keypad.SizeChanged += Keypad_SizeChanged;
        }

        private void Keypad_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool small = e.NewSize.Height < _belowHeight;
            if (small == _isSmall) return;

            _isSmall = small;
            _labels ??= CollectLabels();

            double scale = small ? _scale : 1;
            foreach ((DependencyObject label, DependencyProperty property, double size) in _labels)
            {
                label.SetValue(property, size * scale);
            }
        }

        // the text or glyph on every key, and the letters of a key that stacks them in a small grid, like the
        // fraction key
        private List<(DependencyObject Label, DependencyProperty Property, double Size)> CollectLabels()
        {
            var labels = new List<(DependencyObject Label, DependencyProperty Property, double Size)>();

            foreach (Button key in _keypad.Children.OfType<Button>())
            {
                CollectLabels(key.Content, labels);
            }

            return labels;
        }

        private static void CollectLabels(object content, List<(DependencyObject Label, DependencyProperty Property, double Size)> labels)
        {
            switch (content)
            {
                case TextBlock text:
                    labels.Add((text, TextBlock.FontSizeProperty, text.FontSize));
                    break;

                case FontIcon icon:
                    labels.Add((icon, FontIcon.FontSizeProperty, icon.FontSize));
                    break;

                case Panel panel:
                    foreach (UIElement child in panel.Children)
                    {
                        CollectLabels(child, labels);
                    }
                    break;
            }
        }
    }
}
