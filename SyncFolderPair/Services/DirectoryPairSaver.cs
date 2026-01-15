using SyncFolderPair.Types;
using System.Text.Json;

namespace SyncFolderPair.Services
{
    public static class DirectoryPairSaver
    {
        static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        public static void Save(string filePath, IReadOnlyList<DirectoryPair> pairs)
        {
            var json = JsonSerializer.Serialize(pairs, _jsonSerializerOptions);
            File.WriteAllText(filePath, json);
        }
    }
}
