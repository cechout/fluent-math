using FluentMath.Models;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
            AppDataFolderCard.Description = PersistenceService.Instance.RootFolder;
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


        // === backup and reset ===

        // the folder may not exist yet, before the first save
        private void OpenAppDataFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = PersistenceService.Instance.RootFolder;

            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch { /* no explorer reachable, and this page has nowhere to report that to */ }
        }

        private async void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker(MainWindow.Instance.AppWindow.Id)
            {
                SuggestedFileName = $"FluentMath-Backup-{DateTime.Now:yyyy-MM-dd}",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add("Backup File", new List<string> { ".zip" });

            PickFileResult? picked = await picker.PickSaveFileAsync();
            if (picked == null) return;

            try
            {
                PersistenceService.Instance.ExportBackup(picked.Path);
                await ShowInfoAsync("Export Successful", "Your settings have been exported.");
            }
            catch
            {
                await ShowInfoAsync("Export Failed", "The settings could not be exported.");
            }
        }

        private async void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker(MainWindow.Instance.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".zip");

            PickFileResult? picked = await picker.PickSingleFileAsync();
            if (picked == null) return;

            if (!await ConfirmAsync("Import Settings?",
                "This overwrites all settings, window and page states, then restarts the app.", "Import")) return;

            if (PersistenceService.Instance.ImportBackup(picked.Path))
            {
                await RestartAsync();
            }
            else
            {
                await ShowInfoAsync("Import Failed", "The selected file is not a valid Fluent Math backup.");
            }
        }

        private async void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (!await ConfirmResetAsync("All Settings")) return;

            PersistenceService.Instance.ResetAll();
            await RestartAsync();
        }

        private async void ResetGeneralSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!await ConfirmResetAsync("General Settings")) return;

            PersistenceService.Instance.ResetSettings();
            await RestartAsync();
        }

        private async void ResetWindowAndPageStates_Click(object sender, RoutedEventArgs e)
        {
            if (!await ConfirmResetAsync("Window and Page States")) return;

            PersistenceService.Instance.ResetWindowAndPageStates();
            await RestartAsync();
        }

        private Task<bool> ConfirmResetAsync(string what)
        {
            return ConfirmAsync($"Reset {what}?",
                "This restores the default values and restarts the app. It cannot be undone.", "Reset");
        }

        // everything is read at launch, so a new start is what brings the files on disk into the app
        // Restart only returns when it failed; the disk is already done and nothing is saved over it, so a
        // start by hand finishes the job
        private async Task RestartAsync()
        {
            AppInstance.Restart("");

            await ShowInfoAsync("Restart Fluent Math", "Close Fluent Math and open it again to finish.");
        }

        private async Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            ContentDialog dialog = NewDialog(title, message);
            dialog.PrimaryButtonText = confirmText;
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Close;

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private async Task ShowInfoAsync(string title, string message)
        {
            ContentDialog dialog = NewDialog(title, message);
            dialog.CloseButtonText = "OK";

            await dialog.ShowAsync();
        }

        // a dialog sits beside the window content, so the theme the app sets there is handed over by hand
        private ContentDialog NewDialog(string title, string message)
        {
            return new ContentDialog
            {
                Title = title,
                Content = message,
                XamlRoot = this.XamlRoot,
                RequestedTheme = XamlRoot.Content is FrameworkElement root ? root.ActualTheme : ElementTheme.Default
            };
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
