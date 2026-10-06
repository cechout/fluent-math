using FluentMath.Distribution;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;
using FluentMath.Views;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
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
using WindowState = FluentMath.Persistence.Models.WindowState;

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

        private const string WindowKey = "Main"; // in window-state.json

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

        // the full window bounds for the way back; (the dragged compact size per page is in PageStateService)
        private bool _isCompact;
        private RectInt32 _fullBounds;
        private bool _fullWasMaximized;

        // --- title bar ---
        private const double TitleBarDeactivatedOpacity = 0.5; // TitleBarDeactivatedOpacity of the WinUI TitleBar


        // === constructor ===

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;
            this.AppWindow.SetIcon("Assets\\Icon\\Icon.ico");

            // opens on the start page from the settings; its item is found by tag (the list starts with a header)
            string startTag = SettingsService.Instance.StartupPage.ToString();
            ShowPage(PageForTag(startTag) ?? typeof(StandardPage));
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(item => (string)item.Tag == startTag);

            // our own title bar in the client area; transparent caption buttons, so the Mica shows through
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            // the whole bar drags the window; its buttons get passthrough rects, see UpdateTitleBarPassthrough
            this.SetTitleBar(AppTitleBar);
            this.Activated += MainWindow_Activated;

            ApplyTheme(SettingsService.Instance.AppTheme);
            SettingsService.Instance.ThemeChanged += ApplyTheme;

            // a floor that keeps the keypad in the window, then the saved bounds or the start size
            _windowManager = WindowManager.Get(this);
            _windowManager.MinWidth = FullMinWidth;
            _windowManager.MinHeight = FullMinHeight;
            RestoreWindowState();

            AppWindow.Changed += AppWindow_Changed;
            this.Closed += MainWindow_Closed;

            // last; the check runs in the background and only ever adds the pill
            UpdateService.Instance.UpdateStateChanged += ShowUpdatePill;
            UpdateService.Instance.Start(this.GetWindowHandle());
        }


        // === navigation ===

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            Type? page = PageForTag(args.InvokedItemContainer.Tag.ToString());

            // a second click on the item already shown would navigate the page onto itself
            if (page == null || MainFrame.CurrentSourcePageType == page) return;

            ShowPage(page);
        }

        private static Type? PageForTag(string? itemTag)
        {
            return itemTag switch
            {
                "Standard" => typeof(StandardPage),
                "Scientific" => typeof(ScientificPage),
                "Currency" => typeof(CurrencyPage),
                "Volume" => typeof(VolumePage),
                "Length" => typeof(LengthPage),
                "Settings" => typeof(SettingsPage),
                _ => null
            };
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

            CompactSize? dragged = PageStateService.Instance.GetCompactSize(page.GetType().Name);
            Size size = dragged != null ? new Size(dragged.Width, dragged.Height) : page.CompactStartSize;
            this.SetWindowSize(size.Width, size.Height);
            PinCompactWindow();
        }

        // back to the full window; bounds first, then the maximize, so a later restore gives the full size
        public void ExitCompactMode()
        {
            if (!_isCompact) return;

            SaveCompactSize();

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

            AppTitleIcon.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            AppTitleText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            CompactReturnButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            ShowUpdatePill();

            if (MainFrame.Content is ICompactPage page)
            {
                page.SetCompactLayout(compact);
            }
        }

        private void CompactReturnButton_Click(object sender, RoutedEventArgs e)
        {
            ExitCompactMode();
        }

        // the size the current page is dragged to while compact, for its next entry
        // (the scale from the window, which still answers while it closes)
        private void SaveCompactSize()
        {
            double scale = this.GetDpiForWindow() / 96.0;
            PageStateService.Instance.SetCompactSize(MainFrame.Content.GetType().Name,
                AppWindow.Size.Width / scale, AppWindow.Size.Height / scale);
        }


        // === title bar ===

        // the bar dims while another window is active, as the WinUI TitleBar does
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            bool inactive = args.WindowActivationState == WindowActivationState.Deactivated;

            AppTitleText.Opacity = inactive ? TitleBarDeactivatedOpacity : 1;
            AppTitleIcon.Opacity = inactive ? TitleBarDeactivatedOpacity : 1;
            UpdatePillButton.Opacity = inactive ? TitleBarDeactivatedOpacity : 1;
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarLayout();

        // the pill and the return key appear without the bar changing size
        private void TitleBarElement_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarLayout();

        private void UpdateTitleBarLayout()
        {
            FitTitle();
            UpdateTitleBarPassthrough();
        }

        // the title trims rather than push the pill under the caption buttons in a narrow window
        private void FitTitle()
        {
            if (Content?.XamlRoot == null) return;

            double captionButtons = AppWindow.TitleBar.RightInset / Content.XamlRoot.RasterizationScale;

            double others = AppTitleText.Margin.Left + AppTitleText.Margin.Right;
            for (int column = 0; column < AppTitleBar.ColumnDefinitions.Count; column++)
            {
                GridLength width = AppTitleBar.ColumnDefinitions[column].Width;
                if (width.IsAbsolute || (width.IsAuto && column != Grid.GetColumn(AppTitleText)))
                {
                    others += AppTitleBar.ColumnDefinitions[column].ActualWidth;
                }
            }

            AppTitleText.MaxWidth = Math.Max(0, AppTitleBar.ActualWidth - captionButtons - others);
        }

        // SetTitleBar makes the whole bar drag the window, so every button in it needs a passthrough rect
        // in window pixels; again whenever one of them appears, goes or moves
        private void UpdateTitleBarPassthrough()
        {
            if (Content?.XamlRoot == null) return;

            double scale = Content.XamlRoot.RasterizationScale;
            var rects = new List<RectInt32>();

            foreach (FrameworkElement element in new FrameworkElement[] { CompactReturnButton, UpdatePillButton })
            {
                if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0) continue;

                Rect bounds = element.TransformToVisual(null).TransformBounds(
                    new Rect(0, 0, element.ActualWidth, element.ActualHeight));

                rects.Add(new RectInt32(
                    (int)Math.Round(bounds.X * scale),
                    (int)Math.Round(bounds.Y * scale),
                    (int)Math.Round(bounds.Width * scale),
                    (int)Math.Round(bounds.Height * scale)));
            }

            InputNonClientPointerSource.GetForWindowId(AppWindow.Id)
                .SetRegionRects(NonClientRegionKind.Passthrough, rects.ToArray());
        }


        // === update ===

        // the pill names the waiting version; a store update GitHub could not name yet still needs a label
        private void ShowUpdatePill()
        {
            UpdateService service = UpdateService.Instance;

            string versionLabel = UpdateService.VersionLabel(service.Latest?.Version);
            UpdatePillText.Text = versionLabel.Length > 0 ? versionLabel : "Update";

            UpdatePillButton.Visibility = service.IsUpdateAvailable && !_isCompact ? Visibility.Visible : Visibility.Collapsed;

            // after layout; a button that just went measures nothing and raises no SizeChanged
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, UpdateTitleBarLayout);
        }

        private async void UpdatePillButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDialog.ShowAsync(Content.XamlRoot, UpdateService.Instance.Latest);
        }

        // the update script waits for this process to end; Closed flushes what still waits to be saved
        public void ExitForUpdate()
        {
            this.Close();
        }


        // === window state ===

        // the saved bounds when they still overlap a monitor, else the start size; a saved maximize either way
        private void RestoreWindowState()
        {
            WindowState? saved = WindowStateService.Instance.GetState(WindowKey);
            RectInt32 bounds = saved != null ? new RectInt32(saved.X, saved.Y, saved.Width, saved.Height) : default;

            if (bounds.Width > 0 && bounds.Height > 0 && IsOnScreen(bounds))
            {
                AppWindow.MoveAndResize(bounds);
            }
            else
            {
                this.SetWindowSize(FullStartWidth, FullStartHeight);
            }

            if (saved?.IsMaximized == true) ((OverlappedPresenter)AppWindow.Presenter).Maximize();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange) SaveWindowState();
        }

        // skipped while minimized or hidden, whose rect means nothing; a maximized window keeps the rect it
        // returns to, and a compact one the full bounds kept for the way back, so a restart opens full
        private void SaveWindowState()
        {
            OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
            if (presenter.State == OverlappedPresenterState.Minimized || !AppWindow.IsVisible) return;

            WindowState state;
            if (_isCompact)
            {
                state = StateOf(_fullBounds, _fullWasMaximized);
            }
            else if (presenter.State == OverlappedPresenterState.Maximized)
            {
                WindowState? existing = WindowStateService.Instance.GetState(WindowKey);
                state = existing != null
                    ? new WindowState { X = existing.X, Y = existing.Y, Width = existing.Width, Height = existing.Height }
                    : new WindowState();
                state.IsMaximized = true;
            }
            else
            {
                state = StateOf(new RectInt32(AppWindow.Position.X, AppWindow.Position.Y,
                    AppWindow.Size.Width, AppWindow.Size.Height), false);
            }

            WindowStateService.Instance.SetState(WindowKey, state);
        }

        private static WindowState StateOf(RectInt32 bounds, bool isMaximized)
        {
            return new WindowState
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = isMaximized
            };
        }

        // a monitor taken away, or arranged differently, can leave a saved rect on none of them
        // (indexed, since a foreach over FindAll throws an InvalidCastException in the WinRT projection)
        private static bool IsOnScreen(RectInt32 bounds)
        {
            var displayAreas = DisplayArea.FindAll();
            for (int i = 0; i < displayAreas.Count; i++)
            {
                RectInt32 work = displayAreas[i].WorkArea;
                if (bounds.X < work.X + work.Width && bounds.X + bounds.Width > work.X
                    && bounds.Y < work.Y + work.Height && bounds.Y + bounds.Height > work.Y)
                {
                    return true;
                }
            }

            return false;
        }

        // whatever still waits on a debounce goes to disk before the process ends; a compact window saves
        // its size here, since otherwise only the way back to full does
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_isCompact) SaveCompactSize();

            PersistenceService.Instance.FlushAll();
        }


        // === theming ===

        // applies a theme to the content and the caption buttons; (those only follow PreferredTheme)
        private void ApplyTheme(string themeTag)
        {
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
