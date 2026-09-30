using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.Foundation;

namespace FluentMath.Views
{
    // the pad entrance:
    // the page switches without a transition and its pad grows in from a little under full size
    // (the Windows Calculator mode switch, which starts from 0.92)
    internal static class PadEntrance
    {
        // --- pad entrance ---
        private const double StartScale = 0.9; // the share of full size it starts at; (1 = no growth)
        private static readonly TimeSpan GrowTime = TimeSpan.FromMilliseconds(367);
        private const double EaseExponent = 5; // (higher = more of the growth in the first frames)

        // scales around the middle, so the pad grows toward all four edges
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
