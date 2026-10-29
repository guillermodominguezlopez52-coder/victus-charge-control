using VictusChargeControl.Core.Model;

namespace VictusChargeControl.Core.Alerts;

/// <summary>
/// Decide cuándo emitir una alerta. Reglas:
/// <list type="bullet">
/// <item>Una sola alerta por entrada a la zona.</item>
/// <item>Para rearmarse hay que salir de la zona con histéresis (1 punto por defecto).</item>
/// <item>El umbral superior exige AC; el inferior exige descarga.</item>
/// <item>Las lecturas sin porcentaje válido no alertan ni rearman.</item>
/// </list>
/// </summary>
public sealed class AlertEngine
{
    private readonly int _hysteresis;
    private AlertThresholds _thresholds;
    private bool _upperArmed = true;
    private bool _lowerArmed = true;

    public AlertEngine(AlertThresholds thresholds, int hysteresis = 1)
    {
        _thresholds = thresholds;
        _hysteresis = Math.Max(0, hysteresis);
    }

    public AlertThresholds Thresholds => _thresholds;

    public ChargeZone CurrentZone { get; private set; } = ChargeZone.Unknown;

    /// <summary>Cambia los umbrales y rearma ambas alertas.</summary>
    public void Configure(AlertThresholds thresholds)
    {
        _thresholds = thresholds;
        _upperArmed = true;
        _lowerArmed = true;
    }

    public void Reset()
    {
        _upperArmed = true;
        _lowerArmed = true;
        CurrentZone = ChargeZone.Unknown;
    }

    public IReadOnlyList<BatteryAlert> Evaluate(BatteryReading reading)
    {
        var zone = ChargeZoneCalculator.GetZone(reading.Percent, _thresholds.Lower, _thresholds.Upper);
        CurrentZone = zone;

        if (zone == ChargeZone.Unknown || reading.Percent is not int percent)
        {
            return Array.Empty<BatteryAlert>();
        }

        var alerts = new List<BatteryAlert>(1);

        if (percent >= _thresholds.Upper)
        {
            if (_upperArmed && _thresholds.UpperEnabled && reading.IsOnAc)
            {
                alerts.Add(Create(AlertKind.UpperThreshold, percent));
                _upperArmed = false;
            }
        }
        else if (percent < _thresholds.Upper - _hysteresis)
        {
            _upperArmed = true;
        }

        if (percent <= _thresholds.Lower)
        {
            if (_lowerArmed && _thresholds.LowerEnabled && reading.State == BatteryChargeState.Discharging)
            {
                alerts.Add(Create(AlertKind.LowerThreshold, percent));
                _lowerArmed = false;
            }
        }
        else if (percent > _thresholds.Lower + _hysteresis)
        {
            _lowerArmed = true;
        }

        return alerts;
    }

    private BatteryAlert Create(AlertKind kind, int percent)
    {
        var (title, message) = AlertText.Build(kind, percent, _thresholds);
        return new BatteryAlert(kind, percent, title, message);
    }
}
