using VictusChargeControl.Core.Model;

namespace VictusChargeControl.Core.History;

/// <summary>
/// Decide qué lecturas merecen una entrada y agrupa las escrituras para no golpear
/// el disco en cada consulta.
/// </summary>
public sealed class HistoryRecorder
{
    private const int PercentJumpThreshold = 5;

    private readonly HistoryStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _minFlushInterval;
    private readonly int _maxPending;
    private readonly List<HistoryEntry> _pending = new();

    private BatteryReading? _lastRecorded;
    private ChargeZone _lastZone = ChargeZone.Unknown;
    private DateTimeOffset _lastFlush;

    public HistoryRecorder(
        HistoryStore store,
        Func<DateTimeOffset> clock,
        TimeSpan? minFlushInterval = null,
        int maxPending = 10)
    {
        _store = store;
        _clock = clock;
        _minFlushInterval = minFlushInterval ?? TimeSpan.FromSeconds(60);
        _maxPending = Math.Max(1, maxPending);
        _lastFlush = clock();
    }

    public int PendingCount => _pending.Count;

    /// <summary>Registra la lectura si aporta información nueva. Devuelve si se registró.</summary>
    public bool Add(BatteryReading reading, ChargeZone zone)
    {
        var changes = DetermineChanges(reading, zone);
        if (changes == HistoryChange.None)
        {
            return false;
        }

        _pending.Add(new HistoryEntry(
            reading.Timestamp,
            reading.Percent,
            reading.AcConnected,
            reading.State,
            zone,
            changes));

        _lastRecorded = reading;
        _lastZone = zone;

        FlushIfDue();
        return true;
    }

    public void FlushIfDue()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var now = _clock();
        if (_pending.Count >= _maxPending || now - _lastFlush >= _minFlushInterval)
        {
            Flush();
        }
    }

    /// <summary>Escribe lo pendiente. Se llama al cerrar la aplicación.</summary>
    public void Flush()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = _pending.ToArray();
        try
        {
            _store.Append(batch);
            _pending.Clear();
            _lastFlush = _clock();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El historial es secundario: si el disco falla, la app sigue funcionando.
        }
    }

    private HistoryChange DetermineChanges(BatteryReading reading, ChargeZone zone)
    {
        if (_lastRecorded is null)
        {
            return HistoryChange.FirstSample;
        }

        var changes = HistoryChange.None;

        if (_lastRecorded.State != reading.State)
        {
            changes |= HistoryChange.StateChange;
        }

        if (_lastRecorded.AcConnected != reading.AcConnected)
        {
            changes |= HistoryChange.AcChange;
        }

        if (_lastZone != zone)
        {
            changes |= HistoryChange.ZoneChange;
        }

        if (_lastRecorded.Percent is int previous &&
            reading.Percent is int current &&
            Math.Abs(current - previous) >= PercentJumpThreshold)
        {
            changes |= HistoryChange.PercentChange;
        }

        return changes;
    }
}
