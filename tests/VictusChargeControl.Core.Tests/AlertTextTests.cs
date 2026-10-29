using VictusChargeControl.Core.Alerts;
using VictusChargeControl.Core.Model;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class AlertTextTests
{
    [Fact]
    public void LaAlertaSuperior_EsExplicitaSobreElAlcance()
    {
        var text = AlertText.Build(AlertKind.UpperThreshold, 87, new AlertThresholds(80, 40));

        Assert.Contains("87", text.Message);
        Assert.Contains("80", text.Message);
        Assert.Contains("no detiene la carga", text.Message);
        Assert.Contains("cargador", text.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LaAlertaInferior_IndicaElIntervalo()
    {
        var text = AlertText.Build(AlertKind.LowerThreshold, 38, new AlertThresholds(80, 40));

        Assert.Contains("38", text.Message);
        Assert.Contains("40", text.Message);
        Assert.Contains("80", text.Message);
        Assert.Contains("conect", text.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LosTitulos_DistinguenSuperiorEInferior()
    {
        var upper = AlertText.Build(AlertKind.UpperThreshold, 90, new AlertThresholds(80, 40));
        var lower = AlertText.Build(AlertKind.LowerThreshold, 30, new AlertThresholds(80, 40));

        Assert.NotEqual(upper.Title, lower.Title);
    }
}
