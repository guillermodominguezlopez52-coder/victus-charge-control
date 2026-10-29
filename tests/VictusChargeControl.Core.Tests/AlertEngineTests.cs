using VictusChargeControl.Core.Alerts;
using VictusChargeControl.Core.Model;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class AlertEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.FromHours(-6));

    private static BatteryReading Reading(
        int? percent,
        BatteryChargeState state = BatteryChargeState.Discharging,
        bool? ac = null,
        double? power = null) =>
        new(Now)
        {
            Percent = percent,
            State = state,
            AcConnected = ac,
            PowerWatts = power
        };

    private static BatteryReading OnAc(int percent) =>
        Reading(percent, BatteryChargeState.Charging, ac: true);

    private static AlertEngine Engine(int upper = 80, int lower = 40, bool upperEnabled = true, bool lowerEnabled = true) =>
        new(new AlertThresholds(upper, lower, upperEnabled, lowerEnabled));

    [Fact]
    public void AlCruzarElUmbralSuperiorEnAc_EmiteUnaAlerta()
    {
        var alerts = Engine().Evaluate(OnAc(80));

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKind.UpperThreshold, alert.Kind);
        Assert.Equal(80, alert.Percent);
        Assert.Contains("80", alert.Message);
        Assert.Contains("no detiene la carga", alert.Message);
    }

    [Fact]
    public void MientrasSigaEnLaMismaZona_NoRepiteLaAlerta()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(OnAc(80)));

        Assert.Empty(engine.Evaluate(OnAc(81)));
        Assert.Empty(engine.Evaluate(OnAc(95)));
        Assert.Empty(engine.Evaluate(OnAc(80)));
    }

    [Fact]
    public void TrasBajarConHisteresis_VuelveADisparar()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(OnAc(80)));

        // 79 todavía no rearma (histéresis de 1 punto).
        Assert.Empty(engine.Evaluate(OnAc(79)));
        Assert.Empty(engine.Evaluate(OnAc(80)));

        // 78 rearma; volver a 80 alerta de nuevo.
        Assert.Empty(engine.Evaluate(OnAc(78)));
        Assert.Single(engine.Evaluate(OnAc(80)));
    }

    [Fact]
    public void AlCruzarElUmbralInferiorDescargando_EmiteUnaAlerta()
    {
        var alerts = Engine().Evaluate(Reading(40, BatteryChargeState.Discharging, ac: false));

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKind.LowerThreshold, alert.Kind);
        Assert.Contains("40", alert.Message);
    }

    [Fact]
    public void LaAlertaInferiorNoSeRepiteYSeRearmaConHisteresis()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(Reading(40, BatteryChargeState.Discharging, ac: false)));
        Assert.Empty(engine.Evaluate(Reading(39, BatteryChargeState.Discharging, ac: false)));

        // 41 no rearma, 42 sí.
        Assert.Empty(engine.Evaluate(Reading(41, BatteryChargeState.Discharging, ac: false)));
        Assert.Empty(engine.Evaluate(Reading(40, BatteryChargeState.Discharging, ac: false)));
        Assert.Empty(engine.Evaluate(Reading(42, BatteryChargeState.Discharging, ac: false)));
        Assert.Single(engine.Evaluate(Reading(40, BatteryChargeState.Discharging, ac: false)));
    }

    [Fact]
    public void ElUmbralSuperiorNoAlertaSiNoHayCorriente()
    {
        Assert.Empty(Engine().Evaluate(Reading(90, BatteryChargeState.Discharging, ac: false)));
        Assert.Empty(Engine().Evaluate(Reading(90, BatteryChargeState.Unknown, ac: null)));
    }

    [Fact]
    public void ElUmbralSuperiorAlertaTambienConAcSinCarga()
    {
        var alerts = Engine().Evaluate(Reading(90, BatteryChargeState.AcConnectedIdle, ac: true));
        Assert.Single(alerts);
    }

    [Fact]
    public void ElUmbralInferiorNoAlertaSiEstaEnchufado()
    {
        Assert.Empty(Engine().Evaluate(Reading(30, BatteryChargeState.AcConnectedIdle, ac: true)));
    }

    [Fact]
    public void UnaAlertaDeshabilitadaNoDispara()
    {
        Assert.Empty(Engine(upperEnabled: false).Evaluate(OnAc(95)));
        Assert.Empty(Engine(lowerEnabled: false).Evaluate(Reading(20, BatteryChargeState.Discharging, ac: false)));
    }

    [Fact]
    public void DatosDesconocidosNoAlertanNiRearman()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(OnAc(80)));

        Assert.Empty(engine.Evaluate(Reading(null, BatteryChargeState.Unknown, ac: null)));
        Assert.Empty(engine.Evaluate(Reading(null, BatteryChargeState.NotPresent, ac: false)));

        // Si el dato desconocido hubiera rearmado, esto alertaría otra vez.
        Assert.Empty(engine.Evaluate(OnAc(80)));
    }

    [Fact]
    public void BateriaNoPresenteNoAlerta()
    {
        Assert.Empty(Engine().Evaluate(Reading(null, BatteryChargeState.NotPresent, ac: true)));
    }

    [Fact]
    public void Reset_VuelveAAlertar()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(OnAc(85)));

        engine.Reset();

        Assert.Single(engine.Evaluate(OnAc(85)));
    }

    [Fact]
    public void Configure_ConNuevosUmbralesRearmaLasAlertas()
    {
        var engine = Engine();
        Assert.Single(engine.Evaluate(OnAc(85)));

        engine.Configure(new AlertThresholds(90, 40));

        Assert.Single(engine.Evaluate(OnAc(92)));
    }

    [Fact]
    public void LaHisteresisEsConfigurable()
    {
        var engine = new AlertEngine(new AlertThresholds(80, 40), hysteresis: 3);
        Assert.Single(engine.Evaluate(OnAc(80)));

        Assert.Empty(engine.Evaluate(OnAc(77)));
        Assert.Empty(engine.Evaluate(OnAc(80)));

        Assert.Empty(engine.Evaluate(OnAc(76)));
        Assert.Single(engine.Evaluate(OnAc(80)));
    }
}
