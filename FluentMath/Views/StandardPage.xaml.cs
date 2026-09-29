using FluentMath.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace FluentMath.Views
{
    // the standard calculator: the display and the ViewModel of the scientific page over the keys a pocket
    // calculator prints; it has no panels and no shift, so there is nothing here but the way into view and
    // the way into compact mode
    public sealed partial class StandardPage : Page, ICompactPage
    {
        public CalculatorViewModel ViewModel { get; }

        // --- display ---
        // font sizes in pixels; the scientific page has its own pair
        private const double InputLineFontSize = 36; // the lower line, the formula being typed and then its result (bigger = larger)
        private const double HistoryLineFontSize = 16; // the upper line, the calculation that gave the result (bigger = larger)

        // --- row floors ---
        // in pixels; the full window and compact mode each have their own pair
        private const double DisplayFloor = 96; // the display with the caret bar under it, which alone takes 32 (smaller = shorter display allowed)
        private const double PadFloor = 220; // the whole keypad, six rows of keys (smaller = shorter keys allowed)
        private const double CompactDisplayFloor = 120; // the same two while compact
        private const double CompactPadFloor = 180;

        // --- compact mode ---
        // sizes in pixels; the start size is the one the Windows Calculator opens its keep on top window at
        private const double CompactStartWidth = 320; // the first compact window of a session, title bar included (bigger = wider)
        private const double CompactStartHeight = 394; // (bigger = taller)
        private const double CompactMinWidth = 200; // how narrow the page can be dragged (smaller = narrower floor)

        // the key labels drop to a smaller size under this window height while compact, since the keys are
        // too short for them by then
        private const double SmallKeysBelowHeight = 380; // window height in pixels, title bar included (bigger = small labels sooner)
        private const double SmallKeyTextScale = 0.8; // the small labels against the normal ones (smaller = smaller labels)

        private bool _isCompact;
        private bool _hasSmallKeys;

        // every label on the keypad with the size the markup gave it; filled on the first switch
        private List<(DependencyObject Label, DependencyProperty Property, double Size)>? _keyLabels;


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new CalculatorViewModel(App.Settings);
            this.InitializeComponent();

            Display.InputFontSize = InputLineFontSize;
            Display.HistoryFontSize = HistoryLineFontSize;

            DisplayRow.MinHeight = DisplayFloor;
            PadRow.MinHeight = PadFloor;

            this.Loaded += StandardPage_Loaded;
            this.SizeChanged += StandardPage_SizeChanged;
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


        // === compact mode ===

        // the window owns compact mode, see MainWindow; this page only asks for it and hands back its header
        // while it lasts, since the title bar carries the way back
        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.EnterCompactMode();
        }

        public Size CompactStartSize => new Size(CompactStartWidth, CompactStartHeight);

        // both floors plus the 12 of margins around and between the rows
        public Size CompactMinSize => new Size(CompactMinWidth, CompactDisplayFloor + CompactPadFloor + 12);

        // compact also lowers the two row floors, so the display and the keys can get shorter than the full
        // window ever lets them
        public void SetCompactLayout(bool compact)
        {
            _isCompact = compact;

            Header.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            DisplayRow.MinHeight = compact ? CompactDisplayFloor : DisplayFloor;
            PadRow.MinHeight = compact ? CompactPadFloor : PadFloor;

            UpdateKeyTextSize();
        }

        private void StandardPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateKeyTextSize();
        }

        // the display needs nothing of the kind, since it scales its formula down to the height it gets
        private void UpdateKeyTextSize()
        {
            bool small = _isCompact && XamlRoot != null && XamlRoot.Size.Height < SmallKeysBelowHeight;
            if (small == _hasSmallKeys) return;

            _hasSmallKeys = small;
            _keyLabels ??= CollectKeyLabels();

            double scale = small ? SmallKeyTextScale : 1;
            foreach ((DependencyObject label, DependencyProperty property, double size) in _keyLabels)
            {
                label.SetValue(property, size * scale);
            }
        }

        // the text or glyph on every key, and the two letters of the fraction key, which is a small grid
        private List<(DependencyObject Label, DependencyProperty Property, double Size)> CollectKeyLabels()
        {
            var labels = new List<(DependencyObject Label, DependencyProperty Property, double Size)>();

            foreach (Button key in Pad.Children.OfType<Button>())
            {
                CollectLabels(key.Content, labels);
            }

            return labels;
        }

        private static void CollectLabels(object content, List<(DependencyObject Label, DependencyProperty Property, double Size)> labels)
        {
            switch (content)
            {
                case TextBlock text:
                    labels.Add((text, TextBlock.FontSizeProperty, text.FontSize));
                    break;

                case FontIcon icon:
                    labels.Add((icon, FontIcon.FontSizeProperty, icon.FontSize));
                    break;

                case Panel panel:
                    foreach (UIElement child in panel.Children)
                    {
                        CollectLabels(child, labels);
                    }
                    break;
            }
        }
    }
}
