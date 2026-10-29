namespace VictusChargeControl.Core.Model;

public static class ChargeZoneCalculator
{
    /// <summary>
    /// Clasifica un porcentaje en la zona correspondiente. Si los umbrales están cruzados
    /// (configuración inválida), la zona superior tiene prioridad para no duplicar alertas.
    /// </summary>
    public static ChargeZone GetZone(int? percent, int lower, int upper)
    {
        if (percent is not int value || value is < 0 or > 100)
        {
            return ChargeZone.Unknown;
        }

        if (value >= upper)
        {
            return ChargeZone.AboveUpper;
        }

        if (value <= lower)
        {
            return ChargeZone.BelowLower;
        }

        return ChargeZone.Within;
    }
}
