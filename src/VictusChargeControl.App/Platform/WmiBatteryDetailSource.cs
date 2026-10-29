using System.Management;
using System.Runtime.InteropServices;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Detalle eléctrico opcional desde <c>root\wmi:BatteryStatus</c>. Solo se consulta
/// cuando WinRT no entrega potencia, para no duplicar trabajo.
/// </summary>
public sealed class WmiBatteryDetailSource
{
    public async Task<double?> ReadPowerWattsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(Read, cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or ManagementException or COMException
                                       or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    private static double? Read()
    {
        using var searcher = new ManagementObjectSearcher(
            "root\\wmi",
            "SELECT ChargeRate, DischargeRate, PowerOnline FROM BatteryStatus");

        using var results = searcher.Get();
        foreach (ManagementObject item in results)
        {
            using (item)
            {
                var onAc = item["PowerOnline"] is bool online && online;

                if (onAc)
                {
                    var charge = ToDouble(item["ChargeRate"]);
                    if (charge is > 0)
                    {
                        return charge.Value / 1000.0;
                    }
                }
                else
                {
                    var discharge = ToDouble(item["DischargeRate"]);
                    if (discharge is > 0)
                    {
                        return -discharge.Value / 1000.0;
                    }
                }
            }
        }

        return null;
    }

    private static double? ToDouble(object? value) => value switch
    {
        null => null,
        uint number => number,
        int number => number,
        ushort number => number,
        long number => number,
        _ => double.TryParse(value.ToString(), out var parsed) ? parsed : null
    };
}
