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


        // hide flyouts when a currency is selected
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
