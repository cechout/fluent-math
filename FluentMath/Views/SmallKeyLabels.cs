using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace FluentMath.Views
{
    // draws the key labels of a keypad smaller once the keypad is shorter than a threshold, whatever made it
    // short; one per page, like PadEntrance
    //
    // the sizes come from the markup, read on the first switch, and are written back as local values
    internal sealed class SmallKeyLabels
    {
        private readonly Panel _keypad;
        private readonly double _belowHeight;
        private readonly double _scale;

        private bool _isSmall;

        // every label with its markup size; filled on the first switch
        private List<(DependencyObject Label, DependencyProperty Property, double Size)>? _labels;

        // the keypad handler keeps the instance alive
        public static void Attach(Panel keypad, double belowHeight, double scale)
        {
            _ = new SmallKeyLabels(keypad, belowHeight, scale);
        }

        // the keypad height comes from its row, not its keys, so a label switch never feeds back
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

        // the text or glyph of every key, and the letters of a stacked key like the fraction
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
