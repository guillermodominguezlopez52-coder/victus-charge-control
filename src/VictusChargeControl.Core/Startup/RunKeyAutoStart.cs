using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VictusChargeControl.Core.Startup;

/// <summary>
/// Inicio automático por usuario. No requiere administrador y no toca nada más
/// que un valor de texto en HKCU.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunKeyAutoStart : IAutoStart
{
    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "VictusChargeControl";

    private readonly string _keyPath;
    private readonly string _command;

    public RunKeyAutoStart(string command, string keyPath = DefaultKeyPath)
    {
        _command = command;
        _keyPath = keyPath;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
                key?.SetValue(ValueName, _command, RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Sin permisos: el ajuste queda como estaba. La UI lo refleja al releer.
        }
    }
}
