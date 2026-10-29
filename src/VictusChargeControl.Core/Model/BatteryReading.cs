namespace VictusChargeControl.Core.Model;

/// <summary>
/// Instantánea normalizada de la batería. Todos los datos opcionales usan <c>null</c>
/// para "no disponible": nunca se confunde con cero.
/// </summary>
public sealed record BatteryReading(DateTimeOffset Timestamp)
{
    /// <summary>Porcentaje 0-100, o <c>null</c> si la fuente no lo entrega.</summary>
    public int? Percent { get; init; }

    public BatteryChargeState State { get; init; } = BatteryChargeState.Unknown;

    /// <summary>Corriente alterna conectada, o <c>null</c> si no se pudo determinar.</summary>
    public bool? AcConnected { get; init; }

    /// <summary>Potencia en vatios: positiva al cargar, negativa al descargar.</summary>
    public double? PowerWatts { get; init; }

    public BatterySource Source { get; init; } = BatterySource.Unknown;

    /// <summary>
    /// Verdadero si hay AC. Si la fuente no lo informa, se infiere del estado
    /// (cargando o conectado sin carga).
    /// </summary>
    public bool IsOnAc =>
        AcConnected ?? (State is BatteryChargeState.Charging or BatteryChargeState.AcConnectedIdle);

    public bool HasValidPercent => Percent is >= 0 and <= 100;
}
