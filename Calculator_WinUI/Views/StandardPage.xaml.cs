using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using Calculator_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using Windows.Foundation;

namespace Calculator_WinUI.Views
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
            ViewModel = new StandardViewModel();
            this.InitializeComponent();

            RebuildStyles();

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




        // === panel bar ===

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
