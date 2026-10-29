using System.Text.Json;

namespace VictusChargeControl.Core.Settings;

/// <summary>
/// Persistencia de la configuración en JSON. Si el archivo falta o está corrupto,
/// se devuelven los valores por defecto en lugar de fallar.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SettingsStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VictusChargeControl",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return AppSettings.Default;
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return (settings ?? AppSettings.Default).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = settings.Normalized();
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        var temporary = FilePath + ".tmp";

        File.WriteAllText(temporary, json);
        File.Move(temporary, FilePath, overwrite: true);
    }
}
