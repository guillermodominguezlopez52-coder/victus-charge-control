using System.Xml;
using System.Xml.Linq;
using VictusChargeControl.Core.Model;

namespace VictusChargeControl.Core.BatteryReport;

/// <summary>
/// Extrae capacidad de diseño, capacidad de carga completa y ciclos del XML que
/// genera <c>powercfg /batteryreport /xml</c>. Cualquier dato ausente queda en
/// <c>null</c>: no se inventan ceros.
/// </summary>
public static class BatteryReportParser
{
    public static BatteryHealthInfo? Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(xml);

            var battery = document
                .Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "Battery" &&
                    element.Elements().Any(child => child.Name.LocalName == "DesignCapacity"));

            if (battery is null)
            {
                return null;
            }

            var design = ReadPositive(battery, "DesignCapacity");
            if (design is null)
            {
                return null;
            }

            return new BatteryHealthInfo(
                design,
                ReadPositive(battery, "FullChargeCapacity"),
                ReadNonNegative(battery, "CycleCount"));
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static int? ReadPositive(XElement parent, string name)
    {
        var value = ReadInt(parent, name);
        return value is > 0 ? value : null;
    }

    private static int? ReadNonNegative(XElement parent, string name)
    {
        var value = ReadInt(parent, name);
        return value is >= 0 ? value : null;
    }

    private static int? ReadInt(XElement parent, string name)
    {
        var element = parent.Elements().FirstOrDefault(child => child.Name.LocalName == name);
        return element is not null && int.TryParse(element.Value.Trim(), out var value) ? value : null;
    }
}
