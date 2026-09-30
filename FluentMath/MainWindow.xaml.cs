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
    // the app shell:
    // the navigation sidebar and the Frame every page is shown in;
    // it also owns the theme and compact mode, since both reach the window beyond the page
    public sealed partial class MainWindow : Window
    {
        // === win32 api imports ===

        // the drawn window frame, without the invisible resize border
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

        // the applied theme tag; SettingsPage preselects its combo box with it
        public string CurrentTheme { get; private set; } = "Default";

        // --- full window ---
        // in px
        private const double FullStartWidth = 330;
        private const double FullStartHeight = 500;
        private const double FullMinWidth = 300;
        private const double FullMinHeight = 460;

        // --- compact window ---
        // in px; (the sizes come from the page, see ICompactPage)
        private const double CompactEdgeGap = 10; // gap to the top and right screen edge
        private const double CompactFloorBuffer = 8; // room over the page floors, for pixel rounding
        private const double CompactReturnKeyOverhang = 2; // how far the return key reaches past the top edge

        private readonly WindowManager _windowManager;

        // the dragged compact size per page, and the full window bounds for the way back
        private bool _isCompact;
        private readonly Dictionary<Type, Size> _compactSizes = new Dictionary<Type, Size>();
        private RectInt32 _fullBounds;
        private bool _fullWasMaximized;

        // the title bar icon and name, hidden while compact
        private readonly IconSource? _titleBarIcon;
        private readonly string _titleBarTitle;


        // === constructor ===

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.SetIcon("Assets\\Icon\\Icon.ico");

            // opens on the standard calculator; its item is found by tag (the list starts with a header)
            ShowPage(typeof(StandardPage));
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(item => (string)item.Tag == "Standard");

            // our own title bar in the client area; transparent caption buttons, so the Mica shows through
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            // the return key only hangs in the bar while compact; any set header changes the bar layout
            _titleBarIcon = AppTitleBar.IconSource;
            _titleBarTitle = AppTitleBar.Title;
            AppTitleBar.LeftHeader = null;

            // start size, and a floor that keeps the keypad in the window
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

        // no frame slide; (the pad pages bring their own entrance)
        private void ShowPage(Type page)
        {
            MainFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
        }


        // === compact overlay ===

        // the current page alone in a small window on top of every other one
        //
        // on purpose not the CompactOverlay presenter, which cannot be resized (WASDK 2.5.1 drops the resize
        // border); the window keeps its presenter and gets the other three changes: on top, no min, no max
        public void EnterCompactMode()
        {
            if (_isCompact || MainFrame.Content is not ICompactPage page) return;

            OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;

            // a maximized window is restored first, so the kept bounds are its normal ones
            _fullWasMaximized = presenter.State == OverlappedPresenterState.Maximized;
            if (_fullWasMaximized) presenter.Restore();

            _fullBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y,
                AppWindow.Size.Width, AppWindow.Size.Height);

            presenter.IsAlwaysOnTop = true;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;

            // the floor is on the outer window, so the invisible resize border is added
            double scale = Content.XamlRoot.RasterizationScale;
            double borderWidth = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / scale;
            double borderHeight = (AppWindow.Size.Height - AppWindow.ClientSize.Height) / scale;

            _windowManager.MinWidth = page.CompactMinSize.Width + borderWidth;
            _windowManager.MinHeight = AppTitleBar.ActualHeight + page.CompactMinSize.Height + borderHeight
                + CompactFloorBuffer;

            // the return key takes the size of the close button, all the caption area holds by now
            CompactReturnButton.Width = AppWindow.TitleBar.RightInset / scale;
            CompactReturnButton.Height = (AppWindow.TitleBar.Height / scale) + CompactReturnKeyOverhang;
            CompactReturnButton.Margin = new Thickness(0, -CompactReturnKeyOverhang, 0, 0);
            CompactReturnButton.Padding = new Thickness(0, CompactReturnKeyOverhang, 0, 0);

            // the page goes compact before the window shrinks, so no pass sees it small with full floors
            _isCompact = true;
            ShowCompactChrome(true);

            Size size = _compactSizes.TryGetValue(page.GetType(), out Size dragged) ? dragged : page.CompactStartSize;
            this.SetWindowSize(size.Width, size.Height);
            PinCompactWindow();
        }

        // back to the full window; bounds first, then the maximize, so a later restore gives the full size
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

        // top right of the work area of the current screen, on every way in; the gap is measured to the
        // drawn frame, so the invisible resize border is counted back in
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

        // compact keeps one row above the page: the return key and the close button
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

        // applies a theme to the content and the caption buttons; (those only follow PreferredTheme)
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
