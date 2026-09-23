using FluentMath.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentMath.Views
{
    public sealed partial class SettingsPage : Page
    {
        // ComboBox.SelectionChanged already fires from inside InitializeComponent, before the page is
        // in any usable state; without this guard the first navigation to the page would reapply the
        // theme as a side effect of building the control
        private bool _isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();

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
            settings.NumberFormat = new NumberFormat((NumberNotation)NotationComboBox.SelectedIndex, DigitsComboBox.SelectedIndex);
            settings.UsePrefixes = PrefixesToggle.IsOn;
            settings.DecimalMark = (DecimalMark)DecimalMarkComboBox.SelectedIndex;
            settings.GroupDigits = GroupDigitsToggle.IsOn;

            UpdateDigitsAvailability();
        }

        // Norm writes every digit it has, so the digit count only means something for Fix and Sci
        private void UpdateDigitsAvailability()
        {
            DigitsComboBox.IsEnabled = NotationComboBox.SelectedIndex >= (int)NumberNotation.Fix;
        }
    }
}
