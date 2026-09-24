using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.Foundation;

namespace FluentMath.Views
{
    // how a page with a pad comes into view, the two calculators and the currency converter: the page
    // itself switches without a transition, and the pad grows in from a little under its full size
    //
    // the shape is the one the Windows Calculator plays on its keypad when it changes mode, the same
    // duration and the same exponential ease out; it starts from 0.92 there
    internal static class PadEntrance
    {
        // --- pad entrance ---
        private const double StartScale = 0.9; // share of its full size the pad starts at (1 = no growth)
        private static readonly TimeSpan GrowTime = TimeSpan.FromMilliseconds(367); // how long the growth takes
        private const double EaseExponent = 5; // how hard the growth front loads (higher = more of it in the first frames)

        // scales around the middle of the pad, so it grows toward all four edges at once
        public static void Play(UIElement pad)
        {
            var scale = new ScaleTransform { ScaleX = StartScale, ScaleY = StartScale };
            pad.RenderTransformOrigin = new Point(0.5, 0.5);
            pad.RenderTransform = scale;

            var storyboard = new Storyboard();
            storyboard.Children.Add(Grow(scale, nameof(ScaleTransform.ScaleX)));
            storyboard.Children.Add(Grow(scale, nameof(ScaleTransform.ScaleY)));
            storyboard.Begin();
        }

        private static DoubleAnimation Grow(ScaleTransform scale, string property)
        {
            var animation = new DoubleAnimation
            {
                From = StartScale,
                To = 1,
                Duration = new Duration(GrowTime),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = EaseExponent }
            };

            Storyboard.SetTarget(animation, scale);
            Storyboard.SetTargetProperty(animation, property);

            return animation;
        }
    }
}
