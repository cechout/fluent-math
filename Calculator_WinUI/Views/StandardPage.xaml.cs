using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using Calculator_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Calculator_WinUI.Views
{
    // the calculator display; everything else on the page is buttons bound straight to the ViewModel
    //
    // XAML has no way to draw a stacked fraction or a real root sign, so the formula is rendered by KaTeX
    // in a WebView2 instead of by a TextBlock; the ViewModel already produces LaTeX, this page only pushes
    // it into the browser
    public sealed partial class StandardPage : Page
    {
        public StandardViewModel ViewModel { get; }

        // rebuilt on every theme change, so they are fields rather than locals in the load handler
        private MathDisplayStyle _historyStyle;
        private MathDisplayStyle _inputStyle;

        // the source of the accent color the caret uses; held in a field rather than created where it
        // is needed, because a collected UISettings silently stops raising ColorValuesChanged
        private readonly UISettings _uiSettings = new UISettings();



        // === constructor ===

        public StandardPage()
        {
            ViewModel = new StandardViewModel();
            this.InitializeComponent();

            RebuildStyles();

            this.Loaded += StandardPage_Loaded;
            this.Unloaded += StandardPage_Unloaded;
            this.ActualThemeChanged += StandardPage_ActualThemeChanged;
            _uiSettings.ColorValuesChanged += StandardPage_ColorValuesChanged;
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
        private void MathDisplay2_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string address = MathDisplay2.AddressAt(e.GetPosition(MathDisplay2));
            if (address == null) return;

            ViewModel.PlaceCursor(address);
        }


        // the settings page can leave and come back, so the subscription on the shared UISettings has
        // to go with the page that made it
        private void StandardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _uiSettings.ColorValuesChanged -= StandardPage_ColorValuesChanged;
        }




        // === extra functions flyout ===

        // the keys in the flyout carry their own Command, so this only closes the flyout behind them;
        // without it the panel would stay open over the display after every function
        private void ExtraFunction_Click(object sender, RoutedEventArgs e)
        {
            ExtraFunctionsFlyout.Hide();
        }


        // === theming ===

        private void StandardPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            PushStyles();
        }

        // the caret follows the Windows accent, which can be changed while the app is running
        //
        // the event arrives on a background thread, so everything it touches has to be marshalled back
        // first
        private void StandardPage_ColorValuesChanged(UISettings sender, object args)
        {
            DispatcherQueue.TryEnqueue(PushStyles);
        }

        // both display lines take their numbers rather than a css block, so a theme change is a rebuild
        // of the styles and a redraw
        private void PushStyles()
        {
            RebuildStyles();
        }

        private void RebuildStyles()
        {
            _historyStyle = MathDisplayStyle.ForHistoryLine(this.ActualTheme);
            _inputStyle = MathDisplayStyle.ForInputLine(this.ActualTheme);

            // the panels take the knobs as numbers and redraw with them; their colors come from the
            // ThemeResources in the markup, which re-resolve themselves on a theme change
            MathDisplay1.LayoutStyle = _historyStyle.ToLayoutStyle();
            MathDisplay2.LayoutStyle = _inputStyle.ToLayoutStyle();

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
            if (MathDisplay2.CaretViewport is not Rect caret) return;

            MathDisplay2.UpdateLayout();

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
