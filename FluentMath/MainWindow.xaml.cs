using FluentMath.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    // and the compact overlay, for the same reason: it changes the AppWindow itself and takes the navigation
    // and the title bar out of the way, and a page only asks for it and brings its sizes, see ICompactPage
    public sealed partial class MainWindow : Window
    {
        // === win32 api imports ===

        // the frame the window draws, without the invisible resize border around it
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

        private const int DwmExtendedFrameBounds = 9; // DWMWA_EXTENDED_FRAME_BOUNDS

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


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
        // the sizes are the pages own, see ICompactPage; only the place is decided here, in device independent
        // pixels
        private const double CompactEdgeGap = 10; // how far the compact window stands off the top and right screen edge (bigger = further in)

        // room the compact floor keeps over what the page adds up to; every row is rounded to whole device
        // pixels, and at 150 percent six key rows and their gaps alone round a pixel or two past their sum,
        // which the bottom row of keys paid for at the very floor
        private const double CompactFloorBuffer = 8; // (bigger = more room left at the floor, a slightly taller floor)

        private readonly WindowManager _windowManager;

        // the compact window comes back at the size it was last dragged to on the same page for the rest of the
        // session, and the full window at the place and size it had before
        private bool _isCompact;
        private readonly Dictionary<Type, Size> _compactSizes = new Dictionary<Type, Size>();
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

        // a small window on top of every other one, holding the page it was asked from and nothing else
        //
        // on purpose not the CompactOverlay presenter: the Windows App SDK takes the resize border off a
        // window on it, so it keeps whatever size it was given and cannot be dragged to another one; measured
        // on 2.5.1, the presenter clears WS_THICKFRAME and the minimize and maximize boxes and sets
        // WS_EX_TOPMOST, so the window keeps its own presenter and gets the same three changes minus the border
        //
        // a maximized window is restored first, so the bounds kept for the way back are its normal ones, and
        // the floor is lowered before the resize, since the full one would hold the window above the compact
        // size
        public void EnterCompactMode()
        {
            if (_isCompact || MainFrame.Content is not ICompactPage page) return;

            OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;

            _fullWasMaximized = presenter.State == OverlappedPresenterState.Maximized;
            if (_fullWasMaximized) presenter.Restore();

            _fullBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y,
                AppWindow.Size.Width, AppWindow.Size.Height);

            presenter.IsAlwaysOnTop = true;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;

            // the floor holds the outer window, which reaches past the content by the invisible resize border,
            // so the border is measured and added onto what the content needs
            double scale = Content.XamlRoot.RasterizationScale;
            double borderWidth = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / scale;
            double borderHeight = (AppWindow.Size.Height - AppWindow.ClientSize.Height) / scale;

            _windowManager.MinWidth = page.CompactMinSize.Width + borderWidth;
            _windowManager.MinHeight = AppTitleBar.ActualHeight + page.CompactMinSize.Height + borderHeight
                + CompactFloorBuffer;

            // the return key takes the size of the close button across the bar from it; with minimize and
            // maximize gone that button is all the caption area holds, and its measures follow the flags
            // above straight away
            CompactReturnButton.Width = AppWindow.TitleBar.RightInset / scale;
            CompactReturnButton.Height = AppWindow.TitleBar.Height / scale;

            // the page takes its compact layout before the window shrinks, so no layout pass ever sees the small
            // window with the full floors and the header still in it
            _isCompact = true;
            ShowCompactChrome(true);

            Size size = _compactSizes.TryGetValue(page.GetType(), out Size dragged) ? dragged : page.CompactStartSize;
            this.SetWindowSize(size.Width, size.Height);
            PinCompactWindow();
        }

        // back to the full window where it was; the normal bounds go back first and the maximize after them,
        // so a window that was maximized still restores to its own size later rather than to the compact one
        public void ExitCompactMode()
        {
            if (!_isCompact) return;

            double scale = Content.XamlRoot.RasterizationScale;
            _compactSizes[MainFrame.Content.GetType()] = new Size(AppWindow.Size.Width / scale, AppWindow.Size.Height / scale);

            OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
            presenter.IsAlwaysOnTop = false;
            presenter.IsMinimizable = true;
            presenter.IsMaximizable = true;

            _windowManager.MinWidth = FullMinWidth;
            _windowManager.MinHeight = FullMinHeight;

            AppWindow.MoveAndResize(_fullBounds);
            if (_fullWasMaximized) presenter.Maximize();

            _isCompact = false;
            ShowCompactChrome(false);
        }

        // every way into compact puts the window into the top right corner of the work area of the screen it
        // is on, so the taskbar never covers it; it can be dragged away from there, and the next way in puts
        // it back
        //
        // the gap is measured to the frame that is drawn: a resizable window reaches past it on the sides and
        // the bottom with a border that is only there to catch the mouse, and a window is placed by its outer
        // bounds, so that border is counted back in
        private void PinCompactWindow()
        {
            double scale = Content.XamlRoot.RasterizationScale;
            int gap = (int)Math.Round(CompactEdgeGap * scale);

            RectInt32 work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

            int rightBorder = 0;
            int topBorder = 0;

            if (DwmGetWindowAttribute(this.GetWindowHandle(), DwmExtendedFrameBounds, out NativeRect frame,
                Marshal.SizeOf<NativeRect>()) == 0)
            {
                rightBorder = AppWindow.Position.X + AppWindow.Size.Width - frame.Right;
                topBorder = frame.Top - AppWindow.Position.Y;
            }

            AppWindow.Move(new PointInt32(
                work.X + work.Width - gap - AppWindow.Size.Width + rightBorder,
                work.Y + gap - topBorder));
        }

        // compact shows one row above the page, the return key and the close button: the pane toggle, the
        // app icon and name go, and so does the header of the page
        private void ShowCompactChrome(bool compact)
        {
            NavView.IsPaneOpen = false;
            NavView.IsPaneToggleButtonVisible = !compact;

            AppTitleBar.IconSource = compact ? null : _titleBarIcon;
            AppTitleBar.Title = compact ? "" : _titleBarTitle;
            AppTitleBar.LeftHeader = compact ? CompactReturnButton : null;

            if (MainFrame.Content is ICompactPage page)
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
