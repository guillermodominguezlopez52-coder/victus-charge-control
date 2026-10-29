namespace VictusChargeControl.Core.Model;

/// <summary>Zona de carga respecto del intervalo objetivo.</summary>
public enum ChargeZone
{
    /// <summary>Sin dato de porcentaje válido.</summary>
    Unknown = 0,

    /// <summary>Por encima del umbral superior.</summary>
    AboveUpper = 1,

    /// <summary>Dentro del intervalo objetivo.</summary>
    Within = 2,

    /// <summary>Por debajo del umbral inferior.</summary>
    BelowLower = 3
}
