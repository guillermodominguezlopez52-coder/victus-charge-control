using VictusChargeControl.Core.History;
using VictusChargeControl.Core.Model;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vcc-store-" + Guid.NewGuid().ToString("N"));
    private string Path_ => Path.Combine(_dir, "history.jsonl");

    public HistoryStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static HistoryEntry Entry(int percent, int minute) =>
        new(new DateTimeOffset(2026, 10, 29, 10, minute, 0, TimeSpan.Zero),
            percent,
            true,
            BatteryChargeState.Charging,
            ChargeZone.Within,
            HistoryChange.FirstSample);

    [Fact]
    public void LoadRecent_SinArchivo_DevuelveListaVacia()
    {
        var store = new HistoryStore(Path_);

        Assert.Empty(store.LoadRecent(50));
    }

    [Fact]
    public void Append_Y_LoadRecent_ConservaElOrdenCronologico()
    {
        var store = new HistoryStore(Path_);

        store.Append(new[] { Entry(70, 1), Entry(75, 2), Entry(80, 3) });
        var entries = store.LoadRecent(10);

        Assert.Equal(new[] { 70, 75, 80 }, entries.Select(e => e.Percent!.Value).ToArray());
    }

    [Fact]
    public void LoadRecent_DevuelveSoloLasUltimasEntradas()
    {
        var store = new HistoryStore(Path_);
        store.Append(Enumerable.Range(1, 10).Select(i => Entry(50 + i, i)));

        var entries = store.LoadRecent(3);

        Assert.Equal(new[] { 58, 59, 60 }, entries.Select(e => e.Percent!.Value).ToArray());
    }

    [Fact]
    public void LoadRecent_IgnoraLineasCorruptas()
    {
        var store = new HistoryStore(Path_);
        store.Append(new[] { Entry(70, 1) });
        File.AppendAllText(Path_, "{ linea rota" + Environment.NewLine);
        store.Append(new[] { Entry(80, 2) });

        var entries = store.LoadRecent(10);

        Assert.Equal(2, entries.Count);
        Assert.Equal(80, entries[^1].Percent);
    }

    [Fact]
    public void Append_RespetaElTopeDeEntradas()
    {
        var store = new HistoryStore(Path_, maxEntries: 5);

        store.Append(Enumerable.Range(1, 12).Select(i => Entry(50 + i, i)));

        var lines = File.ReadAllLines(Path_).Where(l => !string.IsNullOrWhiteSpace(l)).Count();
        Assert.InRange(lines, 1, 5);
        Assert.Equal(62, store.LoadRecent(5)[^1].Percent);
    }

    [Fact]
    public void Append_NoFallaSiElDirectorioNoExiste()
    {
        var nested = Path.Combine(_dir, "sub", "history.jsonl");
        var store = new HistoryStore(nested);

        store.Append(new[] { Entry(70, 1) });

        Assert.Single(store.LoadRecent(5));
    }
}
