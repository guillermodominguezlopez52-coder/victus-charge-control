using System.Runtime.Versioning;
using Microsoft.Win32;
using VictusChargeControl.Core.Startup;
using Xunit;

namespace VictusChargeControl.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class RunKeyAutoStartTests
{
    private readonly string _keyPath = @"Software\VictusChargeControlTests\" + Guid.NewGuid().ToString("N");

    [Fact]
    public void SetEnabled_AltaYBaja_ReflejaElEstado()
    {
        try
        {
            var autoStart = new RunKeyAutoStart("\"C:\\ruta\\VictusChargeControl.exe\" --tray", _keyPath);

            Assert.False(autoStart.IsEnabled);

            autoStart.SetEnabled(true);
            Assert.True(autoStart.IsEnabled);

            using (var key = Registry.CurrentUser.OpenSubKey(_keyPath))
            {
                Assert.Equal(
                    "\"C:\\ruta\\VictusChargeControl.exe\" --tray",
                    key?.GetValue(RunKeyAutoStart.ValueName));
            }

            autoStart.SetEnabled(false);
            Assert.False(autoStart.IsEnabled);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void IsEnabled_ConClaveInexistente_EsFalso()
    {
        var autoStart = new RunKeyAutoStart("comando", _keyPath);

        Assert.False(autoStart.IsEnabled);
    }
}
