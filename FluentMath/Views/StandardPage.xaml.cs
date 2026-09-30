using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;

namespace FluentMath.Views
{
    // the standard calculator:
    // the display and the ViewModel of the scientific page over the keys a pocket calculator prints;
    // It has no panels and no shift, so there is nothing here but the way into view and
    // the way into compact mode
    public sealed partial class StandardPage : Page, ICompactPage
    {
        public CalculatorViewModel ViewModel { get; }

        // --- display ---
        // in px; (the scientific page has its own pair)
        private const double InputLineFontSize = 36; // the lower line; input
        private const double HistoryLineFontSize = 16; // the upper line; output

        // --- row floors ---
        // in px; (the full window and compact mode each have their own pair)
        private const double DisplayFloor = 96; // the display with the caret bar under it
        private const double PadFloor = 220; // the keypad, six rows of keys
        private const double CompactDisplayFloor = 120;
        private const double CompactPadFloor = 180;

        // --- compact mode ---
        // sizes in px; the start size is the one the Windows Calculator opens its keep on top window at
        private const double CompactStartWidth = 320;
        private const double CompactStartHeight = 394;
        private const double CompactMinWidth = 200;

        // --- key labels ---
        // key font drops to smaller size once the keypad is shorter than this
        private const double SmallKeysBelowHeight = 200; // keypad height in pixels
        private const double SmallKeyTextScale = 0.8;


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();

            Display.InputFontSize = InputLineFontSize;
            Display.HistoryFontSize = HistoryLineFontSize;

            DisplayRow.MinHeight = DisplayFloor;
            PadRow.MinHeight = PadFloor;
            SmallKeyLabels.Attach(Pad, SmallKeysBelowHeight, SmallKeyTextScale);

            this.Loaded += StandardPage_Loaded;
        }


        // === navigation ===

        // Loaded, not OnNavigatedTo; a cached page is only back in the tree by then
        private void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            PadEntrance.Play(Pad);
        }

        // the page is cached, so the decimal key reads its label again on every way back
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RefreshSettingLabels();
        }


        // === compact mode ===

        // MainWindow owns compact mode; the page only asks for it and lays itself out
        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.EnterCompactMode();
        }

        public Size CompactStartSize => new Size(CompactStartWidth, CompactStartHeight);

        // both floors plus 12 of margins
        public Size CompactMinSize => new Size(CompactMinWidth, CompactDisplayFloor + CompactPadFloor + 12);

        public void SetCompactLayout(bool compact)
        {
            Header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            DisplayRow.MinHeight = compact ? CompactDisplayFloor : DisplayFloor;
            PadRow.MinHeight = compact ? CompactPadFloor : PadFloor;
        }
    }
}
