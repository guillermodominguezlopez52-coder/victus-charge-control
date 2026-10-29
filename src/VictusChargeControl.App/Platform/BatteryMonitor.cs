using VictusChargeControl.Core.Model;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Orquesta las fuentes: WinRT como principal, <c>GetSystemPowerStatus</c> para AC y
/// respaldo, WMI solo si falta la potencia. Una única lectura en vuelo a la vez.
/// </summary>
public sealed class BatteryMonitor : IDisposable
{
    private readonly WinRtBatterySource _winRt = new();
    private readonly SystemPowerStatusSource _system = new();
    private readonly WmiBatteryDetailSource _wmi = new();
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private readonly object _timerLock = new();

    private Windows.Devices.Power.Battery? _aggregate;
    private Timer? _timer;
    private TimeSpan _interval = TimeSpan.FromSeconds(30);
    private bool _started;
    private bool _disposed;

    public event EventHandler<BatteryReading>? ReadingChanged;

    public BatteryReading? Latest { get; private set; }

    public TimeSpan PollInterval
    {
        get => _interval;
        set
        {
            var clamped = value < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : value;
            if (clamped == _interval)
            {
                return;
            }

            _interval = clamped;
            RestartTimer();
        }
    }

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;

        try
        {
            _aggregate = Windows.Devices.Power.Battery.AggregateBattery;
            _aggregate.ReportUpdated += OnReportUpdated;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TypeLoadException)
        {
            _aggregate = null;
        }

        RestartTimer();
        QueueRefresh();
    }

    public void RequestRefresh() => QueueRefresh();

    public async Task RefreshAsync()
    {
        if (_disposed || !await _refreshing.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var reading = await ComposeAsync().ConfigureAwait(false);
            if (reading is null)
            {
                return;
            }

            Latest = reading;
            ReadingChanged?.Invoke(this, reading);
        }
        finally
        {
            _refreshing.Release();
        }
    }

    private async Task<BatteryReading?> ComposeAsync()
    {
        var winRt = await _winRt.ReadAsync().ConfigureAwait(false);
        var system = _system.Read();

        if (winRt is null)
        {
            return system;
        }

        var ac = system?.AcConnected ?? _system.ReadAcLineStatus();
        var percent = winRt.Percent ?? system?.Percent;
        var power = winRt.PowerWatts;

        if (power is null)
        {
            power = await _wmi.ReadPowerWattsAsync().ConfigureAwait(false);
        }

        return winRt with
        {
            Percent = percent,
            State = ResolveState(winRt.State, ac),
            AcConnected = ac,
            PowerWatts = power,
            Timestamp = DateTimeOffset.Now
        };
    }

    private static BatteryChargeState ResolveState(BatteryChargeState state, bool? ac) => state switch
    {
        BatteryChargeState.NotPresent => BatteryChargeState.NotPresent,
        BatteryChargeState.Charging when ac == false => BatteryChargeState.Discharging,
        BatteryChargeState.Discharging when ac == true => BatteryChargeState.AcConnectedIdle,
        BatteryChargeState.Unknown when ac == true => BatteryChargeState.AcConnectedIdle,
        BatteryChargeState.Unknown when ac == false => BatteryChargeState.Discharging,
        _ => state
    };

    private void OnReportUpdated(Windows.Devices.Power.Battery sender, object args) => QueueRefresh();

    private void QueueRefresh() => _ = RefreshAsync();

    private void RestartTimer()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = _started
                ? new Timer(_ => QueueRefresh(), null, _interval, _interval)
                : null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_aggregate is not null)
        {
            try
            {
                _aggregate.ReportUpdated -= OnReportUpdated;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // La sesión WinRT ya se cerró.
            }
        }

        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        _refreshing.Dispose();
    }
}
