namespace VictusChargeControl.Core.Model;

/// <summary>
/// Datos de salud del pack. Cualquier campo puede ser <c>null</c> si la fuente no lo
/// proporciona (por ejemplo, un equipo sin batería).
/// </summary>
public sealed record BatteryHealthInfo(
    int? DesignCapacityMWh,
    int? FullChargeCapacityMWh,
    int? CycleCount)
{
    /// <summary>Salud relativa 0-100, o <c>null</c> si faltan datos.</summary>
    public double? HealthPercent =>
        DesignCapacityMWh is int design && design > 0 && FullChargeCapacityMWh is int full && full > 0
            ? Math.Round(full * 100.0 / design, 1)
            : null;
}
