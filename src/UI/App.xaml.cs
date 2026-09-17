using System.Windows;
using System.Windows.Threading;
using Mixline.Core;

namespace Mixline.App;

public partial class App : Application
{
    public static AppSession Session { get; private set; } = null!;
    private Mutex? _mutex;
    private bool _ownsMutex;

    private async void Boot(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(true, AppInfo.MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("Cuebox is already running.", AppInfo.Name);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Session?.Log.Error("app", "Unhandled error.", ex);
        };

        var splash = new SplashWindow();
        splash.Show();

        try
        {
            await splash.Step("Reading settings", 0.22);
            Session = new AppSession();
            Theme.Apply(Session.Config.Appearance);

            await splash.Step("Starting audio", 0.58);
            Session.StartEngineIfNeeded();

            await splash.Step("Opening Cuebox", 0.88);
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
            await splash.Finish();
        }
        catch (Exception ex)
        {
            try { splash.Close(); } catch { }
            Session?.Log.Error("ui", "Cuebox failed to start.", ex);
            MessageBox.Show("Cuebox could not start.\n" + ex.Message, AppInfo.Name);
            Shutdown();
        }
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Session?.Log.Error("ui", "The window hit an error. Audio should keep running.", e.Exception);
        Session?.SetError("Something went wrong in the window.", e.Exception.Message);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Session?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
