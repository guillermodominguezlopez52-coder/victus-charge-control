using System.Diagnostics;
using VictusChargeControl.Core.BatteryReport;
using VictusChargeControl.Core.Model;

namespace VictusChargeControl.App.Platform;

/// <summary>
/// Obtiene capacidad de diseño, capacidad completa y ciclos ejecutando
/// <c>powercfg /batteryreport /xml</c>. Es una operación lenta: se hace al iniciar,
/// al refrescar manualmente y nunca en el bucle de polling.
/// </summary>
public sealed class PowerCfgReportProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<BatteryHealthInfo?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var output = Path.Combine(Path.GetTempPath(), $"vcc-battery-{Guid.NewGuid():N}.xml");

        try
        {
            var startInfo = new ProcessStartInfo("powercfg", $"/batteryreport /xml /output \"{output}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return null;
            }

            if (!File.Exists(output))
            {
                return null;
            }

            var xml = await File.ReadAllTextAsync(output, cancellationToken).ConfigureAwait(false);
            return BatteryReportParser.Parse(xml);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(output))
                {
                    File.Delete(output);
                }
            }
            catch (IOException)
            {
                // Archivo temporal: si no se puede borrar ahora, Windows lo limpiará.
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // El proceso ya terminó.
        }
    }
}
