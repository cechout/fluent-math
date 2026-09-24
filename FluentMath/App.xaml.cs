using FluentMath.Models;
using Microsoft.UI.Xaml;

namespace FluentMath
{
    // application entry point; the app owns exactly one window and handles no activation kind
    // beyond a plain launch
    public partial class App : Application
    {
        private Window? _window;

        // the calculator setup for the whole session; the calculator pages and the settings page all hold on
        // to this one object, so a change on one page is what every other page reads
        public static CalculatorSettings Settings { get; } = new CalculatorSettings();

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
