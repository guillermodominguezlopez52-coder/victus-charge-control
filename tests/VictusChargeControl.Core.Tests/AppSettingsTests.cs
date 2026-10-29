using VictusChargeControl.Core.Settings;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void LosValoresPorDefecto_CoincidenConLaEspecificacion()
    {
        var settings = new AppSettings();

        Assert.Equal(80, settings.UpperThreshold);
        Assert.Equal(40, settings.LowerThreshold);
        Assert.True(settings.UpperAlertEnabled);
        Assert.True(settings.LowerAlertEnabled);
        Assert.True(settings.NotificationsEnabled);
        Assert.False(settings.StartWithWindows);
        Assert.InRange(settings.PollIntervalSeconds, 10, 300);
    }

    [Theory]
    [InlineData(150, 40, 100, 40)]
    [InlineData(10, 5, 25, 20)]
    [InlineData(80, 78, 83, 78)]
    [InlineData(80, 40, 80, 40)]
    [InlineData(100, 99, 100, 95)]
    public void Normalized_CorrigeValoresFueraDeRango(int upper, int lower, int expectedUpper, int expectedLower)
    {
        var normalized = new AppSettings { UpperThreshold = upper, LowerThreshold = lower }.Normalized();

        Assert.Equal(expectedUpper, normalized.UpperThreshold);
        Assert.Equal(expectedLower, normalized.LowerThreshold);
        Assert.True(normalized.UpperThreshold - normalized.LowerThreshold >= 5);
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(301, 300)]
    [InlineData(45, 45)]
    public void Normalized_AcotaElIntervaloDeConsulta(int input, int expected)
    {
        Assert.Equal(expected, new AppSettings { PollIntervalSeconds = input }.Normalized().PollIntervalSeconds);
    }
}
