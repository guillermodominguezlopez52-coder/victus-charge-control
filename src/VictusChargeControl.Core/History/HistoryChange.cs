namespace VictusChargeControl.Core.History;

/// <summary>Motivo por el que se registró una entrada. Puede haber varios a la vez.</summary>
[Flags]
public enum HistoryChange
{
    None = 0,
    FirstSample = 1,
    StateChange = 2,
    AcChange = 4,
    ZoneChange = 8,
    PercentChange = 16
}
