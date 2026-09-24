using FluentMath.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIEx;

namespace FluentMath
{
    // shell of the app: navigation sidebar plus the content Frame every page is hosted in
    //
    // also owns the theme, because switching it has to touch two things a Page cannot reach:
    // the XAML content tree and the native title bar buttons on the AppWindow
    public sealed partial class MainWindow : Window
    {
        // === fields ===

        public static MainWindow Instance { get; private set; }

        // last theme tag that was applied; SettingsPage reads it back to preselect its combo box
        public string CurrentTheme { get; private set; } = "Default";


        // === constructor ===

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.SetIcon("Assets\\Icon\\Icon.ico");

            MainFrame.Navigate(typeof(ScientificPage));
            NavView.SelectedItem = NavView.MenuItems[0];

            // draw our own title bar into the client area; the caption buttons keep transparent
            // backgrounds so the Mica backdrop stays visible behind them
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            // start size, plus a floor that keeps the keypad from being squeezed out of the window
            //
            // the floor carries the two fixed bars on the scientific page above the keypad, the caret bar
            // and the panel bar, which together are about 80px that cannot shrink
            this.SetWindowSize(330, 500);
            var manager = WindowManager.Get(this);
            manager.MinWidth = 300;
            manager.MinHeight = 460;
        }


        // === navigation ===

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            string itemTag = args.InvokedItemContainer.Tag.ToString();

            switch (itemTag)
            {
                case "Standard":
                    MainFrame.Navigate(typeof(ScientificPage));
                    break;
                case "Currency":
                    MainFrame.Navigate(typeof(CurrencyPage));
                    break;
                case "Settings":
                    MainFrame.Navigate(typeof(SettingsPage));
                    break;
            }
        }


        // === theming ===

        // applies a theme to both halves of the window; the XAML content follows RequestedTheme on the
        // root element, the native caption buttons only follow AppWindow.TitleBar.PreferredTheme
        public void ApplyTheme(string themeTag)
        {
            CurrentTheme = themeTag;

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = themeTag switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }

            AppWindow.TitleBar.PreferredTheme = themeTag switch
            {
                "Light" => TitleBarTheme.Light,
                "Dark" => TitleBarTheme.Dark,
                _ => TitleBarTheme.UseDefaultAppMode
            };
        }
    }
}
