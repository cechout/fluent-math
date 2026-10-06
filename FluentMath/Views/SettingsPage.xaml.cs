using CommunityToolkit.WinUI.Controls;
using FluentMath.Distribution;
using FluentMath.Models;
using FluentMath.Models.Converters;
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
using Windows.UI;
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
        private const double PrecisionIconSize = 17; // .00
        private const ushort PrecisionIconWeight = 400;
        private const double IconOverhang = 30; // how far a text icon may draw past its box on either side
        private const double IconBoxSize = 20; // SettingsCardHeaderIconMaxSize; (not a knob)
        private const double HeaderIconsMinWidth = 286; // card width; SettingsCardWrapNoIconThreshold

        // every card and expander with a header icon, and the icon to restore
        private readonly List<(Control Element, IconElement Icon)> _headerIcons = new();
        private bool _headerIconsShown = true;

        // --- dialogs ---
        private const double DialogMaxWidthShare = 0.9; // of the window width
        private const double DialogPlatformMinWidth = 320; // ContentDialogMinWidth; (not a knob)

        // --- update badge ---
        // literals, not theme resources: a status colour means the same in both themes, and a white glyph on a
        // coloured plate reads on both; the accent states use the system accent
        private static readonly Color SuccessColor = Color.FromArgb(0xFF, 0x4C, 0xA2, 0x2E);
        private static readonly Color CautionColor = Color.FromArgb(0xFF, 0xC1, 0x8A, 0x1B);
        private static readonly Color CriticalColor = Color.FromArgb(0xFF, 0xC4, 0x3E, 0x1C);
        private static readonly Color NeutralColor = Color.FromArgb(0xFF, 0x6B, 0x6B, 0x6B);
        private static readonly Color AccentFallbackColor = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);

        public SettingsPage()
        {
            InitializeComponent();

            NumberFormatExpander.HeaderIcon = TextIcon("×10ⁿ", NumberFormatIconSize, NumberFormatIconWeight);
            SeparatorsExpander.HeaderIcon = TextIcon("0,1", SeparatorsIconSize, SeparatorsIconWeight);
            PrecisionExpander.HeaderIcon = TextIcon(".00", PrecisionIconSize, PrecisionIconWeight);
            CollectHeaderIcons();

            RestoreThemeSelection();
            StartupPageComboBox.SelectedIndex = (int)SettingsService.Instance.StartupPage;
            RestoreCalculatorSettings();
            RestoreConverterSettings();
            AppDataFolderCard.Description = PersistenceService.Instance.RootFolder;
            VersionTextBlock.Text = UpdateService.VersionLabel(UpdateService.CurrentVersion);
            CheckUpdatesToggle.IsOn = SettingsService.Instance.CheckUpdatesOnStartup;
            _isLoading = false;

            // the page is built anew on every visit, so it follows the update service only while shown
            Loaded += (_, _) =>
            {
                UpdateService.Instance.UpdateStateChanged += RefreshUpdateState;
                RefreshUpdateState();
            };
            Unloaded += (_, _) => UpdateService.Instance.UpdateStateChanged -= RefreshUpdateState;
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


        // === converter ===

        // each combo box lists its range from the lowest value, so the index is the value less the minimum
        private void RestoreConverterSettings()
        {
            ConverterSettings settings = SettingsService.Instance.Converter;

            FillRange(CurrencyDecimalsComboBox, ConverterSettings.MinCurrencyDecimals, ConverterSettings.MaxCurrencyDecimals);
            FillRange(UnitDigitsComboBox, ConverterSettings.MinUnitDigits, ConverterSettings.MaxUnitDigits);

            CurrencyDecimalsComboBox.SelectedIndex = settings.CurrencyDecimals - ConverterSettings.MinCurrencyDecimals;
            UnitDigitsComboBox.SelectedIndex = settings.UnitDigits - ConverterSettings.MinUnitDigits;
        }

        private static void FillRange(ComboBox comboBox, int min, int max)
        {
            for (int value = min; value <= max; value++)
                comboBox.Items.Add(value.ToString());
        }

        private void ConverterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            ConverterSettings settings = SettingsService.Instance.Converter;

            settings.CurrencyDecimals = CurrencyDecimalsComboBox.SelectedIndex + ConverterSettings.MinCurrencyDecimals;
            settings.UnitDigits = UnitDigitsComboBox.SelectedIndex + ConverterSettings.MinUnitDigits;
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
        // its width is capped to a share of the window, the floor lowered with it for a narrow one
        private ContentDialog NewDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                XamlRoot = this.XamlRoot,
                RequestedTheme = XamlRoot.Content is FrameworkElement root ? root.ActualTheme : ElementTheme.Default
            };

            double maxWidth = XamlRoot.Size.Width * DialogMaxWidthShare;
            dialog.Resources["ContentDialogMaxWidth"] = maxWidth;
            dialog.Resources["ContentDialogMinWidth"] = Math.Min(DialogPlatformMinWidth, maxWidth);

            return dialog;
        }


        // === updates ===

        // the pill and the state card, pulled from the service on every change
        private void RefreshUpdateState()
        {
            UpdateService service = UpdateService.Instance;

            string versionLabel = UpdateService.VersionLabel(service.Latest?.Version);
            UpdatePillText.Text = versionLabel.Length > 0 ? versionLabel : "Update";
            UpdatePillButton.Visibility = service.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

            bool checking = service.UiState == UpdateUiState.Checking;
            UpdateCheckingRing.IsActive = checking;
            UpdateCheckingRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
            UpdateBadge.Visibility = checking ? Visibility.Collapsed : Visibility.Visible;
            UpdateStatusCard.IsEnabled = !checking;

            string lastChecked = service.LastCheckedAt.HasValue
                ? $"Last checked {service.LastCheckedAt.Value:dd.MM. HH:mm}"
                : "Not checked yet";

            switch (service.UiState)
            {
                case UpdateUiState.UpToDate:
                    SetUpdateState("\uE73E", SuccessColor, "You are up to date", lastChecked);
                    break;

                case UpdateUiState.Checking:
                    SetUpdateState("\uE895", AccentColor(), "Checking for updates", "");
                    break;

                case UpdateUiState.UpdateAvailable:
                    // a store update without a GitHub name has no version
                    SetUpdateState("\uE896", AccentColor(), "Update available", versionLabel.Length > 0
                        ? $"{versionLabel} is ready to install"
                        : "A new version is ready to install");
                    break;

                case UpdateUiState.Skipped:
                    SetUpdateState("\uE7BA", CautionColor, $"{UpdateService.VersionLabel(service.SkippedVersion)} skipped",
                        "Select to install it anyway");
                    break;

                case UpdateUiState.Failed:
                    SetUpdateState("\uE711", CriticalColor, "Check failed", AppDistribution.SupportsSelfUpdate
                        ? "Could not reach GitHub, select to try again"
                        : "Could not reach the Microsoft Store, select to try again");
                    break;

                default:
                    SetUpdateState("\uE895", NeutralColor, "Check for updates", lastChecked);
                    break;
            }
        }

        private void SetUpdateState(string glyph, Color color, string title, string description)
        {
            UpdateBadgeIcon.Glyph = glyph;
            UpdateBadge.Background = new SolidColorBrush(color);
            UpdateStatusCard.Header = title;
            UpdateStatusCard.Description = description;
        }

        // the Windows accent, else the WinUI default
        private static Color AccentColor() =>
            Application.Current.Resources.TryGetValue("SystemAccentColor", out object value) && value is Color color
                ? color
                : AccentFallbackColor;

        // one card, three jobs, by service state
        private async void UpdateStatusCard_Click(object sender, RoutedEventArgs e)
        {
            UpdateService service = UpdateService.Instance;

            switch (service.UiState)
            {
                case UpdateUiState.UpdateAvailable:
                    await UpdateDialog.ShowAsync(XamlRoot, service.Latest);
                    break;

                case UpdateUiState.Skipped:
                    // the way back out of a skip
                    service.ClearSkippedVersion();
                    await UpdateDialog.ShowAsync(XamlRoot, service.Latest);
                    break;

                default:
                    await service.CheckAsync();
                    break;
            }
        }

        private async void UpdatePillButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDialog.ShowAsync(XamlRoot, UpdateService.Instance.Latest);
        }

        private void CheckUpdatesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            SettingsService.Instance.CheckUpdatesOnStartup = CheckUpdatesToggle.IsOn;
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

        private void CollectHeaderIcons()
        {
            foreach (UIElement child in CardsPanel.Children)
            {
                IconElement? icon = child switch
                {
                    SettingsCard card => card.HeaderIcon,
                    SettingsExpander expander => expander.HeaderIcon,
                    _ => null
                };
                if (icon != null) _headerIcons.Add(((Control)child, icon));
            }
        }

        private void CardsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateHeaderIcons(e.NewSize.Width >= HeaderIconsMinWidth);
        }

        // hides or restores every header icon at once, measured on the width of a plain card
        //
        // the toolkit would hide each icon by the width of its own card, and an expander header is a card
        // narrowed by its chevron; so the toolkit threshold is off and the page decides for all of them
        private void UpdateHeaderIcons(bool show)
        {
            if (show == _headerIconsShown) return;
            _headerIconsShown = show;

            foreach (var (element, icon) in _headerIcons)
            {
                IconElement value = show ? icon : null!; // null takes the icon and its gap out
                if (element is SettingsCard card) card.HeaderIcon = value;
                else if (element is SettingsExpander expander) expander.HeaderIcon = value;
            }
        }
    }
}
