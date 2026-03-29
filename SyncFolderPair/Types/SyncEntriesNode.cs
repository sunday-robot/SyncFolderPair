using System.Text.Json.Serialization;

namespace SyncFolderPair.Types;

// TODO 以下のほうが良い？
public class SyncEntries2 : List<(string Name, SyncEntry Entry)>;
public abstract record SyncEntry
{
    public record Directory(SyncEntries2 Entries) : SyncEntry;
    public record File(DateTime LastWriteTimeUtc, long Size) : SyncEntry;
}

/// <summary>
/// 派生クラスをきちんとシリアライズ/デシリアライズするための指定
/// </summary>
[JsonDerivedType(typeof(SyncEntriesLeaf), nameof(SyncEntriesLeaf))]
[JsonDerivedType(typeof(SyncEntries), nameof(SyncEntries))]
public abstract record SyncEntriesNode;

public sealed record SyncEntriesLeaf(DateTime LastModifiedUtc) : SyncEntriesNode();

public sealed record SyncEntries(IDictionary<string, SyncEntriesNode> Nodes) : SyncEntriesNode
{
    public SyncEntries() : this(new Dictionary<string, SyncEntriesNode>(StringComparer.OrdinalIgnoreCase)) { }
    public SyncEntriesNode? Get(string name) => !Nodes.TryGetValue(name, out var node) ? null : node;

    // TODO 以下は、Setにして、引数がnullだったらセットせず、逆にあったら削除するようにしたほうが良いかも。
    public void Add(string name, SyncEntriesNode node) => Nodes[name] = node;
}
