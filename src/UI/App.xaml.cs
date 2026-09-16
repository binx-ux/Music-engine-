using System.Windows;
using System.Windows.Threading;
using Mixline.Core;

namespace Mixline.App;

public partial class App : Application
{
    public static AppSession Session { get; private set; } = null!;
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
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

        Session = new AppSession();
        base.OnStartup(e);
        Session.StartEngineIfNeeded();
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Session.Log.Error("ui", "The window hit an error. Audio should keep running.", e.Exception);
        Session.SetError("Something went wrong in the window.", e.Exception.Message);
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
