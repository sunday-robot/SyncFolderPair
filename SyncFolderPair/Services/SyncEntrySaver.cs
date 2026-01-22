using SyncFolderPair.Types;
using System.Text;

namespace SyncFolderPair.Services;

public static class SyncEntrySaver
{
    public static void Save(string filePath, IEnumerable<SyncEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var entry in entries)
        {
            writer.WriteLine(ToTsv(entry));
        }
    }

    static string ToTsv(SyncEntry entry)
    {
        return $"{entry.RelativePath}\t{entry.LastModifiedUtc:o}\t{entry.Size}";
    }
}
