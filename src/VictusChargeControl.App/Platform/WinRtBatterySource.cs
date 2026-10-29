using System.Runtime.InteropServices;
using VictusChargeControl.Core.Model;
using VictusChargeControl.Core.Monitoring;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Fuente principal: WinRT <c>Windows.Devices.Power.Battery</c>. Entrega porcentaje,
/// estado y potencia; no entrega AC (se completa con <see cref="SystemPowerStatusSource"/>).
/// </summary>
public sealed class WinRtBatterySource : IBatterySource
{
    public string Name => "Windows.Devices.Power";

    public bool IsAvailable { get; private set; } = true;

    public Task<BatteryReading?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var report = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
            var state = MapState(report.Status);

            int? percent = null;
            if (report.RemainingCapacityInMilliwattHours is int remaining &&
                report.FullChargeCapacityInMilliwattHours is int full &&
                full > 0)
            {
                percent = (int)Math.Round(remaining * 100.0 / full, MidpointRounding.AwayFromZero);
                percent = Math.Clamp(percent.Value, 0, 100);
            }

            double? watts = report.ChargeRateInMilliwatts is int rate ? rate / 1000.0 : null;

            IsAvailable = true;
            return Task.FromResult<BatteryReading?>(new BatteryReading(DateTimeOffset.Now)
            {
                Percent = percent,
                State = state,
                PowerWatts = watts,
                Source = BatterySource.WinRt
            });
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TypeLoadException)
        {
            IsAvailable = false;
            return Task.FromResult<BatteryReading?>(null);
        }
    }

    private static BatteryChargeState MapState(Windows.System.Power.BatteryStatus status) => status switch
    {
        Windows.System.Power.BatteryStatus.Charging => BatteryChargeState.Charging,
        Windows.System.Power.BatteryStatus.Discharging => BatteryChargeState.Discharging,
        Windows.System.Power.BatteryStatus.Idle => BatteryChargeState.AcConnectedIdle,
        Windows.System.Power.BatteryStatus.NotPresent => BatteryChargeState.NotPresent,
        _ => BatteryChargeState.Unknown
    };
}
