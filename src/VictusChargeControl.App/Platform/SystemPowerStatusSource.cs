using System.Runtime.InteropServices;
using VictusChargeControl.Core.Model;
using VictusChargeControl.Core.Monitoring;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Respaldo y fuente de AC: <c>GetSystemPowerStatus</c>. Barato, sin dependencias
/// y disponible incluso si la proyección WinRT falla.
/// </summary>
public sealed class SystemPowerStatusSource : IBatterySource
{
    private const byte NoSystemBattery = 128;
    private const byte UnknownFlag = 255;
    private const byte UnknownPercent = 255;

    public string Name => "GetSystemPowerStatus";

    public bool IsAvailable { get; private set; } = true;

    public Task<BatteryReading?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<BatteryReading?>(Read());

    public BatteryReading? Read()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                IsAvailable = false;
                return null;
            }

            IsAvailable = true;

            var noBattery = status.BatteryFlag == UnknownFlag || (status.BatteryFlag & NoSystemBattery) != 0;
            var percent = status.BatteryLifePercent == UnknownPercent ? (int?)null : status.BatteryLifePercent;
            var ac = status.ACLineStatus switch
            {
                0 => (bool?)false,
                1 => true,
                _ => null
            };

            var state = noBattery
                ? BatteryChargeState.NotPresent
                : ac switch
                {
                    true => (status.BatteryFlag & 8) != 0 ? BatteryChargeState.Charging : BatteryChargeState.AcConnectedIdle,
                    false => BatteryChargeState.Discharging,
                    null => BatteryChargeState.Unknown
                };

            return new BatteryReading(DateTimeOffset.Now)
            {
                Percent = percent,
                State = state,
                AcConnected = ac,
                Source = BatterySource.SystemPowerStatus
            };
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            IsAvailable = false;
            return null;
        }
    }

    public bool? ReadAcLineStatus()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                return null;
            }

            return status.ACLineStatus switch
            {
                0 => false,
                1 => true,
                _ => null
            };
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
