using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FluentMath.Views
{
    // the standard calculator: the display and the ViewModel of the scientific page over the keys a pocket
    // calculator prints; it has no panels and no shift, so there is nothing here but the way into view
    public sealed partial class StandardPage : Page
    {
        public CalculatorViewModel ViewModel { get; }

        // --- display ---
        // font sizes in pixels; the scientific page has its own pair
        private const double InputLineFontSize = 30; // the lower line, the formula being typed and then its result (bigger = larger)
        private const double HistoryLineFontSize = 16; // the upper line, the calculation that gave the result (bigger = larger)


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();

            Display.InputFontSize = InputLineFontSize;
            Display.HistoryFontSize = HistoryLineFontSize;

            this.Loaded += StandardPage_Loaded;
        }


        // === navigation ===

        // Loaded rather than OnNavigatedTo, since a cached page is only back in the tree by then; it fires
        // on every way in, the first one included
        private void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }

        // cached like the scientific page, so the decimal key reads its label again on the way back
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RefreshSettingLabels();
        }
    }
}
