using FluentMath.Models;
using FluentMath.Persistence.Services;
using Microsoft.UI.Xaml;

namespace FluentMath
{
    // the entry point; one window, and no activation beyond a plain launch
    public partial class App : Application
    {
        private Window? _window;

        // the calculator setup for the session; every page holds this one object
        public static CalculatorSettings Settings => SettingsService.Instance.Calculator;

        public App()
        {
            InitializeComponent();

            PersistenceService.Initialize(AppDataFolder.Resolve());

            // MainWindow flushes on close; a crash would drop what still waits on the debounce
            // (a kill cannot be covered, nothing managed runs then)
            this.UnhandledException += (s, e) => PersistenceService.Instance.FlushAll();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // everything on disk is loaded before the first window reads a default
            SettingsService.Instance.LoadFromData(PersistenceService.Instance.LoadSettings());
            WindowStateService.Instance.LoadFromDisk(PersistenceService.Instance.LoadWindowStates());
            PageStateService.Instance.LoadFromDisk(PersistenceService.Instance.LoadPageState());

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
