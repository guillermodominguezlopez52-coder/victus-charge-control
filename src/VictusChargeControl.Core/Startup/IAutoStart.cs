namespace VictusChargeControl.Core.Startup;

/// <summary>Inicio automático con Windows. Implementado con la clave HKCU\...\Run.</summary>
public interface IAutoStart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
