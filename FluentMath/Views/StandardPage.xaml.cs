using FluentMath.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FluentMath.Views
{
    // the standard calculator: the display and the ViewModel of the scientific page over the keys a pocket
    // calculator prints; it has no panels and no shift, so there is nothing here but the way back into view
    public sealed partial class StandardPage : Page
    {
        public CalculatorViewModel ViewModel { get; }


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();
        }


        // === navigation ===

        // cached like the scientific page, so the decimal key reads its label again on the way back
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RefreshSettingLabels();
        }
    }
}
