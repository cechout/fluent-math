using FluentMath.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Windows.UI;

namespace FluentMath.Views
{
    // the scientific calculator; the display is a CalculatorDisplay and every key is bound straight to the
    // ViewModel, so what is left here is the panel bar
    public sealed partial class ScientificPage : Page
    {
        public CalculatorViewModel ViewModel { get; }


        // === constructor ===

        public ScientificPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();

            ApplyPanelBarFade();
            AccentPanelButtonsWhileOpen();

            this.Loaded += ScientificPage_Loaded;
        }


        // === navigation ===

        // Loaded rather than OnNavigatedTo, since a cached page is only back in the tree by then; it fires
        // on every way in, the first one included
        private void ScientificPage_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }

        // the page is cached and comes back with whatever it was left with, so the two labels the settings
        // page can change are read again here; the display lines do the same when they are loaded
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RefreshSettingLabels();
        }


        // === panel bar ===

        // the bar holds more category buttons than any window width fits, so the side that runs off
        // the edge gets a chevron; one click moves the strip by most of a viewport rather than by a
        // button, which is what puts the far end of the bar two clicks away
        private const double PanelScrollRatio = 0.7;

        private void PanelScroller_Loaded(object sender, RoutedEventArgs e)
        {
            // the strip is in the tree by now but has not been through a pass, so its extent is
            // still zero and both chevrons would read as not needed; the same ordering the caret
            // reveal needs, and for the same reason
            PanelScroller.UpdateLayout();

            UpdatePanelChevrons();
        }

        private void PanelScroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePanelChevrons();
        }

        private void PanelScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdatePanelChevrons();
        }

        private void PanelScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            ScrollPanelBar(-1);
        }

        private void PanelScrollRight_Click(object sender, RoutedEventArgs e)
        {
            ScrollPanelBar(1);
        }

        // an offset lands a fraction of a pixel short of its end often enough that both edges are
        // read with a pixel of slack; ScrollableWidth is zero while the whole bar fits, which is
        // what collapses both chevrons through the same two comparisons
        private void UpdatePanelChevrons()
        {
            double offset = PanelScroller.HorizontalOffset;

            bool left = offset > 1;
            bool right = offset < PanelScroller.ScrollableWidth - 1;

            PanelScrollLeft.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
            PanelScrollRight.Visibility = right ? Visibility.Visible : Visibility.Collapsed;

            // a chevron and the fade under it answer the same question, so they are decided together
            UpdatePanelFade(left, right);
        }

        private void ScrollPanelBar(int direction)
        {
            double step = PanelScroller.ViewportWidth * PanelScrollRatio;

            PanelScroller.ChangeView(PanelScroller.HorizontalOffset + (direction * step), null, null);
        }


        // === panel bar fade ===

        // how far in from the edge the strip is back at full strength, chevron included, and what
        // is left of it out there; a mask reads nothing but alpha, so the color is white throughout
        // and only the alpha carries the ramp
        private const double PanelFadeWidth = 44;

        private static readonly Color MaskKeep = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color MaskDrop = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);

        // the strip fades out into the window edge, and nothing is painted over it to do that: a
        // veil would be a color of its own over the backdrop, and the Mica would stop showing
        // through exactly where the bar runs out
        //
        // so the strip is redirected rather than covered. One CompositionVisualSurface captures the
        // scrollers rendering, a second captures a Rectangle that is nothing but a gradient, and a
        // CompositionMaskBrush takes the alpha of the second as the alpha of the first; a sprite
        // over an empty host in the same place draws the result
        //
        // this is the shape of the Toolkit Labs OpacityMaskView, which is also what the WinUI
        // Gallery ships its own opacity mask sample on
        private void ApplyPanelBarFade()
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(PanelStripFade).Compositor;

            CompositionMaskBrush mask = compositor.CreateMaskBrush();
            mask.Source = RedirectOf(PanelScroller);
            mask.Mask = RedirectOf(PanelStripMask);

            // the sprite is sized off the scroller rather than off the host it hangs on, so a copy
            // can never end up scaled against the original by a pixel of rounding somewhere
            SpriteVisual sprite = compositor.CreateSpriteVisual();
            sprite.Brush = mask;
            sprite.StartAnimation(nameof(sprite.Size), SizeOf(PanelScroller));

            ElementCompositionPreview.SetElementChildVisual(PanelStripFade, sprite);
        }

        // the element is hidden through its composition visual and not through UIElement.Opacity,
        // and that difference is the whole trick: a visual surface renders the subtree without the
        // source visuals own opacity, so the capture survives while the original stops drawing, and
        // XAML hit testing reads UIElement.Opacity rather than the visual, so every button in the
        // strip stays clickable while a sprite is what is actually on screen
        //
        // the surface carries its own size rather than inheriting one, so it is bound to the visual
        // instead of read once and left behind by the next window resize
        private static CompositionBrush RedirectOf(UIElement element)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(element);
            Compositor compositor = visual.Compositor;

            CompositionVisualSurface surface = compositor.CreateVisualSurface();
            surface.SourceVisual = visual;
            surface.StartAnimation(nameof(surface.SourceSize), SizeOf(element));

            visual.Opacity = 0;

            return compositor.CreateSurfaceBrush(surface);
        }

        // a Vector2 expression that stays on the elements measured size for as long as it runs
        private static ExpressionAnimation SizeOf(UIElement element)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(element);

            ExpressionAnimation size = visual.Compositor.CreateExpressionAnimation("source.Size");
            size.SetReferenceParameter("source", visual);

            return size;
        }

        // the ramp is a share of the width and the width is not fixed, so every stop is placed from
        // the measured viewport; a side with nothing behind it keeps its stops on the edge and at
        // full alpha, which is a mask that changes nothing
        //
        // the cut is where the strip ends: the outer stop is held flat from the edge to the inner
        // side of the chevron, so a category button never passes under the one control covering it
        // the chevron is asked for its own width rather than the number being repeated here, and
        // Width answers where ActualWidth does not, since the button has not been through a layout
        // pass yet on the frame it appears
        private void UpdatePanelFade(bool fadeLeft, bool fadeRight)
        {
            double width = PanelScroller.ActualWidth;
            double ramp = width > 0 ? Math.Min(0.5, PanelFadeWidth / width) : 0;

            double leftCut = width > 0 ? Math.Min(ramp, PanelScrollLeft.Width / width) : 0;
            double rightCut = width > 0 ? Math.Min(ramp, PanelScrollRight.Width / width) : 0;

            PanelFadeLeftOuter.Color = fadeLeft ? MaskDrop : MaskKeep;
            PanelFadeLeftCut.Color = fadeLeft ? MaskDrop : MaskKeep;
            PanelFadeLeftCut.Offset = fadeLeft ? leftCut : 0;
            PanelFadeLeftInner.Offset = fadeLeft ? ramp : 0;

            PanelFadeRightInner.Offset = fadeRight ? 1 - ramp : 1;
            PanelFadeRightCut.Offset = fadeRight ? 1 - rightCut : 1;
            PanelFadeRightCut.Color = fadeRight ? MaskDrop : MaskKeep;
            PanelFadeRightOuter.Color = fadeRight ? MaskDrop : MaskKeep;
        }

        // the keys in both panels carry their own Command, so this only closes the panel behind them;
        // without it it would stay open over the keypad after every function
        //
        // one handler for all of them, because a key only ever sits in the panel that is open and
        // hiding the others costs nothing; a new panel adds its flyout here
        private void FlyoutKey_Click(object sender, RoutedEventArgs e)
        {
            TrigonometryFlyout.Hide();
            FunctionFlyout.Hide();
            CalculusFlyout.Hide();
            NumberTheoryFlyout.Hide();
            ProbabilityFlyout.Hide();
            CoordinatesFlyout.Hide();
            PrefixesFlyout.Hide();
        }

        // a button whose panel is open reads as the accent color, which is the only thing that says
        // which of the seven is showing; a Button raises nothing for it, so the state is entered from
        // here and the look of it lives in the FlyoutStates group of SubtleBarButtonStyle
        //
        // the handler closes over the button it belongs to rather than reading FlyoutBase.Target,
        // which the framework fills in when it shows an attached flyout and is not ours to lean on
        private void AccentPanelButtonsWhileOpen()
        {
            foreach (Button button in PanelStrip.Children.OfType<Button>())
            {
                if (button.Flyout == null) continue;

                button.Flyout.Opened += (_, _) => VisualStateManager.GoToState(button, "FlyoutOpen", false);
                button.Flyout.Closed += (_, _) => VisualStateManager.GoToState(button, "FlyoutClosed", false);
            }
        }

        // the two latches belong to the open panel and not to the app, so they come back to the plain
        // grid with it; this fires for a function key and for a dismissal alike, since the key hides the
        // panel rather than resetting anything itself
        private void TrigonometryFlyout_Closed(object sender, object e)
        {
            ViewModel.ResetTrigLatches();
        }
    }
}
