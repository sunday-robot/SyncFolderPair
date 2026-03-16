using System.Text.Json.Serialization;

namespace SyncFolderPair.Types;

/// <summary>
/// 派生クラスをきちんとシリアライズ/デシリアライズするための指定
/// </summary>
[JsonDerivedType(typeof(SyncEntriesLeaf), nameof(SyncEntriesLeaf))]
[JsonDerivedType(typeof(SyncEntries), nameof(SyncEntries))]
public abstract record SyncEntriesNode;

public sealed record SyncEntriesLeaf(DateTime LastModifiedUtc, long Size) : SyncEntriesNode();

public sealed record SyncEntries(IDictionary<string, SyncEntriesNode> Nodes) : SyncEntriesNode
{
    public SyncEntries() : this(new Dictionary<string, SyncEntriesNode>(StringComparer.OrdinalIgnoreCase)) { }
}
