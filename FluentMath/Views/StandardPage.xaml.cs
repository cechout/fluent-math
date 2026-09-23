using FluentMath.Models;
using FluentMath.Models.Layout;
using FluentMath.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace FluentMath.Views
{
    // the calculator display; everything else on the page is buttons bound straight to the ViewModel
    //
    // both display lines are a MathPanel, which draws the formula out of ordinary XAML elements; neither
    // can be bound to, so this page pushes the tokens into them from the property change and otherwise
    // stays out of the way
    public sealed partial class StandardPage : Page
    {
        public StandardViewModel ViewModel { get; }

        // rebuilt on every theme change, so they are fields rather than locals in the load handler
        private MathLayoutStyle _historyStyle;
        private MathLayoutStyle _inputStyle;


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new StandardViewModel(App.Settings);
            this.InitializeComponent();

            RebuildStyles();
            ApplyPanelBarFade();
            AccentPanelButtonsWhileOpen();
            HookCaretPreview();

            this.Loaded += StandardPage_Loaded;
            this.ActualThemeChanged += StandardPage_ActualThemeChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        // === lifecycle ===

        private void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // the page sits in the tree by now, so ActualTheme finally answers with the theme in force
            RebuildStyles();
        }


        // === clicking into the formula ===

        // a tap moves the caret to the nearest place it could stand
        //
        // an address the input manager cannot use simply changes nothing, which is what makes this safe
        // to answer with the nearest position rather than only with an exact hit
        //
        // the point is taken relative to the panel even though the tap arrives at the scroller around it,
        // which is what puts it in the space the boxes were laid out in, scroll offset and all
        private void InputDisplay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string address = MathDisplay2.AddressAt(e.GetPosition(MathDisplay2));
            if (address == null) return;

            ViewModel.PlaceCursor(address);
        }


        // === caret preview ===

        // hovering the input line shows where a click would leave the caret
        //
        // the handlers sit on the scroller and not on the panel, exactly the way the tap does: the panel
        // is only as wide as the formula, and the line is the whole strip, so aiming at the air beside a
        // short formula has to count for the preview as much as it does for the click
        //
        // they are hooked through AddHandler with handledEventsToo rather than named in the markup,
        // because a ScrollViewer marks pointer input handled for its own manipulation and a handler in
        // the markup would never run
        private bool _isPreviewPressed;

        private void HookCaretPreview()
        {
            InputScroller.AddHandler(PointerMovedEvent, new PointerEventHandler(InputDisplay_PointerMoved), true);
            InputScroller.AddHandler(PointerPressedEvent, new PointerEventHandler(InputDisplay_PointerPressed), true);
            InputScroller.AddHandler(PointerReleasedEvent, new PointerEventHandler(InputDisplay_PointerReleased), true);
            InputScroller.AddHandler(PointerExitedEvent, new PointerEventHandler(InputDisplay_PointerLeft), true);
            InputScroller.AddHandler(PointerCanceledEvent, new PointerEventHandler(InputDisplay_PointerLeft), true);
        }

        // a finger has no hover: it would drag a preview along behind it, so only the two devices that
        // can point at something without pressing it get one
        private static bool Hovers(PointerRoutedEventArgs e)
        {
            return e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch;
        }

        private void InputDisplay_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!Hovers(e)) return;

            MathDisplay2.ShowPreviewCaret(e.GetCurrentPoint(MathDisplay2).Position, _isPreviewPressed);
        }

        private void InputDisplay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!Hovers(e)) return;

            _isPreviewPressed = true;
            MathDisplay2.ShowPreviewCaret(e.GetCurrentPoint(MathDisplay2).Position, true);
        }

        private void InputDisplay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!Hovers(e)) return;

            _isPreviewPressed = false;
            MathDisplay2.ShowPreviewCaret(e.GetCurrentPoint(MathDisplay2).Position, false);
        }

        private void InputDisplay_PointerLeft(object sender, PointerRoutedEventArgs e)
        {
            _isPreviewPressed = false;
            MathDisplay2.HidePreviewCaret();
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
            NumberTheoryFlyout.Hide();
            ProbabilityFlyout.Hide();
            CoordinatesFlyout.Hide();
            PrefixesFlyout.Hide();
        }

        // a button whose panel is open reads as the accent color, which is the only thing that says
        // which of the six is showing; a Button raises nothing for it, so the state is entered from
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


        // === theming ===

        private void StandardPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            PushStyles();
        }

        // both display lines take their numbers rather than a css block, so a theme change is a rebuild
        // of the styles and a redraw
        private void PushStyles()
        {
            RebuildStyles();
        }

        private void RebuildStyles()
        {
            _historyStyle = MathLayoutStyle.ForHistoryLine();
            _inputStyle = MathLayoutStyle.ForInputLine();

            // the page is built anew on every navigation, so a change on the settings page is in force
            // by the time these are read
            foreach (MathLayoutStyle style in new[] { _historyStyle, _inputStyle })
            {
                style.DecimalMark = App.Settings.DecimalMarkText;
                style.GroupDigits = App.Settings.GroupDigits;
            }

            // the panels take the knobs as numbers and redraw with them; their colors come from the
            // ThemeResources in the markup, which re-resolve themselves on a theme change
            MathDisplay1.LayoutStyle = _historyStyle;
            MathDisplay2.LayoutStyle = _inputStyle;

            MathDisplay1.Show(ViewModel.CalculationTokens);
            ShowInputLine();

            // the evaluator has to agree with the display on this one, since it decides the shape a
            // result comes back in rather than only how it is drawn
            ViewModel.UseDisplayFractions = _inputStyle.UseDisplayFractions;
        }

        // === rendering ===

        // neither display line can be bound to, so both are redrawn by hand from the property change
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CalculationTokens))
            {
                MathDisplay1.Show(ViewModel.CalculationTokens);
            }
            else if (e.PropertyName == nameof(ViewModel.InputTokens))
            {
                ShowInputLine();
            }
        }

        // an error message is a line of text rather than a formula, so it does not go through the layout
        private void ShowInputLine()
        {
            // set before the redraw, because rebuilding the line is what works the preview and the
            // room kept for a caret out again; an error message is a line of text with nothing in it
            // to aim at
            MathDisplay2.CaretIsPlaceable = ViewModel.InputErrorText == null && ViewModel.CanPlaceCursor;

            if (ViewModel.InputErrorText != null)
            {
                MathDisplay2.ShowText(ViewModel.InputErrorText);
                return;
            }

            MathDisplay2.Show(ViewModel.InputTokens,
                new CaretTarget(ViewModel.CaretTokens, ViewModel.CaretIndex));

            RevealCaret();
        }

        // the formula is wider than the display as soon as it is long enough, and the caret has to stay
        // in sight while it is being typed at the far end of it
        private void RevealCaret()
        {
            // the panel has only been told to redraw at this point; without the layout pass first, the
            // caret rect read below is still the one from the keystroke before and the scroller trails
            // the caret by a character, which leaves it standing on the edge of the display
            MathDisplay2.UpdateLayout();

            if (MathDisplay2.CaretViewport is not Rect caret) return;

            double left = InputScroller.HorizontalOffset;
            double right = left + InputScroller.ViewportWidth;
            double margin = _inputStyle.FontSizePx;

            if (caret.Right + margin > right)
            {
                InputScroller.ChangeView(caret.Right + margin - InputScroller.ViewportWidth, null, null, true);
            }
            else if (caret.Left - margin < left)
            {
                InputScroller.ChangeView(Math.Max(0, caret.Left - margin), null, null, true);
            }
        }
    }
}
