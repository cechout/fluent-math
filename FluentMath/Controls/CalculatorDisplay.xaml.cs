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
    // the calculator display:
    // the previous calculation over the formula being typed, both a MathPanel;
    // neither can be bound to, so the tokens are pushed in from the property change
    public sealed partial class CalculatorDisplay : UserControl
    {
        // === fields ===

        // rebuilt on every theme change
        private MathLayoutStyle _historyStyle;
        private MathLayoutStyle _inputStyle;

        private CalculatorViewModel _viewModel;

        // set by the page through x:Bind; (never unsubscribed, the page, control and ViewModel live together)
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

        // in px; set once by the page in its constructor, read on every Loaded
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
            // ActualTheme only answers once the control is in the tree
            RebuildStyles();
        }


        // === clicking into the formula ===

        // a tap moves the caret to the nearest place it could stand; an unusable address changes nothing
        // (the point is relative to the panel, the space the boxes were laid out in, scroll offset and all)
        private void InputDisplay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string address = MathDisplay2.AddressAt(e.GetPosition(MathDisplay2));
            if (address == null) return;

            ViewModel.PlaceCursor(address);
        }


        // === caret preview ===

        // hovering the input line shows where a click would leave the caret
        //
        // on the scroller like the tap, so the air beside a short formula counts too; through AddHandler
        // with handledEventsToo, since the ScrollViewer marks pointer input handled
        private bool _isPreviewPressed;

        private void HookCaretPreview()
        {
            InputScroller.AddHandler(PointerMovedEvent, new PointerEventHandler(InputDisplay_PointerMoved), true);
            InputScroller.AddHandler(PointerPressedEvent, new PointerEventHandler(InputDisplay_PointerPressed), true);
            InputScroller.AddHandler(PointerReleasedEvent, new PointerEventHandler(InputDisplay_PointerReleased), true);
            InputScroller.AddHandler(PointerExitedEvent, new PointerEventHandler(InputDisplay_PointerLeft), true);
            InputScroller.AddHandler(PointerCanceledEvent, new PointerEventHandler(InputDisplay_PointerLeft), true);
        }

        // no preview for touch; a finger would drag it along
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

        // a theme change is a rebuild of the styles and a redraw
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

            // read again on every way back, so a settings change is in force
            foreach (MathLayoutStyle style in new[] { _historyStyle, _inputStyle })
            {
                style.DecimalMark = App.Settings.DecimalMarkText;
                style.GroupDigits = App.Settings.GroupDigits;
            }

            // the panels redraw with the new knobs; (their colors are ThemeResources in the markup)
            MathDisplay1.LayoutStyle = _historyStyle;
            MathDisplay2.LayoutStyle = _inputStyle;

            MathDisplay1.Show(ViewModel.CalculationTokens);
            ShowInputLine();

            // the evaluator has to agree here; it decides the shape a result comes back in
            ViewModel.UseDisplayFractions = _inputStyle.UseDisplayFractions;
        }

        // === rendering ===

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

        // an error message is plain text and skips the layout
        private void ShowInputLine()
        {
            // set before the redraw, which works the preview and the caret room out from it
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

        // keeps the caret in sight once the formula runs wider than the display
        private void RevealCaret()
        {
            // the layout pass first, or the caret rect is still the one from the keystroke before
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
