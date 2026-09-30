using FluentMath.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace FluentMath.Views
{
    // the scientific calculator:
    // every key binds straight to the ViewModel; what is left here is the panel bar and compact mode
    public sealed partial class ScientificPage : Page, ICompactPage
    {
        public CalculatorViewModel ViewModel { get; }

        // --- display ---
        // in px; (the standard page has its own pair)
        private const double InputLineFontSize = 30; // the lower line; input
        private const double HistoryLineFontSize = 16; // the upper line; output

        // --- row floors ---
        // in px; also how far the splitter goes; (the full window and compact mode each have their own pair)
        private const double DisplayFloor = 96; // the display with the caret bar under it
        private const double PadFloor = 252; // the panel bar over the keypad of six rows
        private const double CompactDisplayFloor = 96;
        private const double CompactPadFloor = 220;
        private const double PushBuffer = 4; // room the pad keeps over its floor when it pushes the splitter

        // --- compact mode ---
        // in px
        private const double CompactStartWidth = 340;
        private const double CompactStartHeight = 520;
        private const double CompactMinWidth = 220; // holds the caret bar

        // --- key labels ---
        // key font drops to smaller size once the keypad is shorter than this; (the panel bar keeps its size)
        private const double SmallKeysBelowHeight = 205; // keypad height in pixels
        private const double SmallKeyTextScale = 0.8;


        // === constructor ===

        public ScientificPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();

            Display.InputFontSize = InputLineFontSize;
            Display.HistoryFontSize = HistoryLineFontSize;

            DisplayRow.MinHeight = DisplayFloor;
            PadRow.MinHeight = PadFloor;
            SmallKeyLabels.Attach(Keypad, SmallKeysBelowHeight, SmallKeyTextScale);

            ApplyPanelBarFade();
            AccentPanelButtonsWhileOpen();

            this.Loaded += ScientificPage_Loaded;
        }


        // === navigation ===

        // Loaded, not OnNavigatedTo; a cached page is only back in the tree by then
        private void ScientificPage_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }

        // the page is cached, so the labels the settings can change are read again on every way back
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RefreshSettingLabels();
        }


        // === compact mode ===

        // MainWindow owns compact mode; the page only asks for it and lays itself out
        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.EnterCompactMode();
        }

        public Size CompactStartSize => new Size(CompactStartWidth, CompactStartHeight);

        // both floors, the divider row and 8 of margins
        public Size CompactMinSize => new Size(CompactMinWidth,
            CompactDisplayFloor + DividerRow.ActualHeight + CompactPadFloor + 8);

        public void SetCompactLayout(bool compact)
        {
            Header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            DisplayRow.MinHeight = compact ? CompactDisplayFloor : DisplayFloor;
            PadRow.MinHeight = compact ? CompactPadFloor : PadFloor;

            InvalidateMeasure();
        }


        // === display height ===

        // a window too short for the pad pushes the splitter up, where it stays until dragged
        // (in the measure; a Grid that does not fit is clipped, so its SizeChanged never reports the squeeze)
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!double.IsInfinity(availableSize.Height))
            {
                double header = Header.Visibility == Visibility.Visible ? Header.Height : 0;
                double divider = DividerLine.Height + DividerLine.Margin.Top + DividerLine.Margin.Bottom;
                double room = availableSize.Height - RootGrid.Margin.Top - RootGrid.Margin.Bottom
                    - header - divider - PadRow.MinHeight - PushBuffer;

                if (DisplayRow.Height.Value > room)
                {
                    DisplayRow.Height = new GridLength(Math.Max(DisplayRow.MinHeight, room));
                }
            }

            return base.MeasureOverride(availableSize);
        }


        // === panel bar ===

        // the share of the viewport one chevron click scrolls
        private const double PanelScrollRatio = 0.7;

        private void PanelScroller_Loaded(object sender, RoutedEventArgs e)
        {
            // the strip has no extent before its first pass, so both chevrons would read as not needed
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

        // both edges read with a pixel of slack; an offset often lands a fraction short of its end
        private void UpdatePanelChevrons()
        {
            double offset = PanelScroller.HorizontalOffset;

            bool left = offset > 1;
            bool right = offset < PanelScroller.ScrollableWidth - 1;

            PanelScrollLeft.Visibility = left ? Visibility.Visible : Visibility.Collapsed;
            PanelScrollRight.Visibility = right ? Visibility.Visible : Visibility.Collapsed;

            UpdatePanelFade(left, right);
        }

        private void ScrollPanelBar(int direction)
        {
            double step = PanelScroller.ViewportWidth * PanelScrollRatio;

            PanelScroller.ChangeView(PanelScroller.HorizontalOffset + (direction * step), null, null);
        }


        // === panel bar fade ===

        // in px; how far in from the edge the strip is back at full strength, chevron included
        private const double PanelFadeWidth = 44;

        // (a mask reads only alpha)
        private static readonly Color MaskKeep = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color MaskDrop = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);

        // fades the strip out into the window edge without painting over it, so the Mica still shows through
        //
        // one visual surface captures the scroller, a second the gradient Rectangle, and a mask brush takes
        // the alpha of the second for the first; a sprite over an empty host draws the result
        // (the shape of the Toolkit Labs OpacityMaskView)
        private void ApplyPanelBarFade()
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(PanelStripFade).Compositor;

            CompositionMaskBrush mask = compositor.CreateMaskBrush();
            mask.Source = RedirectOf(PanelScroller);
            mask.Mask = RedirectOf(PanelStripMask);

            // sized off the scroller, not the host, so no rounding pixel scales the copy
            SpriteVisual sprite = compositor.CreateSpriteVisual();
            sprite.Brush = mask;
            sprite.StartAnimation(nameof(sprite.Size), SizeOf(PanelScroller));

            ElementCompositionPreview.SetElementChildVisual(PanelStripFade, sprite);
        }

        // captures the element and hides it through its visual, not UIElement.Opacity; the capture ignores
        // the visual opacity and hit testing reads only UIElement.Opacity, so the strip stays clickable
        //
        // the surface size is bound to the visual, so it follows a window resize
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

        // the live size of the element
        private static ExpressionAnimation SizeOf(UIElement element)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(element);

            ExpressionAnimation size = visual.Compositor.CreateExpressionAnimation("source.Size");
            size.SetReferenceParameter("source", visual);

            return size;
        }

        // places the stops from the measured viewport; a side with nothing behind it keeps them on the edge
        // at full alpha, a mask that changes nothing
        //
        // the strip is cut flat under the chevron, so no button shows through the control over it
        // (the chevron Width, not ActualWidth, which is still zero on the frame it appears)
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

        // closes the open panel after one of its keys (the key carries its own Command)
        // one handler for every panel, since hiding a closed one costs nothing; a new panel adds its flyout here
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

        // accents a button while its panel is open; the look is the FlyoutStates group of SubtleBarButtonStyle
        // (the handler closes over its button rather than reading FlyoutBase.Target, which is not ours to rely on)
        private void AccentPanelButtonsWhileOpen()
        {
            foreach (Button button in PanelStrip.Children.OfType<Button>())
            {
                if (button.Flyout == null) continue;

                button.Flyout.Opened += (_, _) => VisualStateManager.GoToState(button, "FlyoutOpen", false);
                button.Flyout.Closed += (_, _) => VisualStateManager.GoToState(button, "FlyoutClosed", false);
            }
        }

        // the two latches belong to the open panel, so they reset with it, after a key or a dismissal alike
        private void TrigonometryFlyout_Closed(object sender, object e)
        {
            ViewModel.ResetTrigLatches();
        }
    }
}
