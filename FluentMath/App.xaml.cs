using FluentMath.Models;
using Microsoft.UI.Xaml;

namespace FluentMath
{
    // the entry point; one window, and no activation beyond a plain launch
    public partial class App : Application
    {
        private Window? _window;

        // the calculator setup for the session; every page holds this one object
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
