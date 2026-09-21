using Calculator_WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Calculator_WinUI.Views
{
    public sealed partial class CurrencyPage : Page
    {
        public CurrencyViewModel ViewModel { get; }

        public CurrencyPage()
        {
            this.InitializeComponent();
            ViewModel = new CurrencyViewModel();
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
