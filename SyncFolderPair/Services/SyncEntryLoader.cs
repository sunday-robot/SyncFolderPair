using SyncFolderPair.Types;
using System.Globalization;

namespace SyncFolderPair.Services;

public static class SyncEntryLoader
{
    public static IReadOnlyList<SyncEntry> Load(string filePath)
    {
        var list = new List<SyncEntry>();

        if (!File.Exists(filePath))
            throw new Exception("(TODO)syncの前に初期化をして");

        foreach (var line in File.ReadLines(filePath))
            list.Add(ParseLine(line));

        return list;
    }

    private static SyncEntry ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length != 3)
            throw new FormatException($"Invalid TSV line: {line}");

        var relativePath = parts[0];
        var lastModifiedUtc = DateTime.Parse(parts[1], null, DateTimeStyles.RoundtripKind);
        var size = long.Parse(parts[2]);

        return new SyncEntry(relativePath, lastModifiedUtc, size);
    }
}
