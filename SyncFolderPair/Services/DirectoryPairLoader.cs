using SyncFolderPair.Types;
using System.Text.Json;

namespace SyncFolderPair.Services
{
    public static class DirectoryPairLoader
    {
        public static IReadOnlyList<DirectoryPair> Load(string filePath)
        {
            if (!File.Exists(filePath))
                return [];

            var json = File.ReadAllText(filePath);
            var list = JsonSerializer.Deserialize<List<DirectoryPair>>(json) ?? throw new Exception("pairs.json is invalid.");
            return list;
        }
    }
}
