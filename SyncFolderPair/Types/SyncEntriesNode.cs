using System.Text.Json.Serialization;

namespace SyncFolderPair.Types;

#if true
// TODO 以下のほうが良い？
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
#else
/// <summary>
/// 派生クラスをきちんとシリアライズ/デシリアライズするための指定
/// </summary>
[JsonDerivedType(typeof(SyncEntriesLeaf), nameof(SyncEntriesLeaf))]
[JsonDerivedType(typeof(SyncEntries), nameof(SyncEntries))]
public abstract record SyncEntriesNode;

public sealed record SyncEntriesLeaf(DateTime LastWriteTimeUtc) : SyncEntriesNode();

public sealed record SyncEntries(IDictionary<string, SyncEntriesNode> Nodes) : SyncEntriesNode
{
    public SyncEntries() : this(new Dictionary<string, SyncEntriesNode>(StringComparer.OrdinalIgnoreCase)) { }
    public SyncEntriesNode? Get(string name) => !Nodes.TryGetValue(name, out var node) ? null : node;

    public void Add(string name, SyncEntriesNode node) => Nodes[name] = node;
}
#endif
