using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FluentMath.Views
{
    public sealed partial class CurrencyPage : Page, ICompactPage
    {
        public CurrencyViewModel ViewModel { get; }

        // --- row floors ---
        // in px; (the full window and compact mode each have their own pair)
        private const double AmountFloor = 40; // each of the two amount lines, the display of this page
        private const double PadFloor = 0; // the number pad, five rows of keys; 0 = no floor
        private const double CompactAmountFloor = 40;
        private const double CompactPadFloor = 150;

        // --- compact mode ---
        // sizes in px
        private const double CompactStartWidth = 320;
        private const double CompactStartHeight = 460;
        private const double CompactMinWidth = 200;

        // --- key labels ---
        // key font drops to smaller size once the number pad is shorter than this
        private const double SmallKeysBelowHeight = 170; // number pad height in pixels
        private const double SmallKeyTextScale = 0.8;

        public CurrencyPage()
        {
            this.InitializeComponent();
            ViewModel = new CurrencyViewModel();

            AmountRow1.MinHeight = AmountFloor;
            AmountRow2.MinHeight = AmountFloor;
            PadRow.MinHeight = PadFloor;
            SmallKeyLabels.Attach(Pad, SmallKeysBelowHeight, SmallKeyTextScale);

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

        // MainWindow owns compact mode; the page only asks for it and lays itself out
        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.EnterCompactMode();
        }

        public Size CompactStartSize => new Size(CompactStartWidth, CompactStartHeight);

        // the floors, the pickers and the rate line as laid out, six row gaps and 8 of margins
        public Size CompactMinSize => new Size(CompactMinWidth,
            PickerRow1.ActualHeight + PickerRow2.ActualHeight + RateRow.ActualHeight
            + (2 * CompactAmountFloor) + CompactPadFloor + (6 * RootGrid.RowSpacing) + 8);

        public void SetCompactLayout(bool compact)
        {
            Header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            AmountRow1.MinHeight = compact ? CompactAmountFloor : AmountFloor;
            AmountRow2.MinHeight = compact ? CompactAmountFloor : AmountFloor;
            PadRow.MinHeight = compact ? CompactPadFloor : PadFloor;
        }
    }
}
