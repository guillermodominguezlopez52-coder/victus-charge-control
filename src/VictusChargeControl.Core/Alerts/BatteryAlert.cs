namespace VictusChargeControl.Core.Alerts;

public sealed record BatteryAlert(
    AlertKind Kind,
    int Percent,
    string Title,
    string Message);
