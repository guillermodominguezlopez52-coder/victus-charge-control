namespace VictusChargeControl.Core.Alerts;

/// <summary>Umbrales efectivos de alerta.</summary>
public sealed record AlertThresholds(
    int Upper = 80,
    int Lower = 40,
    bool UpperEnabled = true,
    bool LowerEnabled = true);
