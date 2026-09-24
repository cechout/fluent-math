using FluentMath.Models.Layout;
using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using Windows.Foundation;

namespace FluentMath.Controls
{
    // the two display lines of a calculator page, the previous calculation over the formula being typed
    //
    // both are a MathPanel, which draws the formula out of ordinary XAML elements; neither can be bound
    // to, so this control pushes the tokens into them from the property change and otherwise stays out
    // of the way
    public sealed partial class CalculatorDisplay : UserControl
    {
        // === fields ===

        // rebuilt on every theme change, so they are fields rather than locals in the load handler
        private MathLayoutStyle _historyStyle;
        private MathLayoutStyle _inputStyle;

        private CalculatorViewModel _viewModel;

        // handed in by the page through x:Bind, which sets it again whenever the bindings are updated; the
        // page, this control and the view model come and go together, so the one subscription is never
        // taken back
        public CalculatorViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel == value) return;

                _viewModel = value;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        // the font size of each line, set by the page so the two calculators can differ; read whenever the
        // styles are built, which is on every Loaded, so a page sets them once in its constructor
        public double InputFontSize { get; set; } = MathLayoutStyle.InputLineFontSize;
        public double HistoryFontSize { get; set; } = MathLayoutStyle.HistoryLineFontSize;


        // === constructor ===

        public CalculatorDisplay()
        {
            this.InitializeComponent();

            HookCaretPreview();

            this.Loaded += CalculatorDisplay_Loaded;
            this.ActualThemeChanged += CalculatorDisplay_ActualThemeChanged;
        }


        // === lifecycle ===

        private void CalculatorDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // the control sits in the tree by now, so ActualTheme finally answers with the theme in force
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


        // === theming ===

        private void CalculatorDisplay_ActualThemeChanged(FrameworkElement sender, object args)
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

            // the page picks the sizes; everything else in a style is in em and scales along with them
            _historyStyle.FontSizePx = HistoryFontSize;
            _inputStyle.FontSizePx = InputFontSize;

            // this runs again every time the page comes back into view, so a change on the settings page
            // is in force by the time these are read
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
