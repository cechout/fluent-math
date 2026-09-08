using Calculator_WinUI.Models;
using Calculator_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

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
                    padding: 0 10px;
                    display: flex;
                    justify-content: flex-end; /* right aligned, like a calculator display */
                    align-items: center;
                    height: 100vh;
                    overflow: hidden;
                    color: var(--math-color);
                }

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

                /* the right edge is the anchor, so shrinking to fit leaves the newest input in place */
                #math-container {
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
                }

                /* the input cursor, tagged by the LaTeX the engine emits; that LaTeX only reserves the
                   space, the visible bar is the border below, which is what makes the caret follow the
                   font size of the slot it stands in */
                @keyframes cursor-blink {
                    0%, 49% { opacity: 1; }
                    50%, 100% { opacity: 0; }
                }
                .cursor {
                    display: inline-block;
                    width: 0;
                    height: var(--cursor-height);
                    border-left: var(--cursor-width) solid currentColor;
                    vertical-align: var(--cursor-shift);
                    animation: cursor-blink 1.1s infinite;
                }
            </style>
        </head>
        <body>
            <div id='math-container'></div>
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
                }

                // called from C# whenever the theme changes, so the page never has to be reloaded
                function applyStyle(cssText) {
                    document.getElementById('style-set').textContent = cssText;
                    fitToBox();
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

                    const available = document.documentElement.clientHeight - 2;
                    const needed = container.getBoundingClientRect().height;
                    if (needed <= 0 || needed <= available) return;

                    const floor = parseFloat(getComputedStyle(document.documentElement)
                        .getPropertyValue('--min-fit-scale')) || 0.45;

                    let scale = available / needed;
                    if (scale < floor) scale = floor;
                    container.style.transform = 'scale(' + scale + ')';
                }

                // a KaTeX font is only fetched once a glyph actually needs it, which on the history line
                // is the moment the first result appears; asking for them up front takes that fetch out
                // of the first render, where it shows as the line settling into shape a moment late
                function warmFonts() {
                    if (!document.fonts || !document.fonts.load) return;

                    var families = ['KaTeX_Main', 'KaTeX_SansSerif', 'KaTeX_Math', 'KaTeX_Size1', 'KaTeX_Size2'];
                    families.forEach(function (family) { document.fonts.load('1em ' + family); });
                }

                warmFonts();

                // a formula measured before its fonts arrived is measured at the wrong height, so the
                // fit is taken again once they are in
                if (document.fonts && document.fonts.ready) document.fonts.ready.then(fitToBox);
            </script>
        </body>
        </html>";

        // rebuilt on every theme change, so they are fields rather than locals in the load handler
        private MathDisplayStyle _historyStyle;
        private MathDisplayStyle _inputStyle;


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new StandardViewModel();
            this.InitializeComponent();

            RebuildStyles();

            this.Loaded += StandardPage_Loaded;
            this.ActualThemeChanged += StandardPage_ActualThemeChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        // === webview setup ===

        private async void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await MathWebView1.EnsureCoreWebView2Async();
            await MathWebView2.EnsureCoreWebView2Async();

            // the page sits in the tree by now, so ActualTheme finally answers with the theme in force
            RebuildStyles();

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


        // === theming ===

        // the formula lives in a browser, which never hears about the WinUI theme, so the colors are
        // pushed in again from here; re-navigating instead would flash and re-fetch KaTeX from the CDN
        private async void StandardPage_ActualThemeChanged(FrameworkElement sender, object args)
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
