namespace VictusChargeControl.Core.Alerts;

/// <summary>
/// Textos de alerta. Son deliberadamente explícitos: la aplicación mide y avisa,
/// no interrumpe la carga.
/// </summary>
public static class AlertText
{
    public static (string Title, string Message) Build(AlertKind kind, int percent, AlertThresholds thresholds) =>
        kind switch
        {
            AlertKind.UpperThreshold => (
                "Batería por encima del objetivo",
                $"La batería está al {percent} % y el cargador sigue conectado (límite configurado: {thresholds.Upper} %). " +
                "Desconectá el cargador si querés mantenerla dentro del intervalo. Esta alerta no detiene la carga."),
            AlertKind.LowerThreshold => (
                "Batería por debajo del objetivo",
                $"La batería está al {percent} % y bajó del {thresholds.Lower} %. " +
                $"Conectá el cargador para volver al intervalo de {thresholds.Lower}–{thresholds.Upper} %."),
            _ => ("Estado de batería", $"La batería está al {percent} %.")
        };
}
