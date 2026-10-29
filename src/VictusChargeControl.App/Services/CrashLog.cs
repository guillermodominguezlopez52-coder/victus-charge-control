using System.Text;

namespace VictusChargeControl.App.Services;

/// <summary>
/// Registro mínimo de errores no controlados. Sirve para diagnóstico sin
/// depender del Visor de eventos ni de una consola.
/// </summary>
public static class CrashLog
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VictusChargeControl",
        "error.log");

    public static void Write(Exception exception, string context)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine(new string('-', 80));

            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si ni siquiera se puede escribir el log, no hay nada más que hacer.
        }
    }
}
