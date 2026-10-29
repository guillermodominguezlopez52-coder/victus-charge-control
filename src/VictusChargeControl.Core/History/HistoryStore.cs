using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VictusChargeControl.Core.History;

/// <summary>
/// Historial en formato JSON Lines (una entrada por línea). Mantiene un tope de
/// entradas y recorta el archivo solo cuando supera ese tope con margen, para no
/// reescribir el archivo en cada evento.
/// </summary>
public sealed class HistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly int _maxEntries;
    private int _count;

    public HistoryStore(string filePath, int maxEntries = 5000)
    {
        FilePath = filePath;
        _maxEntries = Math.Max(2, maxEntries);
        _count = CountLines();
    }

    public string FilePath { get; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VictusChargeControl",
        "history.jsonl");

    /// <summary>Devuelve las últimas <paramref name="max"/> entradas en orden cronológico.</summary>
    public IReadOnlyList<HistoryEntry> LoadRecent(int max)
    {
        if (max <= 0)
        {
            return Array.Empty<HistoryEntry>();
        }

        try
        {
            if (!File.Exists(FilePath))
            {
                return Array.Empty<HistoryEntry>();
            }

            var buffer = new Queue<HistoryEntry>(max);
            foreach (var line in File.ReadLines(FilePath))
            {
                var entry = Deserialize(line);
                if (entry is null)
                {
                    continue;
                }

                buffer.Enqueue(entry);
                if (buffer.Count > max)
                {
                    buffer.Dequeue();
                }
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<HistoryEntry>();
        }
    }

    public void Append(IEnumerable<HistoryEntry> entries)
    {
        var batch = entries as IList<HistoryEntry> ?? entries.ToList();
        if (batch.Count == 0)
        {
            return;
        }

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            foreach (var entry in batch)
            {
                writer.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
            }
        }

        _count += batch.Count;

        // Recorte diferido: reescribir en cada evento sería un desperdicio de disco.
        if (_count > _maxEntries * 1.1)
        {
            Trim();
        }
    }

    private void Trim()
    {
        var keep = LoadRecent(_maxEntries);
        var temporary = FilePath + ".tmp";

        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            foreach (var entry in keep)
            {
                writer.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
            }
        }

        File.Move(temporary, FilePath, overwrite: true);
        _count = keep.Count;
    }

    private static HistoryEntry? Deserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HistoryEntry>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private int CountLines()
    {
        try
        {
            return File.Exists(FilePath)
                ? File.ReadLines(FilePath).Count(line => !string.IsNullOrWhiteSpace(line))
                : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
