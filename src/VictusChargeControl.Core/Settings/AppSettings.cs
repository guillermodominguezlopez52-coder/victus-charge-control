namespace VictusChargeControl.Core.Settings;

/// <summary>
/// Configuración persistida. Los valores siempre se normalizan antes de guardarse o usarse.
/// </summary>
public sealed record AppSettings
{
    public const int MinThreshold = 20;
    public const int MaxThreshold = 100;
    public const int MinGap = 5;
    public const int MinPollSeconds = 10;
    public const int MaxPollSeconds = 300;

    public int UpperThreshold { get; init; } = 80;

    public int LowerThreshold { get; init; } = 40;

    public bool UpperAlertEnabled { get; init; } = true;

    public bool LowerAlertEnabled { get; init; } = true;

    public bool NotificationsEnabled { get; init; } = true;

    public bool StartWithWindows { get; init; }

    public int PollIntervalSeconds { get; init; } = 30;

    public static AppSettings Default { get; } = new();

    /// <summary>
    /// Acota los umbrales a 20-100, garantiza una separación mínima de 5 puntos
    /// y limita el intervalo de consulta a 10-300 segundos.
    /// </summary>
    public AppSettings Normalized()
    {
        var upper = Math.Clamp(UpperThreshold, MinThreshold, MaxThreshold);
        var lower = Math.Clamp(LowerThreshold, MinThreshold, MaxThreshold);

        if (upper - lower < MinGap)
        {
            upper = lower + MinGap;
            if (upper > MaxThreshold)
            {
                upper = MaxThreshold;
                lower = MaxThreshold - MinGap;
            }
        }

        return this with
        {
            UpperThreshold = upper,
            LowerThreshold = lower,
            PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, MinPollSeconds, MaxPollSeconds)
        };
    }
}
