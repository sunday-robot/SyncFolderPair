using SyncFolderPair.Types;
using System.Text;

namespace SyncFolderPair.Services
{
    public static class SyncEntrySaver
    {
        public static void Save(string filePath, IEnumerable<SyncEntry> entries)
        {
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            foreach (var entry in entries)
            {
                writer.WriteLine(ToTsv(entry));
            }
        }

        private static string ToTsv(SyncEntry entry)
        {
            var escaped = entry.RelativePath.Replace("\t", "\\t");
            return $"{escaped}\t{entry.LastModifiedUtc:o}\t{entry.Size}";
        }
    }
}
