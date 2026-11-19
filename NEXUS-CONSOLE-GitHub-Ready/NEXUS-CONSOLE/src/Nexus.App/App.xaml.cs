using System.Windows;
using System.Windows.Media.Animation;
using Nexus.Core.Config;
using Nexus.Core.Logging;
using Nexus.Shell.Services;

namespace Nexus.Shell;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\NexusConsole.SingleInstance", out var created);
        if (!created) { Shutdown(); return; }

        AppPaths.EnsureCreated();
        Log.Init(AppPaths.Logs);
        Log.Info($"Démarrage de NEXUS {typeof(App).Assembly.GetName().Version}");

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Exception d'interface non gérée", ex.Exception);
            MessageBox.Show("Un problème est survenu, mais NEXUS continue de fonctionner.\nLes détails sont dans les journaux.",
                "NEXUS", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error("Exception fatale", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Error("Tâche en échec", ex.Exception); ex.SetObserved(); };

        var settings = SettingsService.Create();
        // Mode économie : animations limitées à 30 images/s (à poser avant la première animation, donc au démarrage).
        if (settings.Current.LowMemory)
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(30));
        new MainWindow(settings).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("Arrêt de NEXUS");
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
