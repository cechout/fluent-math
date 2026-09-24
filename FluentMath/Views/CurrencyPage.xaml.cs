using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentMath.Views
{
    public sealed partial class CurrencyPage : Page
    {
        public CurrencyViewModel ViewModel { get; }

        public CurrencyPage()
        {
            this.InitializeComponent();
            ViewModel = new CurrencyViewModel();

            this.Loaded += CurrencyPage_Loaded;
        }


        // the number pad grows in the way it does on the two calculators; Loaded rather than
        // OnNavigatedTo, since the page is cached and only back in the tree by then
        private void CurrencyPage_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }


        // a Flyout stays open after a ListView selection, so both currency pickers have to be closed
        // by hand to feel like a normal dropdown
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FlyoutCurrency1.Hide();
        }

        private void ListView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FlyoutCurrency2.Hide();
        }
    }
}
