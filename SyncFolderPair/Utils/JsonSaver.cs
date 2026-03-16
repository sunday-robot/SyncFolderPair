using System.Text.Json;

namespace SyncFolderPair.Utils;

public static class JsonSaver
{
    static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public static void Save(string filePath, object entries)
    {
        var json = JsonSerializer.Serialize(entries, _jsonSerializerOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, json);
    }
}
