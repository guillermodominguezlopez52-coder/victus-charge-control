using VictusChargeControl.Core.BatteryReport;
using Xunit;

namespace VictusChargeControl.Core.Tests;

public class BatteryReportParserTests
{
    private const string RealReport = """
        <?xml version="1.0" encoding="utf-8"?>
        <BatteryReport xmlns="http://schemas.microsoft.com/battery/2012">
          <ReportInformation>
            <ReportGuid>be3d9813-c3df-4d2e-a87a-ea1c60ce3d72</ReportGuid>
            <ScanTime>2026-10-29T06:59:05Z</ScanTime>
          </ReportInformation>
          <Batteries>
            <Battery>
              <Id>Primary</Id>
              <Manufacturer>HP</Manufacturer>
              <Chemistry>LION</Chemistry>
              <DesignCapacity>64988</DesignCapacity>
              <FullChargeCapacity>64988</FullChargeCapacity>
              <CycleCount>101</CycleCount>
            </Battery>
          </Batteries>
          <RuntimeEstimates>
            <DesignCapacity>
              <Capacity>64988</Capacity>
            </DesignCapacity>
          </RuntimeEstimates>
        </BatteryReport>
        """;

    [Fact]
    public void Parse_ConReporteReal_DevuelveLosTresValores()
    {
        var info = BatteryReportParser.Parse(RealReport);

        Assert.NotNull(info);
        Assert.Equal(64988, info!.DesignCapacityMWh);
        Assert.Equal(64988, info.FullChargeCapacityMWh);
        Assert.Equal(101, info.CycleCount);
    }

    [Fact]
    public void Parse_SinCiclos_DevuelveNullEnCiclosYNoCero()
    {
        var xml = RealReport.Replace("<CycleCount>101</CycleCount>", string.Empty);

        var info = BatteryReportParser.Parse(xml);

        Assert.NotNull(info);
        Assert.Null(info!.CycleCount);
        Assert.Equal(64988, info.DesignCapacityMWh);
    }

    [Fact]
    public void Parse_SinCapacidadDeDiseno_DevuelveNull()
    {
        var xml = RealReport.Replace("<DesignCapacity>64988</DesignCapacity>", string.Empty);

        Assert.Null(BatteryReportParser.Parse(xml));
    }

    [Fact]
    public void Parse_SinBaterias_DevuelveNull()
    {
        var xml = """
            <BatteryReport xmlns="http://schemas.microsoft.com/battery/2012">
              <Batteries />
            </BatteryReport>
            """;

        Assert.Null(BatteryReportParser.Parse(xml));
    }

    [Fact]
    public void Parse_ConXmlInvalido_DevuelveNullSinLanzar()
    {
        Assert.Null(BatteryReportParser.Parse("<BatteryReport><Batteries>"));
        Assert.Null(BatteryReportParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_IgnoraElNamespace()
    {
        var xml = """
            <BatteryReport>
              <Batteries>
                <Battery>
                  <DesignCapacity>70000</DesignCapacity>
                  <FullChargeCapacity>65000</FullChargeCapacity>
                  <CycleCount>12</CycleCount>
                </Battery>
              </Batteries>
            </BatteryReport>
            """;

        var info = BatteryReportParser.Parse(xml);

        Assert.NotNull(info);
        Assert.Equal(70000, info!.DesignCapacityMWh);
        Assert.Equal(65000, info.FullChargeCapacityMWh);
        Assert.Equal(12, info.CycleCount);
    }
}
