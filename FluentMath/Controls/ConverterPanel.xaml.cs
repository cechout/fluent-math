using FluentMath.ViewModels;
using FluentMath.Views;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.UI.Text;

namespace FluentMath.Controls
{
    // the converter panel:
    // header, pickers, amount lines, rate line and keypad, the same on every converter;
    // the page only names it and hands it a ConverterViewModel
    public sealed partial class ConverterPanel : UserControl
    {
        // set by the page before the panel is loaded; the bindings read it once
        public ConverterViewModel ViewModel { get; set; } = null!;

        public string Title { get; set; } = "";

        // --- row floors ---
        // in px; (the full window and compact mode each have their own pair)
        private const double AmountFloor = 40; // each of the two amount lines
        private const double PadFloor = 0; // the number pad, five rows of keys; (0 = no floor)
        private const double CompactAmountFloor = 40;
        private const double CompactPadFloor = 150;

        // --- compact mode ---
        // in px
        private const double CompactStartWidth = 320;
        private const double CompactStartHeight = 460;
        private const double CompactMinWidth = 200;

        // --- key labels ---
        // key font drops to smaller size once the number pad is shorter than this
        private const double SmallKeysBelowHeight = 170; // number pad height in pixels
        private const double SmallKeyTextScale = 0.8;

        public ConverterPanel()
        {
            this.InitializeComponent();

            AmountRow1.MinHeight = AmountFloor;
            AmountRow2.MinHeight = AmountFloor;
            PadRow.MinHeight = PadFloor;
            SmallKeyLabels.Attach(Pad, SmallKeysBelowHeight, SmallKeyTextScale);

            this.Loaded += ConverterPanel_Loaded;
        }


        // also raised when a cached page is back in the tree
        private void ConverterPanel_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }


        // each unit button opens the invisible combo box under it, which does the selecting
        private void UnitButton1_Click(object sender, RoutedEventArgs e)
        {
            UnitComboBox1.IsDropDownOpen = true;
        }

        private void UnitButton2_Click(object sender, RoutedEventArgs e)
        {
            UnitComboBox2.IsDropDownOpen = true;
        }


        // === active line ===

        private void Amount1_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ViewModel.ActivateLine(top: true);
        }

        private void Amount2_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ViewModel.ActivateLine(top: false);
        }

        // the TitleLarge semibold for the line that takes the input, two steps lighter for the other
        private FontWeight WeightOf(bool active)
        {
            return active ? FontWeights.SemiBold : FontWeights.SemiLight;
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
