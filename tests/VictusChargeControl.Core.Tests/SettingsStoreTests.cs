using System.Text.Json;
using VictusChargeControl.Core.Settings;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vcc-settings-" + Guid.NewGuid().ToString("N"));
    private string Path_ => Path.Combine(_dir, "settings.json");

    public SettingsStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Load_SinArchivo_DevuelveValoresPorDefecto()
    {
        var store = new SettingsStore(Path_);

        var settings = store.Load();

        Assert.Equal(80, settings.UpperThreshold);
        Assert.Equal(40, settings.LowerThreshold);
    }

    [Fact]
    public void SaveYLuegoLoad_ConservaLosValores()
    {
        var store = new SettingsStore(Path_);
        var original = new AppSettings
        {
            UpperThreshold = 75,
            LowerThreshold = 45,
            UpperAlertEnabled = false,
            StartWithWindows = true,
            PollIntervalSeconds = 60
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(75, loaded.UpperThreshold);
        Assert.Equal(45, loaded.LowerThreshold);
        Assert.False(loaded.UpperAlertEnabled);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(60, loaded.PollIntervalSeconds);
    }

    [Fact]
    public void Save_NormalizaAntesDeEscribir()
    {
        var store = new SettingsStore(Path_);

        store.Save(new AppSettings { UpperThreshold = 99, LowerThreshold = 98 });

        var json = File.ReadAllText(Path_);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(100, document.RootElement.GetProperty("upperThreshold").GetInt32());
        var loaded = store.Load();
        Assert.Equal(100, loaded.UpperThreshold);
        Assert.Equal(95, loaded.LowerThreshold);
    }

    [Fact]
    public void Save_NoDejaArchivosTemporales()
    {
        var store = new SettingsStore(Path_);

        store.Save(new AppSettings());

        Assert.True(File.Exists(Path_));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void Load_ConJsonCorrupto_DevuelveValoresPorDefecto()
    {
        File.WriteAllText(Path_, "{ esto no es json valido ");
        var store = new SettingsStore(Path_);

        var settings = store.Load();

        Assert.Equal(80, settings.UpperThreshold);
        Assert.Equal(40, settings.LowerThreshold);
    }

    [Fact]
    public void Load_CreaElDirectorioPadreAlGuardar()
    {
        var nested = Path.Combine(_dir, "sub", "carpeta", "settings.json");
        var store = new SettingsStore(nested);

        store.Save(new AppSettings { UpperThreshold = 85 });

        Assert.True(File.Exists(nested));
    }
}
