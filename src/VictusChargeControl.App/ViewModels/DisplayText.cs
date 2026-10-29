using System.Globalization;
using VictusChargeControl.Core.History;
using VictusChargeControl.Core.Model;

namespace VictusChargeControl.App.ViewModels;

/// <summary>Traducciones de datos a texto en español. Un dato ausente se muestra como "—".</summary>
public static class DisplayText
{
    public const string Missing = "—";

    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-AR");

    public static string State(BatteryChargeState state) => state switch
    {
        BatteryChargeState.Charging => "Cargando",
        BatteryChargeState.Discharging => "Descargando",
        BatteryChargeState.AcConnectedIdle => "Conectado sin cargar",
        BatteryChargeState.NotPresent => "Sin batería",
        _ => "Estado desconocido"
    };

    public static string Ac(bool? connected) => connected switch
    {
        true => "Corriente conectada",
        false => "En batería",
        null => "Corriente desconocida"
    };

    public static string Percent(int? percent) => percent is int value ? $"{value} %" : Missing;

    public static string Watts(double? watts)
    {
        if (watts is not double value)
        {
            return Missing;
        }

        var rounded = Math.Round(value, 1);
        var sign = rounded > 0 ? "+" : string.Empty;
        return $"{sign}{rounded.ToString("0.0", Culture)} W";
    }

    public static string Capacity(int? milliwattHours) =>
        milliwattHours is int value && value > 0
            ? $"{Math.Round(value / 1000.0, 1).ToString("0.0", Culture)} Wh"
            : Missing;

    public static string Cycles(int? cycles) => cycles is int value ? value.ToString(Culture) : Missing;

    public static string Health(double? percent) =>
        percent is double value ? $"{Math.Round(value, 0).ToString("0", Culture)} %" : Missing;

    public static string Changes(HistoryChange changes)
    {
        var parts = new List<string>(4);

        if (changes.HasFlag(HistoryChange.FirstSample))
        {
            parts.Add("Inicio");
        }

        if (changes.HasFlag(HistoryChange.StateChange))
        {
            parts.Add("Estado");
        }

        if (changes.HasFlag(HistoryChange.AcChange))
        {
            parts.Add("Corriente");
        }

        if (changes.HasFlag(HistoryChange.ZoneChange))
        {
            parts.Add("Zona");
        }

        if (changes.HasFlag(HistoryChange.PercentChange))
        {
            parts.Add("Carga");
        }

        return parts.Count == 0 ? "Cambio" : string.Join(" · ", parts);
    }
}
