using VictusChargeControl.Core.Model;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class ChargeZoneCalculatorTests
{
    [Theory]
    [InlineData(80, ChargeZone.AboveUpper)]
    [InlineData(81, ChargeZone.AboveUpper)]
    [InlineData(100, ChargeZone.AboveUpper)]
    [InlineData(79, ChargeZone.Within)]
    [InlineData(41, ChargeZone.Within)]
    [InlineData(40, ChargeZone.BelowLower)]
    [InlineData(39, ChargeZone.BelowLower)]
    [InlineData(0, ChargeZone.BelowLower)]
    public void GetZone_ClasificaSegunLosUmbrales(int percent, ChargeZone expected)
    {
        Assert.Equal(expected, ChargeZoneCalculator.GetZone(percent, lower: 40, upper: 80));
    }

    [Fact]
    public void GetZone_SinPorcentaje_EsDesconocida()
    {
        Assert.Equal(ChargeZone.Unknown, ChargeZoneCalculator.GetZone(null, 40, 80));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void GetZone_PorcentajeFueraDeRango_EsDesconocida(int percent)
    {
        Assert.Equal(ChargeZone.Unknown, ChargeZoneCalculator.GetZone(percent, 40, 80));
    }

    [Fact]
    public void GetZone_UmbralesCruzados_DaPrioridadAlSuperior()
    {
        // Configuración inválida: la zona superior gana para no alertar dos veces por el mismo valor.
        Assert.Equal(ChargeZone.AboveUpper, ChargeZoneCalculator.GetZone(60, lower: 70, upper: 50));
    }
}
