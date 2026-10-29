using VictusChargeControl.Core.History;
using VictusChargeControl.Core.Model;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class HistoryRecorderTests : IDisposable
{
    private sealed class FakeClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 29, 8, 0, 0, TimeSpan.FromHours(-6));
        public void Advance(TimeSpan span) => Now += span;
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vcc-history-" + Guid.NewGuid().ToString("N"));
    private readonly FakeClock _clock = new();
    private readonly HistoryStore _store;
    private readonly HistoryRecorder _recorder;

    public HistoryRecorderTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new HistoryStore(Path.Combine(_dir, "history.jsonl"));
        _recorder = new HistoryRecorder(_store, () => _clock.Now);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private BatteryReading Reading(
        int? percent,
        BatteryChargeState state = BatteryChargeState.Discharging,
        bool? ac = false) =>
        new(_clock.Now) { Percent = percent, State = state, AcConnected = ac };

    [Fact]
    public void RegistraLaPrimeraMuestra()
    {
        Assert.True(_recorder.Add(Reading(81), ChargeZone.Within));
        Assert.Equal(1, _recorder.PendingCount);
    }

    [Fact]
    public void NoRegistraMuestrasSinCambiosRelevantes()
    {
        _recorder.Add(Reading(81), ChargeZone.Within);

        Assert.False(_recorder.Add(Reading(81), ChargeZone.Within));
        Assert.False(_recorder.Add(Reading(83), ChargeZone.Within));
        Assert.Equal(1, _recorder.PendingCount);
    }

    [Fact]
    public void RegistraCambioDeEstado()
    {
        _recorder.Add(Reading(81, BatteryChargeState.Discharging, ac: false), ChargeZone.Within);

        Assert.True(_recorder.Add(Reading(81, BatteryChargeState.Charging, ac: true), ChargeZone.Within));
    }

    [Fact]
    public void RegistraSaltoDeCincoPuntos()
    {
        _recorder.Add(Reading(81), ChargeZone.Within);

        Assert.True(_recorder.Add(Reading(86), ChargeZone.Within));
    }

    [Fact]
    public void RegistraEntradaEnZona()
    {
        _recorder.Add(Reading(78), ChargeZone.Within);

        Assert.True(_recorder.Add(Reading(80), ChargeZone.AboveUpper));
    }

    [Fact]
    public void NoEscribeAntesDelIntervaloMinimo()
    {
        _recorder.Add(Reading(81), ChargeZone.Within);

        _clock.Advance(TimeSpan.FromSeconds(30));
        _recorder.FlushIfDue();

        Assert.Empty(_store.LoadRecent(10));
        Assert.Equal(1, _recorder.PendingCount);
    }

    [Fact]
    public void EscribePasadoElIntervaloMinimo()
    {
        _recorder.Add(Reading(81), ChargeZone.Within);

        _clock.Advance(TimeSpan.FromSeconds(61));
        _recorder.FlushIfDue();

        var entries = _store.LoadRecent(10);
        Assert.Single(entries);
        Assert.Equal(81, entries[0].Percent);
        Assert.Equal(0, _recorder.PendingCount);
    }

    [Fact]
    public void EscribeAlAcumularDiezEventos()
    {
        for (var i = 0; i < 10; i++)
        {
            _recorder.Add(Reading(50 + i, BatteryChargeState.Discharging, ac: i % 2 == 0), ChargeZone.Within);
        }

        _recorder.FlushIfDue();

        Assert.Equal(10, _store.LoadRecent(20).Count);
    }

    [Fact]
    public void FlushForzadoEscribeAunqueNoHayaPasadoElIntervalo()
    {
        _recorder.Add(Reading(81), ChargeZone.Within);

        _recorder.Flush();

        Assert.Single(_store.LoadRecent(10));
    }

    [Fact]
    public void FlushSinPendientes_NoHaceNada()
    {
        _recorder.Flush();

        Assert.Empty(_store.LoadRecent(10));
    }

    [Fact]
    public void LaEntradaGuardaFechaPorcentajeCorrienteYEstado()
    {
        _recorder.Add(Reading(77, BatteryChargeState.Discharging, ac: false), ChargeZone.Within);
        _recorder.Flush();

        var entry = Assert.Single(_store.LoadRecent(10));
        Assert.Equal(_clock.Now, entry.Timestamp);
        Assert.Equal(77, entry.Percent);
        Assert.False(entry.AcConnected);
        Assert.Equal(BatteryChargeState.Discharging, entry.State);
        Assert.Equal(ChargeZone.Within, entry.Zone);
    }
}
