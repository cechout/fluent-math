using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FluentMath.Views
{
    public sealed partial class CurrencyPage : Page, ICompactPage
    {
        public CurrencyViewModel ViewModel { get; }

        // --- compact mode ---
        // sizes in pixels; the floors hold while compact, outside it the page keeps the ones in the markup
        // this page has no display block, its display is the two amount lines with a currency picker over each
        // and the rate line under them, so the display floor is the floor of each amount line; the pickers and
        // the rate line keep their own height
        private const double CompactStartWidth = 320; // the first compact window of a session, title bar included (bigger = wider)
        private const double CompactStartHeight = 460; // (bigger = taller)
        private const double CompactMinWidth = 200; // how narrow the page can be dragged (smaller = narrower floor)
        private const double CompactAmountFloor = 40; // each of the two amount lines (smaller = shorter lines allowed)
        private const double CompactPadFloor = 150; // the number pad, five rows of keys (smaller = shorter keys allowed)

        private readonly double _amountFloor;
        private readonly double _padFloor;

        public CurrencyPage()
        {
            this.InitializeComponent();
            ViewModel = new CurrencyViewModel();

            _amountFloor = AmountRow1.MinHeight;
            _padFloor = PadRow.MinHeight;

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


        // === compact mode ===

        // the window owns compact mode, see MainWindow; this page only asks for it and hands back its header
        // while it lasts, since the title bar carries the way back
        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.EnterCompactMode();
        }

        public Size CompactStartSize => new Size(CompactStartWidth, CompactStartHeight);

        // the floors, the pickers and the rate line as they are laid out, the six row gaps and the 8 of page
        // margin
        public Size CompactMinSize => new Size(CompactMinWidth,
            PickerRow1.ActualHeight + PickerRow2.ActualHeight + RateRow.ActualHeight
            + (2 * CompactAmountFloor) + CompactPadFloor + (6 * RootGrid.RowSpacing) + 8);

        public void SetCompactLayout(bool compact)
        {
            Header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            AmountRow1.MinHeight = compact ? CompactAmountFloor : _amountFloor;
            AmountRow2.MinHeight = compact ? CompactAmountFloor : _amountFloor;
            PadRow.MinHeight = compact ? CompactPadFloor : _padFloor;
        }
    }
}
