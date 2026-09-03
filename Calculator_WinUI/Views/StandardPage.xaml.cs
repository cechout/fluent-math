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

        // one page per display line, differing only in color and font size, which are stamped in from C#
        // through the two placeholders
        private readonly string _kaTeXHtmlTemplate = @"
        <!DOCTYPE html>
        <html>
        <head>
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/katex@0.16.8/dist/katex.min.css'>
            <script src='https://cdn.jsdelivr.net/npm/katex@0.16.8/dist/katex.min.js'></script>
            <style>
                body {
                    margin: 0;
                    padding: 0 10px;
                    display: flex;
                    justify-content: flex-end; /* right aligned, like a calculator display */
                    align-items: center;
                    height: 100vh;
                    overflow: hidden;
                    color: [COLOR]; /* replaced from C# */
                    font-size: [SIZE]; /* replaced from C# */
                }

                /* the input cursor, tagged by the LaTeX the engine emits */
                @keyframes cursor-blink {
                    0%, 49% { opacity: 1; }
                    50%, 100% { opacity: 0; }
                }
                .cursor {
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
                        displayMode: true,
                        // the cursor arrives as \htmlClass, which KaTeX drops unless both of these are set
                        trust: true,
                        strict: false
                    });
                }
            </script>
        </body>
        </html>";


        // === constructor ===

        public StandardPage()
        {
            ViewModel = new StandardViewModel();
            this.InitializeComponent();

            this.Loaded += StandardPage_Loaded;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        // === webview setup ===

        private async void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await MathWebView1.EnsureCoreWebView2Async();
            await MathWebView2.EnsureCoreWebView2Async();

            // the JS function does not exist until the page finished loading, so the starting value can
            // only be pushed from here; the history line needs no initial push, it stays empty until the
            // first calculation
            MathWebView2.NavigationCompleted += async (s, args) =>
            {
                await UpdateWebViewMath(MathWebView2, ViewModel.InputAndResultText);
            };

            // top line: the previous calculation, smaller and dimmed
            string html1 = _kaTeXHtmlTemplate.Replace("[COLOR]", "gray").Replace("[SIZE]", "18px");
            MathWebView1.NavigateToString(html1);

            // bottom line: what is being typed right now
            string html2 = _kaTeXHtmlTemplate.Replace("[COLOR]", "white").Replace("[SIZE]", "36px");
            MathWebView2.NavigateToString(html2);
        }


        // === rendering ===

        // a WebView2 cannot be bound to, so the two display lines are updated by hand from the property
        // change instead of through x:Bind
        private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (MathWebView1.CoreWebView2 == null || MathWebView2.CoreWebView2 == null) return;

            if (e.PropertyName == nameof(ViewModel.CalculationText))
            {
                await UpdateWebViewMath(MathWebView1, ViewModel.CalculationText);
            }
            else if (e.PropertyName == nameof(ViewModel.InputAndResultText))
            {
                await UpdateWebViewMath(MathWebView2, ViewModel.InputAndResultText);
            }
        }

        // the LaTeX is pasted into a JS string literal, so backslashes and quotes have to survive that
        // trip; LaTeX is almost entirely backslashes, which makes this mandatory rather than defensive
        private async Task UpdateWebViewMath(WebView2 webView, string mathText)
        {
            if (string.IsNullOrEmpty(mathText)) mathText = " "; // an empty string breaks the render call
            string escapedMath = mathText.Replace("\\", "\\\\").Replace("'", "\\'");
            await webView.ExecuteScriptAsync($"updateMath('{escapedMath}');");
        }
    }
}
