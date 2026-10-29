namespace VictusChargeControl.Core.Model;

/// <summary>Origen del dato leído. Útil para diagnóstico y para la UI.</summary>
public enum BatterySource
{
    Unknown = 0,
    WinRt = 1,
    SystemPowerStatus = 2,
    Wmi = 3
}
