using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using VictusChargeControl.App.Infrastructure;
using VictusChargeControl.Core.History;
using VictusChargeControl.Core.Model;
using VictusChargeControl.Core.Settings;

namespace VictusChargeControl.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly Brush Green = Freeze(0x22, 0xC5, 0x5E);
    private static readonly Brush Blue = Freeze(0x3B, 0x82, 0xF6);
    private static readonly Brush Amber = Freeze(0xF5, 0x9E, 0x0B);
    private static readonly Brush Muted = Freeze(0x8B, 0x98, 0xA9);

    private AppSettings _settings;
    private BatteryReading? _lastReading;

    private string _percentText = DisplayText.Missing;
    private string _stateText = "Esperando datos";
    private string _acText = DisplayText.Missing;
    private string _powerText = DisplayText.Missing;
    private string _lastUpdateText = "Sin lecturas todavía";
    private string _sourceText = string.Empty;
    private string _zoneText = "Sin datos";
    private string _warningText = string.Empty;
    private bool _warningVisible;
    private string _bannerText = string.Empty;
    private bool _bannerVisible;
    private bool _infoVisible = true;
    private Brush _stateBrush = Muted;

    private string _designText = DisplayText.Missing;
    private string _fullText = DisplayText.Missing;
    private string _cyclesText = DisplayText.Missing;
    private string _healthText = DisplayText.Missing;

    private GridLength _lowerStar;
    private GridLength _bandStar;
    private GridLength _upperStar;
    private GridLength _fillStar;
    private GridLength _restStar;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings.Normalized();
        RecalculateBand();
    }

    public ObservableCollection<HistoryRow> History { get; } = new();

    public AppSettings Settings => _settings;

    public int UpperThreshold => _settings.UpperThreshold;

    public int LowerThreshold => _settings.LowerThreshold;

    public string PercentText
    {
        get => _percentText;
        private set => Set(ref _percentText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value);
    }

    public Brush StateBrush
    {
        get => _stateBrush;
        private set => Set(ref _stateBrush, value);
    }

    public string AcText
    {
        get => _acText;
        private set => Set(ref _acText, value);
    }

    public string PowerText
    {
        get => _powerText;
        private set => Set(ref _powerText, value);
    }

    public string LastUpdateText
    {
        get => _lastUpdateText;
        private set => Set(ref _lastUpdateText, value);
    }

    public string SourceText
    {
        get => _sourceText;
        private set => Set(ref _sourceText, value);
    }

    public string ZoneText
    {
        get => _zoneText;
        private set => Set(ref _zoneText, value);
    }

    public string DesignText
    {
        get => _designText;
        private set => Set(ref _designText, value);
    }

    public string FullText
    {
        get => _fullText;
        private set => Set(ref _fullText, value);
    }

    public string CyclesText
    {
        get => _cyclesText;
        private set => Set(ref _cyclesText, value);
    }

    public string HealthText
    {
        get => _healthText;
        private set => Set(ref _healthText, value);
    }

    public bool WarningVisible
    {
        get => _warningVisible;
        private set => Set(ref _warningVisible, value);
    }

    public string WarningText
    {
        get => _warningText;
        private set => Set(ref _warningText, value);
    }

    public bool BannerVisible
    {
        get => _bannerVisible;
        private set => Set(ref _bannerVisible, value);
    }

    public bool InfoVisible
    {
        get => _infoVisible;
        private set => Set(ref _infoVisible, value);
    }

    public string BannerText
    {
        get => _bannerText;
        private set => Set(ref _bannerText, value);
    }

    public GridLength LowerStar
    {
        get => _lowerStar;
        private set => Set(ref _lowerStar, value);
    }

    public GridLength BandStar
    {
        get => _bandStar;
        private set => Set(ref _bandStar, value);
    }

    public GridLength UpperStar
    {
        get => _upperStar;
        private set => Set(ref _upperStar, value);
    }

    public GridLength FillStar
    {
        get => _fillStar;
        private set => Set(ref _fillStar, value);
    }

    public GridLength RestStar
    {
        get => _restStar;
        private set => Set(ref _restStar, value);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Normalized();
        RecalculateBand();
        RecalculateFill();
        UpdateWarning();

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(UpperThreshold));
        OnPropertyChanged(nameof(LowerThreshold));
    }

    public void UpdateReading(BatteryReading reading)
    {
        _lastReading = reading;

        PercentText = DisplayText.Percent(reading.Percent);
        StateText = DisplayText.State(reading.State);
        AcText = reading.State == BatteryChargeState.NotPresent
            ? DisplayText.Missing
            : DisplayText.Ac(reading.AcConnected);
        PowerText = DisplayText.Watts(reading.PowerWatts);
        LastUpdateText = $"Última lectura: {reading.Timestamp:HH:mm:ss}";
        SourceText = reading.Source switch
        {
            BatterySource.WinRt => "Windows.Devices.Power",
            BatterySource.SystemPowerStatus => "GetSystemPowerStatus",
            BatterySource.Wmi => "WMI",
            _ => DisplayText.Missing
        };

        StateBrush = reading.State switch
        {
            BatteryChargeState.Charging => Green,
            BatteryChargeState.Discharging => Blue,
            BatteryChargeState.AcConnectedIdle => Amber,
            _ => Muted
        };

        ZoneText = Core.Model.ChargeZoneCalculator.GetZone(
            reading.Percent, _settings.LowerThreshold, _settings.UpperThreshold) switch
        {
            ChargeZone.AboveUpper => "Por encima del objetivo",
            ChargeZone.BelowLower => "Por debajo del objetivo",
            ChargeZone.Within => "Dentro del intervalo",
            _ => "Sin datos"
        };

        RecalculateFill();
        UpdateWarning();
    }

    public void UpdateHealth(BatteryHealthInfo? health)
    {
        DesignText = DisplayText.Capacity(health?.DesignCapacityMWh);
        FullText = DisplayText.Capacity(health?.FullChargeCapacityMWh);
        CyclesText = DisplayText.Cycles(health?.CycleCount);
        HealthText = DisplayText.Health(health?.HealthPercent);
    }

    public void SetHistory(IEnumerable<HistoryEntry> entries)
    {
        History.Clear();
        foreach (var entry in entries)
        {
            History.Add(HistoryRow.FromEntry(entry));
        }
    }

    public void ShowBanner(string text)
    {
        BannerText = text;
        BannerVisible = true;
        InfoVisible = false;
    }

    public void HideBanner()
    {
        BannerVisible = false;
        InfoVisible = true;
    }

    private void UpdateWarning()
    {
        if (_lastReading is { HasValidPercent: true } reading &&
            reading.IsOnAc &&
            reading.Percent > _settings.UpperThreshold)
        {
            WarningText =
                $"El cargador sigue conectado y la batería está al {reading.Percent} % " +
                $"(por encima de {_settings.UpperThreshold} %). Esta aplicación no detiene la carga.";
            WarningVisible = true;
        }
        else
        {
            WarningVisible = false;
        }

        RecalculateFill();
    }

    private void RecalculateBand()
    {
        var lower = _settings.LowerThreshold;
        var upper = _settings.UpperThreshold;

        LowerStar = new GridLength(lower, GridUnitType.Star);
        BandStar = new GridLength(Math.Max(1, upper - lower), GridUnitType.Star);
        UpperStar = new GridLength(Math.Max(1, 100 - upper), GridUnitType.Star);
    }

    private void RecalculateFill()
    {
        var percent = _lastReading is { HasValidPercent: true } reading ? reading.Percent!.Value : 0;
        percent = Math.Clamp(percent, 0, 100);

        FillStar = new GridLength(Math.Max(percent, 0.01), GridUnitType.Star);
        RestStar = new GridLength(Math.Max(0.01, 100 - percent), GridUnitType.Star);
    }

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
