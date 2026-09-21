using Microsoft.UI.Xaml.Controls;

namespace Calculator_WinUI.Views
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
    }
}
