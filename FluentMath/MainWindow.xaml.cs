using FluentMath.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace FluentMath
{
    // shell of the app: navigation sidebar plus the content Frame every page is hosted in
    //
    // also owns the theme, because switching it has to touch two things a Page cannot reach:
    // the XAML content tree and the native title bar buttons on the AppWindow
    //
    // and the compact overlay, for the same reason: it swaps the presenter on the AppWindow and takes the
    // navigation and the title bar out of the way, and the standard page only asks for it
    public sealed partial class MainWindow : Window
    {
        // === fields ===

        public static MainWindow Instance { get; private set; }

        // last theme tag that was applied; SettingsPage reads it back to preselect its combo box
        public string CurrentTheme { get; private set; } = "Default";

        // --- full window ---
        // sizes in device independent pixels
        private const double FullStartWidth = 330; // the window the app opens with (bigger = wider)
        private const double FullStartHeight = 500; // (bigger = taller)
        private const double FullMinWidth = 300; // how narrow the window can be dragged (smaller = narrower floor)
        private const double FullMinHeight = 460; // how short the window can be dragged (smaller = lower floor)

        // --- compact window ---
        // sizes in device independent pixels; the start size is the one the Windows Calculator opens its keep
        // on top window at, and the height floor is the title bar plus the display and pad floors of the
        // standard page, 96 and 220
        private const double CompactStartWidth = 320; // the first compact window of a session (bigger = wider)
        private const double CompactStartHeight = 394; // (bigger = taller)
        private const double CompactMinWidth = 300; // how narrow the compact window can be dragged (smaller = narrower floor)
        private const double CompactMinHeight = 360; // how short it can be dragged before the pad is squeezed (smaller = lower floor)

        private readonly WindowManager _windowManager;

        // the compact window comes back at the size it was last dragged to for the rest of the session, and
        // the full window at the place, size and presenter it had before
        private bool _isCompact;
        private Size _compactSize = new Size(CompactStartWidth, CompactStartHeight);
        private AppWindowPresenter? _fullPresenter;
        private RectInt32 _fullBounds;
        private bool _fullWasMaximized;

        // what the title bar shows outside compact; the compact bar shows neither
        private readonly IconSource? _titleBarIcon;
        private readonly string _titleBarTitle;


        // === constructor ===

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.SetIcon("Assets\\Icon\\Icon.ico");

            // the app opens on the standard calculator; its item is looked up by the tag, since the list
            // opens with a group header
            ShowPage(typeof(StandardPage));
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(item => (string)item.Tag == "Standard");

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

            // the return key is declared in the title bar markup for its look, but only hangs in the bar
            // while compact: a header the bar carries switches it to its header layout, visible or not
            _titleBarIcon = AppTitleBar.IconSource;
            _titleBarTitle = AppTitleBar.Title;
            AppTitleBar.LeftHeader = null;

            // start size, plus a floor that keeps the keypad from being squeezed out of the window
            //
            // the floor carries the two fixed bars on the scientific page above the keypad, the caret bar
            // and the panel bar, which together are about 80px that cannot shrink
            this.SetWindowSize(FullStartWidth, FullStartHeight);
            _windowManager = WindowManager.Get(this);
            _windowManager.MinWidth = FullMinWidth;
            _windowManager.MinHeight = FullMinHeight;
        }


        // === navigation ===

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            string itemTag = args.InvokedItemContainer.Tag.ToString();

            Type? page = itemTag switch
            {
                "Standard" => typeof(StandardPage),
                "Scientific" => typeof(ScientificPage),
                "Currency" => typeof(CurrencyPage),
                "Settings" => typeof(SettingsPage),
                _ => null
            };

            // a second click on the item already shown would navigate the page onto itself
            if (page == null || MainFrame.CurrentSourcePageType == page) return;

            ShowPage(page);
        }

        // every page switches in without the frames slide; the pages with a pad bring their own entrance,
        // which grows the pad in rather than moving the whole page
        private void ShowPage(Type page)
        {
            MainFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
        }


        // === compact overlay ===

        // a small window on top of every other one, holding the standard calculator and nothing else
        //
        // the floor is lowered before the presenter changes, since the full one would hold the window above
        // the compact size
        public void EnterCompactMode()
        {
            if (_isCompact) return;

            _fullPresenter = AppWindow.Presenter;
            _fullWasMaximized = _fullPresenter is OverlappedPresenter overlapped
                && overlapped.State == OverlappedPresenterState.Maximized;
            _fullBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y,
                AppWindow.Size.Width, AppWindow.Size.Height);

            _windowManager.MinWidth = CompactMinWidth;
            _windowManager.MinHeight = CompactMinHeight;

            AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            this.SetWindowSize(_compactSize.Width, _compactSize.Height);

            _isCompact = true;
            ShowCompactChrome(true);
        }

        // back to the full window where it was; a maximized one is maximized again rather than given its
        // maximized bounds as a plain size
        //
        // the presenter handed back is the one the window had, not a fresh one, so nothing that was set on it
        // is lost on the way
        public void ExitCompactMode()
        {
            if (!_isCompact) return;

            double scale = Content.XamlRoot.RasterizationScale;
            _compactSize = new Size(AppWindow.Size.Width / scale, AppWindow.Size.Height / scale);

            AppWindow.SetPresenter(_fullPresenter);

            _windowManager.MinWidth = FullMinWidth;
            _windowManager.MinHeight = FullMinHeight;

            if (_fullWasMaximized && _fullPresenter is OverlappedPresenter overlapped)
            {
                overlapped.Maximize();
            }
            else
            {
                AppWindow.MoveAndResize(_fullBounds);
            }

            _isCompact = false;
            ShowCompactChrome(false);
        }

        // compact shows one row above the page, the return key and the close button: the pane toggle, the
        // app icon and name go, and so does the header of the standard page
        private void ShowCompactChrome(bool compact)
        {
            NavView.IsPaneOpen = false;
            NavView.IsPaneToggleButtonVisible = !compact;

            AppTitleBar.IconSource = compact ? null : _titleBarIcon;
            AppTitleBar.Title = compact ? "" : _titleBarTitle;
            AppTitleBar.LeftHeader = compact ? CompactReturnButton : null;

            if (MainFrame.Content is StandardPage page)
            {
                page.SetCompactLayout(compact);
            }
        }

        private void CompactReturnButton_Click(object sender, RoutedEventArgs e)
        {
            ExitCompactMode();
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
