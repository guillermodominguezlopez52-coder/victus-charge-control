using Microsoft.Win32;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Reanudación tras suspensión/hibernación. Al volver, la primera lectura puede ser
/// obsoleta: la app fuerza una relectura y reinicia su temporizador.
/// </summary>
public sealed class PowerModeWatcher : IDisposable
{
    private bool _disposed;

    public PowerModeWatcher()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? Resumed;

    public event EventHandler? Suspending;

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Resume:
                Resumed?.Invoke(this, EventArgs.Empty);
                break;
            case PowerModes.Suspend:
                Suspending?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
