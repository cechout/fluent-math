using Calculator_WinUI.Models;
using Calculator_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace Calculator_WinUI.Views
{
    // the calculator display; everything else on the page is buttons bound straight to the ViewModel
    //
    // XAML has no way to draw a stacked fraction or a real root sign, so the formula is rendered by KaTeX
    // in a WebView2 instead of by a TextBlock; the ViewModel already produces LaTeX, this page only pushes
    // it into the browser
    public sealed partial class StandardPage : Page
    {
        public StandardViewModel ViewModel { get; }

        // one page per display line; everything the two lines differ in arrives as the css block that
        // replaces the single placeholder, see MathDisplayStyle for the knobs behind it
        private readonly string _kaTeXHtmlTemplate = @"
        <!DOCTYPE html>
        <html>
        <head>
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/katex@0.16.8/dist/katex.min.css'>
            <script src='https://cdn.jsdelivr.net/npm/katex@0.16.8/dist/katex.min.js'></script>

            <style id='style-set'>[STYLE]</style> <!-- replaced from C#, rewritten on a theme change -->

            <style>
                body {
                    margin: 0;
                    height: 100vh;
                    overflow: hidden;
                    color: var(--math-color);
                }

                /* the scrolling deliberately sits on a div rather than on the body: overflow set on
                   the body while the html element is still visible propagates to the viewport, and
                   the element that then really scrolls is documentElement, not the one styled here
                   it also carries no padding of its own, because a scroll containers padding counts
                   towards its scrollable area and would report an overflow of exactly that much even
                   with nothing to scroll to; the breathing room sits on the content instead */
                #scroll-area {
                    height: 100%;
                    display: flex;
                    align-items: center;
                    overflow-x: auto;
                    overflow-y: hidden;
                }

                /* the bar the user sees is a WinUI ScrollBar sitting over this page, so Chromium draws
                   none of its own; the area still scrolls, by wheel and from the host */
                #scroll-area::-webkit-scrollbar { display: none; }

                /* KaTeX puts its own 1.21em on top of whatever it inherits, so the size is set here
                   instead of on the body to make the value coming from C# the one on screen */
                .katex {
                    font-size: var(--math-size);
                    line-height: var(--math-line-height);
                }

                /* KaTeX writes the bar as an inline border-bottom-width, which only !important beats */
                .katex .mfrac .frac-line {
                    border-bottom-width: var(--frac-bar) !important;
                }

                /* right aligned like a calculator display, but through an auto margin rather than
                   justify-content: a flex item pushed over by justify-content cannot be scrolled back
                   to on its start side, the overflow there stays unreachable
                   the right edge is also the anchor for shrinking to fit, so that leaves the newest
                   input in place */
                #math-container {
                    margin-left: auto;
                    padding: 0 10px;
                    transform-origin: 100% 50%;
                }

                /* one class per structured token, tagged by the LaTeX the engine emits */
                .m-frac { font-size: var(--frac-scale); }
                .m-pow { font-size: var(--pow-scale); }
                .m-root { font-size: var(--root-scale); }
                .m-log { font-size: var(--log-scale); }
                .m-func { font-size: var(--func-scale); }

                /* the engine hands operators over as ordinary atoms, so KaTeX adds no space of its own
                   and the entire gap around + - and the two symbols is the one set here */
                .m-op {
                    font-size: var(--op-scale);
                    padding: 0 var(--op-gap);
                    position: relative;
                    bottom: var(--op-raise); /* purely visual, it takes no part in the layout */
                }

                /* the descendants are named as well, because KaTeX puts the glyph in a span of its
                   own and a weight set there would win over one inherited from the wrapper */
                .m-op, .m-op * {
                    font-weight: var(--op-weight);
                }

                /* the input cursor, tagged by the LaTeX the engine emits
                   overflow is what makes this exact: the baseline of an inline-block is normally the
                   baseline of the line box inside it, which moves around with the content and the
                   line height, but a box whose overflow is not visible has its baseline pinned to its
                   own bottom margin edge, so the bar sits on the baseline of its slot and on nothing
                   else, at every nesting depth
                   the bar is a background rather than a border, and the two half margins take its
                   width straight back again, so the caret costs no space and sits centred on the gap
                   it marks; a border cannot do that, Blink floors a border width to whole pixels
                   while the cancelling margin stays fractional, which measured as half a pixel of
                   drift on every neighbour */
                @keyframes cursor-blink {
                    0%, 49% { opacity: 1; }
                    50%, 100% { opacity: 0; }
                }
                .cursor {
                    display: inline-block;
                    overflow: hidden;
                    width: var(--cursor-width);
                    height: var(--cursor-height);
                    margin-left: calc(-0.5 * var(--cursor-width));
                    margin-right: calc(-0.5 * var(--cursor-width));
                    background: var(--cursor-color);
                    border-radius: var(--cursor-radius);
                    vertical-align: var(--cursor-shift);
                    animation: cursor-blink 1.1s infinite;
                }
            </style>
        </head>
        <body>
            <div id='scroll-area'><div id='math-container'></div></div>
            <script>
                // called from C# on every keypress
                function updateMath(latexString) {
                    katex.render(latexString, document.getElementById('math-container'), {
                        throwOnError: false, // half-typed input is normal here, show it raw instead of failing
                        // text style, not display style; display style is what puts the wide clearances
                        // above and below a fraction bar and wraps the output in a 1em vertical margin
                        displayMode: false,
                        // the cursor arrives as \htmlClass, which KaTeX drops unless both of these are set
                        trust: true,
                        strict: false
                    });
                    fitToBox();
                    revealCursor();
                    reportScroll();
                }

                // called from C# whenever the theme changes, so the page never has to be reloaded
                function applyStyle(cssText) {
                    document.getElementById('style-set').textContent = cssText;
                    fitToBox();
                    revealCursor();
                    reportScroll();
                }

                // the display box is a fixed height while a formula is not, so anything taller than the
                // box is scaled down instead of being cut off at the top and bottom; a stacked fraction
                // needs roughly twice the height of a plain line and already outgrows it on its own
                //
                // the scale never goes below --min-fit-scale, past that point the formula would be
                // shrunk into something nobody can read anyway
                function fitToBox() {
                    const container = document.getElementById('math-container');
                    container.style.transform = 'none';

                    // the scroll areas client height already excludes the horizontal scrollbar, so a
                    // visible bar cannot make the formula believe it has more room than it has
                    const available = document.getElementById('scroll-area').clientHeight - 2;
                    const needed = container.getBoundingClientRect().height;
                    if (needed <= 0 || needed <= available) return;

                    const floor = parseFloat(getComputedStyle(document.documentElement)
                        .getPropertyValue('--min-fit-scale')) || 0.45;

                    let scale = available / needed;
                    if (scale < floor) scale = floor;
                    container.style.transform = 'scale(' + scale + ')';
                }

                // width is scrolled rather than scaled, so the caret has to be pulled back into view
                // after every keypress, otherwise typing past the right edge types out of sight
                //
                // a margin is kept around it so it never ends up flush against an edge with no context
                // left or right of it; the history line has no caret and simply falls out here
                function revealCursor() {
                    const caret = document.querySelector('.cursor');
                    if (!caret) return;

                    const area = document.getElementById('scroll-area');
                    const margin = 24;
                    const box = caret.getBoundingClientRect();
                    const view = area.getBoundingClientRect();

                    if (box.left < view.left + margin) {
                        area.scrollLeft -= (view.left + margin) - box.left;
                    }
                    else if (box.right > view.right - margin) {
                        area.scrollLeft += box.right - (view.right - margin);
                    }
                }

                // the host owns the visible scrollbar, so the page tells it what to show and takes a
                // position back the same way
                function reportScroll() {
                    const area = document.getElementById('scroll-area');
                    window.chrome.webview.postMessage('scroll:' + Math.round(area.scrollLeft)
                        + ':' + Math.round(area.scrollWidth)
                        + ':' + Math.round(area.clientWidth));
                }

                function setScrollLeft(value) {
                    document.getElementById('scroll-area').scrollLeft = value;
                }

                document.getElementById('scroll-area').addEventListener('scroll', reportScroll);

                // a KaTeX font is only fetched once a glyph actually needs it, which on the history line
                // is the moment the first result appears; asking for them up front takes that fetch out
                // of the first render, where it shows as the line settling into shape a moment late
                function warmFonts() {
                    if (!document.fonts || !document.fonts.load) return;

                    var families = ['KaTeX_Main', 'KaTeX_SansSerif', 'KaTeX_Math', 'KaTeX_Size1', 'KaTeX_Size2'];
                    families.forEach(function (family) { document.fonts.load('1em ' + family); });
                }

                warmFonts();

                // a click anywhere in the formula puts the cursor there
                //
                // every token carries the address of the position it begins at, so the token that was
                // hit plus which half of it was hit is enough to name a position; structures nest, and
                // the smallest box containing the point is the innermost token, which is the one meant
                document.addEventListener('click', function (event) {
                    const targets = document.querySelectorAll('[data-p]');
                    if (targets.length === 0) return;

                    let inner = null;
                    let innerArea = Infinity;
                    let nearest = null;
                    let nearestGap = Infinity;

                    for (const element of targets) {
                        const box = element.getBoundingClientRect();
                        const hit = event.clientX >= box.left && event.clientX <= box.right
                            && event.clientY >= box.top && event.clientY <= box.bottom;

                        if (hit) {
                            const area = box.width * box.height;
                            if (area < innerArea) { inner = element; innerArea = area; }
                            continue;
                        }

                        // a click in the empty space beside the formula still has to land somewhere,
                        // which for a right aligned display is most of the box
                        const dx = Math.max(box.left - event.clientX, event.clientX - box.right, 0);
                        const dy = Math.max(box.top - event.clientY, event.clientY - box.bottom, 0);
                        const gap = dx * dx + dy * dy;
                        if (gap < nearestGap) { nearest = element; nearestGap = gap; }
                    }

                    const target = inner || nearest;
                    if (!target) return;

                    const box = target.getBoundingClientRect();
                    const address = target.dataset.p;
                    const at = address.lastIndexOf('@');

                    let index = parseInt(address.substring(at + 1), 10);
                    if (event.clientX > box.left + box.width / 2) index++;

                    window.chrome.webview.postMessage('cursor:' + address.substring(0, at) + '@' + index);
                });

                // a formula measured before its fonts arrived is measured at the wrong height, so the
                // fit is taken again once they are in
                if (document.fonts && document.fonts.ready) document.fonts.ready.then(fitToBox);
            </script>
        </body>
        </html>";

        // rebuilt on every theme change, so they are fields rather than locals in the load handler
        private MathDisplayStyle _historyStyle;
        private MathDisplayStyle _inputStyle;

        // the source of the accent color the caret uses; held in a field rather than created where it
        // is needed, because a collected UISettings silently stops raising ColorValuesChanged
        private readonly UISettings _uiSettings = new UISettings();

        // what the page puts in front of a message to say which kind it is
        private const string CursorMessage = "cursor:";
        private const string ScrollMessage = "scroll:";


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new StandardViewModel();
            this.InitializeComponent();

            RebuildStyles();

            this.Loaded += StandardPage_Loaded;
            this.Unloaded += StandardPage_Unloaded;
            this.ActualThemeChanged += StandardPage_ActualThemeChanged;
            _uiSettings.ColorValuesChanged += StandardPage_ColorValuesChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        // === webview setup ===

        private async void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await MathWebView1.EnsureCoreWebView2Async();
            await MathWebView2.EnsureCoreWebView2Async();

            // the page sits in the tree by now, so ActualTheme finally answers with the theme in force
            RebuildStyles();

            // only the input line talks back; the history line carries neither a cursor nor a scrollbar
            MathWebView2.CoreWebView2.WebMessageReceived += MathWebView2_WebMessageReceived;
            InputScrollBar.Scroll += InputScrollBar_Scroll;

            // the JS function does not exist until the page finished loading, so the starting value can
            // only be pushed from here
            //
            // the history line is pushed too although it has nothing to show yet; without it its first
            // render would be the one = triggers, and that single call would still be waiting for KaTeX
            // to arrive from the CDN, which is visible as the line settling a moment after the result
            MathWebView1.NavigationCompleted += async (s, args) =>
            {
                await UpdateWebViewMath(MathWebView1, ViewModel.CalculationText, _historyStyle);
            };

            MathWebView2.NavigationCompleted += async (s, args) =>
            {
                await UpdateWebViewMath(MathWebView2, ViewModel.InputAndResultText, _inputStyle);
            };

            // top line: the previous calculation, smaller and dimmed
            string html1 = _kaTeXHtmlTemplate.Replace("[STYLE]", _historyStyle.ToCssBlock());
            MathWebView1.NavigateToString(html1);

            // bottom line: what is being typed right now
            string html2 = _kaTeXHtmlTemplate.Replace("[STYLE]", _inputStyle.ToCssBlock());
            MathWebView2.NavigateToString(html2);
        }


        // the settings page can leave and come back, so the subscription on the shared UISettings has
        // to go with the page that made it
        private void StandardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _uiSettings.ColorValuesChanged -= StandardPage_ColorValuesChanged;
        }


        // === messages from the page ===

        // two kinds arrive, both as plain strings: where a click landed, and what the horizontal
        // scroll currently looks like
        private void MathWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string message = args.TryGetWebMessageAsString();

            if (message.StartsWith(CursorMessage))
            {
                ViewModel.PlaceCursor(message.Substring(CursorMessage.Length));
                return;
            }

            if (message.StartsWith(ScrollMessage)) ShowScrollState(message.Substring(ScrollMessage.Length));
        }

        // the formula scrolls inside the page, and this mirrors that onto the scrollbar the user sees
        //
        // a WebView2 draws a Chromium scrollbar, which follows neither the Fluent theme nor the system
        // setting for it, so that one is hidden and a real WinUI ScrollBar shows the same numbers
        private void ShowScrollState(string state)
        {
            string[] parts = state.Split(':');
            if (parts.Length != 3) return;

            if (!TryReadPixels(parts[0], out double offset)) return;
            if (!TryReadPixels(parts[1], out double content)) return;
            if (!TryReadPixels(parts[2], out double viewport)) return;

            // under a pixel of difference is rounding in the page, not something to scroll
            double overflow = content - viewport;
            if (overflow <= 1)
            {
                InputScrollBar.Visibility = Visibility.Collapsed;
                return;
            }

            InputScrollBar.Maximum = overflow;
            InputScrollBar.ViewportSize = viewport;
            InputScrollBar.Value = offset;
            InputScrollBar.Visibility = Visibility.Visible;
        }

        // dragging the scrollbar is the one direction the page does not drive itself
        //
        // setting Value above raises ValueChanged but not Scroll, so pushing the position back here
        // cannot loop with the report that caused it
        private async void InputScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (MathWebView2.CoreWebView2 == null) return;

            await MathWebView2.ExecuteScriptAsync(
                $"setScrollLeft({e.NewValue.ToString(CultureInfo.InvariantCulture)});");
        }

        private static bool TryReadPixels(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }


        // === extra functions flyout ===

        // the keys in the flyout carry their own Command, so this only closes the flyout behind them;
        // without it the panel would stay open over the display after every function
        private void ExtraFunction_Click(object sender, RoutedEventArgs e)
        {
            ExtraFunctionsFlyout.Hide();
        }


        // === theming ===

        private async void StandardPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            await PushStyles();
        }

        // the caret follows the Windows accent, which can be changed while the app is running
        //
        // the event arrives on a background thread, so everything it touches has to be marshalled back
        // first; the WebView2 would throw on a wrong-thread call
        private void StandardPage_ColorValuesChanged(UISettings sender, object args)
        {
            DispatcherQueue.TryEnqueue(async () => await PushStyles());
        }

        // the formula lives in a browser, which never hears about the WinUI theme, so the colors are
        // pushed in again from here; re-navigating instead would flash and re-fetch KaTeX from the CDN
        private async Task PushStyles()
        {
            RebuildStyles();

            if (MathWebView1.CoreWebView2 == null || MathWebView2.CoreWebView2 == null) return;

            await ApplyWebViewStyle(MathWebView1, _historyStyle);
            await ApplyWebViewStyle(MathWebView2, _inputStyle);
        }

        private void RebuildStyles()
        {
            _historyStyle = MathDisplayStyle.ForHistoryLine(this.ActualTheme);
            _inputStyle = MathDisplayStyle.ForInputLine(this.ActualTheme);

            // the one knob that cannot travel as css, because display style is a decision KaTeX makes
            // while parsing rather than something a stylesheet can reach afterwards
            ViewModel.UseDisplayFractions = _inputStyle.UseDisplayFractions;
        }

        // a template literal rather than a quoted string, so the css can arrive on several lines
        private async Task ApplyWebViewStyle(WebView2 webView, MathDisplayStyle style)
        {
            await webView.ExecuteScriptAsync($"applyStyle(`{style.ToCssBlock()}`);");
        }


        // === rendering ===

        // a WebView2 cannot be bound to, so the two display lines are updated by hand from the property
        // change instead of through x:Bind
        private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (MathWebView1.CoreWebView2 == null || MathWebView2.CoreWebView2 == null) return;

            if (e.PropertyName == nameof(ViewModel.CalculationText))
            {
                await UpdateWebViewMath(MathWebView1, ViewModel.CalculationText, _historyStyle);
            }
            else if (e.PropertyName == nameof(ViewModel.InputAndResultText))
            {
                await UpdateWebViewMath(MathWebView2, ViewModel.InputAndResultText, _inputStyle);
            }
        }

        // the LaTeX is pasted into a JS string literal, so backslashes and quotes have to survive that
        // trip; LaTeX is almost entirely backslashes, which makes this mandatory rather than defensive
        private async Task UpdateWebViewMath(WebView2 webView, string mathText, MathDisplayStyle style)
        {
            if (string.IsNullOrEmpty(mathText)) mathText = " "; // an empty string breaks the render call

            string styledMath = style.WrapLatex(mathText);
            string escapedMath = styledMath.Replace("\\", "\\\\").Replace("'", "\\'");
            await webView.ExecuteScriptAsync($"updateMath('{escapedMath}');");
        }
    }
}
