using FluentMath.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace FluentMath.Views
{
    public sealed partial class SettingsPage : Page
    {
        // ComboBox.SelectionChanged already fires from inside InitializeComponent, before the page is
        // in any usable state; without this guard the first navigation to the page would reapply the
        // theme as a side effect of building the control
        private bool _isLoading = true;

        // --- text icons ---
        // two headers carry a few characters of text instead of a glyph, see TextIcon
        // sizes are font sizes in pixels on screen, weights run from 300 (light) over 400 (the pad keys)
        // to 700 (bold) in any step, the range Segoe UI Variable has; outside it the font stays at its end
        private const double NumberFormatIconSize = 14; // size of ×10ⁿ (bigger = larger; about twice as wide as the size, so 14 is 28 wide)
        private const ushort NumberFormatIconWeight = 400; // weight of ×10ⁿ (higher = bolder)
        private const double SeparatorsIconSize = 17.6; // size of 0,1 (bigger = larger; 17.6 is what 24 in a 24 box came out as)
        private const ushort SeparatorsIconWeight = 400; // weight of 0,1 (higher = bolder)
        private const double IconOverhang = 30; // how far a text icon may draw past its box on either side (bigger = room for a wider text, past it the text is cut off)
        private const double IconBoxSize = 20; // the box a card gives its icon, SettingsCardHeaderIconMaxSize in the toolkit; not a knob, anything else scales every text icon by 20 over it

        public SettingsPage()
        {
            InitializeComponent();

            NumberFormatExpander.HeaderIcon = TextIcon("×10ⁿ", NumberFormatIconSize, NumberFormatIconWeight);
            SeparatorsExpander.HeaderIcon = TextIcon("0,1", SeparatorsIconSize, SeparatorsIconWeight);

            RestoreThemeSelection();
            RestoreCalculatorSettings();
            _isLoading = false;
        }


        // === theme ===

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string themeTag = selectedItem.Tag.ToString();
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.ApplyTheme(themeTag);
                }
            }
        }

        // the theme is not persisted anywhere, MainWindow holds the only copy of it for this session
        private void RestoreThemeSelection()
        {
            string currentTheme = "Default";
            if (MainWindow.Instance != null)
            {
                currentTheme = MainWindow.Instance.CurrentTheme;
            }

            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag?.ToString() == currentTheme)
                {
                    ThemeComboBox.SelectedItem = item;
                    return;
                }
            }
            ThemeComboBox.SelectedIndex = 0;
        }


        // === calculator ===

        // the selected index of each combo box is the value it stands for, see the markup
        private void RestoreCalculatorSettings()
        {
            CalculatorSettings settings = App.Settings;

            AngleUnitComboBox.SelectedIndex = (int)settings.AngleMode;
            ResultFormComboBox.SelectedIndex = settings.ExactFirst ? 0 : 1;
            FractionFormComboBox.SelectedIndex = settings.MixedFirst ? 1 : 0;
            RecurringToggle.IsOn = settings.RecurringDecimals;
            NotationComboBox.SelectedIndex = (int)settings.NumberFormat.Notation;
            DigitsComboBox.SelectedIndex = settings.NumberFormat.Digits;
            PrefixesToggle.IsOn = settings.UsePrefixes;
            DecimalMarkComboBox.SelectedIndex = (int)settings.DecimalMark;
            GroupDigitsToggle.IsOn = settings.GroupDigits;

            UpdateDigitsAvailability();
        }

        private void CalculatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteCalculatorSettings();
        }

        private void CalculatorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            WriteCalculatorSettings();
        }

        // every control writes the whole section back, which cannot miss one the way a handler per
        // control could
        private void WriteCalculatorSettings()
        {
            if (_isLoading) return;

            CalculatorSettings settings = App.Settings;

            settings.AngleMode = (AngleMode)AngleUnitComboBox.SelectedIndex;
            settings.ExactFirst = ResultFormComboBox.SelectedIndex == 0;
            settings.MixedFirst = FractionFormComboBox.SelectedIndex == 1;
            settings.RecurringDecimals = RecurringToggle.IsOn;
            settings.NumberFormat = new NumberFormat((NumberNotation)NotationComboBox.SelectedIndex, DigitsComboBox.SelectedIndex);
            settings.UsePrefixes = PrefixesToggle.IsOn;
            settings.DecimalMark = (DecimalMark)DecimalMarkComboBox.SelectedIndex;
            settings.GroupDigits = GroupDigitsToggle.IsOn;

            UpdateDigitsAvailability();
        }

        // Norm writes every digit it has, so the digit count only means something for Fix and Sci
        private void UpdateDigitsAvailability()
        {
            DigitsCard.IsEnabled = NotationComboBox.SelectedIndex >= (int)NumberNotation.Fix;
        }


        // === header icons ===

        // a header icon made of text, in the font the pad keys write in; a FontIcon draws any string it
        // is given as its glyph, centred in its own width, so ×10ⁿ is the same characters as on the key
        //
        // the card hosts its icon in a Viewbox of at most 20 by 20 that scales whatever it gets to fit
        // it; ×10ⁿ is about twice as wide as it is tall, so at any font size it was scaled back to 20
        // wide, the size of a ten pixel ×10ⁿ, and only 0,1 followed its knob until it got that wide
        // the icon therefore hands the Viewbox exactly the box, which keeps the scale at 1, and draws
        // wider than that: its width runs IconOverhang past the box on both sides and a margin of the
        // same amount pulled in takes that back out of the layout, so the text reaches into the space
        // around the icon while the header beside it stays in line with every other card
        private static FontIcon TextIcon(string text, double fontSize, ushort weight)
        {
            return new FontIcon
            {
                Glyph = text,
                FontFamily = new FontFamily("XamlAutoFontFamily"), // what BodyLargeTextBlockStyle sets on the pad keys
                FontSize = fontSize,
                FontWeight = new FontWeight { Weight = weight },
                Width = IconBoxSize + 2 * IconOverhang,
                Height = IconBoxSize,
                Margin = new Thickness(-IconOverhang, 0, -IconOverhang, 0)
            };
        }
    }
}
