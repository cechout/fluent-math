using Microsoft.UI.Xaml;

namespace Calculator_WinUI
{
    // application entry point; the app owns exactly one window and handles no activation kind
    // beyond a plain launch
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
