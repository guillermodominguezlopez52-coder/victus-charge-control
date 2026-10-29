using System.Windows;
using System.Windows.Threading;
using VictusChargeControl.App.Platform;
using VictusChargeControl.App.ViewModels;
using VictusChargeControl.Core.Alerts;
using VictusChargeControl.Core.History;
using VictusChargeControl.Core.Model;
using VictusChargeControl.Core.Notifications;
using VictusChargeControl.Core.Settings;
using VictusChargeControl.Core.Startup;

namespace VictusChargeControl.App.Services;

/// <summary>
/// Composición de la aplicación: monitor, alertas, historial, configuración y
/// notificaciones. La UI solo observa; toda la lógica vive acá y en Core.
/// </summary>
public sealed class BatteryApp : IDisposable
{
    private const int HistoryPageSize = 200;

    private readonly SettingsStore _settingsStore;
    private readonly HistoryStore _historyStore;
    private readonly HistoryRecorder _history;
    private readonly AlertEngine _alerts;
    private readonly BatteryMonitor _monitor;
    private readonly PowerCfgReportProvider _healthProvider;
    private readonly IAutoStart _autoStart;
    private readonly INotifier _notifier;
    private readonly DispatcherTimer _bannerTimer;

    private PowerModeWatcher? _powerWatcher;
    private AppSettings _settings;
    private bool _started;
    private bool _disposed;

    public BatteryApp(INotifier notifier)
    {
        _notifier = notifier;
        _settingsStore = new SettingsStore(SettingsStore.DefaultPath);
        _settings = _settingsStore.Load();
        _historyStore = new HistoryStore(HistoryStore.DefaultPath);
        _history = new HistoryRecorder(_historyStore, () => DateTimeOffset.Now);
        _alerts = new AlertEngine(ToThresholds(_settings));
        _healthProvider = new PowerCfgReportProvider();
        _monitor = new BatteryMonitor { PollInterval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds) };
        _autoStart = new RunKeyAutoStart(BuildAutoStartCommand());

        ViewModel = new MainViewModel(_settings);

        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _bannerTimer.Tick += (_, _) =>
        {
            _bannerTimer.Stop();
            ViewModel.HideBanner();
        };
    }

    public MainViewModel ViewModel { get; }

    public AppSettings Settings => _settings;

    /// <summary>Se dispara cuando ni el shell ni el icono pudieron mostrar la notificación.</summary>
    public event EventHandler<NotificationRequest>? NotificationFailed;

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;

        _monitor.ReadingChanged += OnReadingChanged;

        _powerWatcher = new PowerModeWatcher();
        _powerWatcher.Resumed += OnResumed;
        _powerWatcher.Suspending += OnSuspending;

        EnsureAutoStart();
        ViewModel.SetHistory(GetHistoryEntries());

        _monitor.Start();
        _ = RefreshHealthAsync();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Normalized();
        _settingsStore.Save(_settings);

        _alerts.Configure(ToThresholds(_settings));
        _monitor.PollInterval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);
        _autoStart.SetEnabled(_settings.StartWithWindows);

        ViewModel.ApplySettings(_settings);
    }

    public async Task RefreshAsync()
    {
        _monitor.RequestRefresh();
        await RefreshHealthAsync().ConfigureAwait(false);
    }

    public IReadOnlyList<HistoryRow> GetHistoryRows()
    {
        // El usuario abrió el historial: volcar lo pendiente para no mostrar una lista incompleta.
        _history.Flush();
        return GetHistoryEntries().Select(HistoryRow.FromEntry).ToList();
    }

    public void Shutdown()
    {
        _history.Flush();
        Dispose();
    }

    private IReadOnlyList<HistoryEntry> GetHistoryEntries() => _historyStore.LoadRecent(HistoryPageSize);

    private void OnReadingChanged(object? sender, BatteryReading reading)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            HandleReading(reading);
        }
        else
        {
            dispatcher.BeginInvoke(() => HandleReading(reading));
        }
    }

    private void HandleReading(BatteryReading reading)
    {
        if (_disposed)
        {
            return;
        }

        ViewModel.UpdateReading(reading);

        var alerts = _alerts.Evaluate(reading);
        var recorded = _history.Add(reading, _alerts.CurrentZone);
        _history.FlushIfDue();

        if (recorded)
        {
            ViewModel.SetHistory(GetHistoryEntries());
        }

        foreach (var alert in alerts)
        {
            ShowInAppBanner(alert.Message);
            RaiseNotification(alert);
        }
    }

    private void RaiseNotification(BatteryAlert alert)
    {
        if (!_settings.NotificationsEnabled)
        {
            return;
        }

        var request = new NotificationRequest(alert.Title, alert.Message, NotificationSeverity.Warning);
        if (!_notifier.Notify(request))
        {
            NotificationFailed?.Invoke(this, request);
        }
    }

    private void ShowInAppBanner(string message)
    {
        ViewModel.ShowBanner(message);
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    private async Task RefreshHealthAsync()
    {
        var health = await _healthProvider.ReadAsync().ConfigureAwait(false);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => ViewModel.UpdateHealth(health));
    }

    private void EnsureAutoStart()
    {
        if (_autoStart.IsEnabled != _settings.StartWithWindows)
        {
            _autoStart.SetEnabled(_settings.StartWithWindows);
        }
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        // Tras reanudar, la primera lectura puede ser obsoleta: se fuerza una nueva.
        _monitor.RequestRefresh();
        _history.FlushIfDue();
    }

    private void OnSuspending(object? sender, EventArgs e) => _history.Flush();

    private static AlertThresholds ToThresholds(AppSettings settings) => new(
        settings.UpperThreshold,
        settings.LowerThreshold,
        settings.UpperAlertEnabled,
        settings.LowerAlertEnabled);

    private static string BuildAutoStartCommand()
    {
        var executable = Environment.ProcessPath
                         ?? Path.Combine(AppContext.BaseDirectory, "VictusChargeControl.exe");
        return $"\"{executable}\" --tray";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _bannerTimer.Stop();
        _monitor.ReadingChanged -= OnReadingChanged;
        _monitor.Dispose();
        _powerWatcher?.Dispose();
        _powerWatcher = null;
        _history.Flush();
    }
}
