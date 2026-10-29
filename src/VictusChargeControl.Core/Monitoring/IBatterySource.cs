using VictusChargeControl.Core.Model;

namespace VictusChargeControl.Core.Monitoring;

/// <summary>Fuente de lecturas de batería. Puede no estar disponible en un equipo dado.</summary>
public interface IBatterySource
{
    string Name { get; }

    bool IsAvailable { get; }

    Task<BatteryReading?> ReadAsync(CancellationToken cancellationToken = default);
}
