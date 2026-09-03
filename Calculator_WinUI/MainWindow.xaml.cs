using Calculator_WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace Calculator_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        public string CurrentTheme { get; private set; } = "Default"; // save current theme for settings page combo box

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.SetIcon("Assets\\Icon\\Icon.ico");

            MainFrame.Navigate(typeof(StandardPage));
            NavView.SelectedItem = NavView.MenuItems[0];

            // AppWindow configuration
            // theme 
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

            }
            // size
            this.SetWindowSize(330, 480);
            var manager = WinUIEx.WindowManager.Get(this);
            manager.MinWidth = 300;
            manager.MinHeight = 450;
        }

        // click navigation
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            string itemTag = args.InvokedItemContainer.Tag.ToString();

            switch (itemTag)
            {
                case "Standard":
                    MainFrame.Navigate(typeof(StandardPage));
                    break;
                case "Currency":
                    MainFrame.Navigate(typeof(CurrencyPage));
                    break;
                case "Settings":
                    MainFrame.Navigate(typeof(SettingsPage));
                    break;
            }
        }

        // this method changes both the xaml content theme and the native title bar theme
        public void ApplyTheme(string themeTag)
        {
            CurrentTheme = themeTag; // save current theme for settings page combo box

            // switch the theme of the app content
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = themeTag switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }

            // let windows handle the native title bar buttons automatically
            AppWindow.TitleBar.PreferredTheme = themeTag switch
            {
                "Light" => Microsoft.UI.Windowing.TitleBarTheme.Light,
                "Dark" => Microsoft.UI.Windowing.TitleBarTheme.Dark,
                _ => Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode
            };
        }
    }
}
