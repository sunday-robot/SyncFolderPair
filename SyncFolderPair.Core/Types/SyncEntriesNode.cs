using System.Text.Json.Serialization;

namespace SyncFolderPair.Core.Types;

public class SyncEntries() : Dictionary<string, SyncEntryContent>(StringComparer.OrdinalIgnoreCase)
{
    public SyncEntryContent? Get(string name) => !TryGetValue(name, out var content) ? null : content;
}

[JsonDerivedType(typeof(SyncEntryContent.Directory), nameof(SyncEntryContent.Directory))]
[JsonDerivedType(typeof(SyncEntryContent.File), nameof(SyncEntryContent.File))]
public abstract record SyncEntryContent
{
    public record Directory(SyncEntries Children) : SyncEntryContent;
    public record File(DateTime LastWriteTimeUtc) : SyncEntryContent;
}
