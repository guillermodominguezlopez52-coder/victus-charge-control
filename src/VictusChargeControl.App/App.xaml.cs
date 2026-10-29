using System.Threading;
using System.Windows;
using VictusChargeControl.App.Platform;
using VictusChargeControl.App.Services;
using VictusChargeControl.App.Tray;
using VictusChargeControl.App.Views;
using VictusChargeControl.Core.Notifications;

namespace VictusChargeControl.App;

public partial class App : Application
{
    private const string MutexName = "VictusChargeControl.SingleInstance";
    private const string ShowEventName = "VictusChargeControl.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private BatteryApp? _controller;
    private TrayIconHost? _tray;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
            CrashLog.Write(args.Exception, "DispatcherUnhandledException");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                CrashLog.Write(exception, "AppDomain.UnhandledException");
            }
        };

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // Ya hay una instancia: pedirle que muestre la ventana y salir.
            try
            {
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // La otra instancia todavía no registró el evento; no hay nada más que hacer.
            }

            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);        ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => Dispatcher.BeginInvoke(ShowMainWindow),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        _tray = new TrayIconHost(ShowMainWindow, ShowSettings, ShowHistory, ExitApplication);
        _controller = new BatteryApp(_tray);
        _controller.NotificationFailed += (_, request) => ToastWindow.Show(request);

        _mainWindow = new MainWindow { DataContext = _controller.ViewModel };
        _mainWindow.SettingsRequested += (_, _) => ShowSettings();
        _mainWindow.HistoryRequested += (_, _) => ShowHistory();
        _mainWindow.RefreshRequested += async (_, _) =>
        {
            if (_controller is not null)
            {
                await _controller.RefreshAsync();
            }
        };
        _mainWindow.HiddenToTray += (_, _) => _tray?.Notify(new NotificationRequest(
            "Victus Charge Control",
            "La aplicación sigue en la bandeja del sistema. Usá el icono para abrirla o salir.",
            NotificationSeverity.Information));

        _controller.Start();

        var startHidden = e.Args.Any(arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase));
        if (!startHidden)
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void ShowSettings()
    {
        if (_controller is null)
        {
            return;
        }

        var dialog = new SettingsWindow(_controller.Settings);
        if (_mainWindow is { IsVisible: true })
        {
            dialog.Owner = _mainWindow;
        }

        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            _controller.ApplySettings(dialog.Result);
        }
    }

    private void ShowHistory()
    {
        if (_controller is null)
        {
            return;
        }

        var window = new HistoryWindow(_controller.GetHistoryRows());
        if (_mainWindow is { IsVisible: true })
        {
            window.Owner = _mainWindow;
        }

        window.ShowDialog();
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        _controller?.Shutdown();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _tray?.Dispose();
            _controller?.Dispose();
            _showEvent?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or ApplicationException)
        {
            // Cierre ya realizado por ExitApplication.
        }

        base.OnExit(e);
    }
}
