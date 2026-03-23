using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.Text.Json;

namespace SyncFolderPair.Models;

public static class SyncEntriesStorage
{
    const string _directoryName = "syncentries";

    public static SyncEntries? Get(string pairName)
    {
        var filePath = GetFilePath(pairName);
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<SyncEntries>(json) ?? throw new Exception($"{filePath} is invalid.");
    }

    public static void Set(string pairName, SyncEntries entries)
    {
        JsonSaver.Save(GetFilePath(pairName), entries);
    }

    public static void Delete(string pairName)
    {
        var filePath = GetFilePath(pairName);
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    static string GetFilePath(string pairName)
    {
        return Path.Combine(App.DataDirectory, _directoryName, $"{pairName}.json");
    }
}
