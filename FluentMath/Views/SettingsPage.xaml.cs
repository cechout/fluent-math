using FluentMath.Models;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI.Text;

namespace FluentMath.Views
{
    public sealed partial class SettingsPage : Page
    {
        // SelectionChanged already fires inside InitializeComponent; this keeps the build from applying anything
        private bool _isLoading = true;

        // --- text icons ---
        // in px; (weights: 300 light, 400 the pad keys, 700 bold)
        private const double NumberFormatIconSize = 17; // ×10ⁿ; (about twice as wide as the size)
        private const ushort NumberFormatIconWeight = 400;
        private const double SeparatorsIconSize = 18; // 0,1
        private const ushort SeparatorsIconWeight = 400;
        private const double IconOverhang = 30; // how far a text icon may draw past its box on either side
        private const double IconBoxSize = 20; // SettingsCardHeaderIconMaxSize; (not a knob)

        public SettingsPage()
        {
            InitializeComponent();

            NumberFormatExpander.HeaderIcon = TextIcon("×10ⁿ", NumberFormatIconSize, NumberFormatIconWeight);
            SeparatorsExpander.HeaderIcon = TextIcon("0,1", SeparatorsIconSize, SeparatorsIconWeight);

            RestoreThemeSelection();
            StartupPageComboBox.SelectedIndex = (int)SettingsService.Instance.StartupPage;
            RestoreCalculatorSettings();
            VersionTextBlock.Text = VersionLabel();
            _isLoading = false;
        }


        // === theme ===

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                SettingsService.Instance.AppTheme = selectedItem.Tag.ToString() ?? "Default";
            }
        }

        private void RestoreThemeSelection()
        {
            string currentTheme = SettingsService.Instance.AppTheme;

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


        // === start page ===

        // the selected index is the StartupPage value, see the markup
        private void StartupPageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            SettingsService.Instance.StartupPage = (StartupPage)StartupPageComboBox.SelectedIndex;
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

        // every control writes the whole section back, so none can be missed
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


        // === about ===

        // the running version as v2.2.0, from <Version> in the csproj
        private static string VersionLabel()
        {
            Version? version = typeof(App).Assembly.GetName().Version;
            return version == null ? "" : $"v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }


        // === header icons ===

        // a header icon made of text, in the pad key font; a FontIcon draws any string as its glyph
        //
        // the card scales its icon into a 20 by 20 Viewbox, so the icon hands it exactly that box and draws
        // IconOverhang past it on both sides; a negative margin takes the overhang back out of the layout
        private static FontIcon TextIcon(string text, double fontSize, ushort weight)
        {
            return new FontIcon
            {
                Glyph = text,
                FontFamily = new FontFamily("XamlAutoFontFamily"), // the pad key font
                FontSize = fontSize,
                FontWeight = new FontWeight { Weight = weight },
                Width = IconBoxSize + 2 * IconOverhang,
                Height = IconBoxSize,
                Margin = new Thickness(-IconOverhang, 0, -IconOverhang, 0)
            };
        }
    }
}
