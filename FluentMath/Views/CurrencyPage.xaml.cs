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


        // each currency button opens the invisible combo box under it, which does the selecting
        private void CurrencyButton1_Click(object sender, RoutedEventArgs e)
        {
            CurrencyComboBox1.IsDropDownOpen = true;
        }

        private void CurrencyButton2_Click(object sender, RoutedEventArgs e)
        {
            CurrencyComboBox2.IsDropDownOpen = true;
        }
    }
}
