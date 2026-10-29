using VictusChargeControl.Core.Model;

namespace VictusChargeControl.Core.History;

/// <summary>
/// Evento registrado en el historial. Es un registro de cambios, no un muestreo
/// continuo: no hay entradas mientras la aplicación estuvo cerrada o suspendida.
/// </summary>
public sealed record HistoryEntry(
    DateTimeOffset Timestamp,
    int? Percent,
    bool? AcConnected,
    BatteryChargeState State,
    ChargeZone Zone,
    HistoryChange Changes);
