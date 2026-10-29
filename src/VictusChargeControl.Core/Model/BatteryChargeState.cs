namespace VictusChargeControl.Core.Model;

/// <summary>
/// Estado normalizado de la batería, independiente de la API de origen.
/// </summary>
public enum BatteryChargeState
{
    /// <summary>La fuente no pudo determinar el estado.</summary>
    Unknown = 0,

    /// <summary>No hay batería instalada o no es detectable.</summary>
    NotPresent = 1,

    /// <summary>Conectada a corriente y cargando.</summary>
    Charging = 2,

    /// <summary>Alimentándose de la batería.</summary>
    Discharging = 3,

    /// <summary>Conectada a corriente pero el firmware no está cargando.</summary>
    AcConnectedIdle = 4
}
