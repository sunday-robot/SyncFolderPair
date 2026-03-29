using System.Text.Json.Serialization;

namespace SyncFolderPair.Types;

/// <summary>
/// 派生クラスをきちんとシリアライズ/デシリアライズするための指定
/// </summary>
[JsonDerivedType(typeof(IgnoreEntriesLeaf), nameof(IgnoreEntriesLeaf))]
[JsonDerivedType(typeof(IgnoreEntries), nameof(IgnoreEntries))]

public abstract record IgnoreEntriesNode();

public sealed record IgnoreEntriesLeaf() : IgnoreEntriesNode;

public sealed record IgnoreEntries(IDictionary<string, IgnoreEntriesNode> Nodes) : IgnoreEntriesNode
{
    static readonly char[] _PathSeparator = ['/', '\\'];

    static readonly IgnoreEntries _empty = new();

    public IgnoreEntries() : this(new Dictionary<string, IgnoreEntriesNode>(StringComparer.OrdinalIgnoreCase)) { }

    public void Add(string path)
    {
        var path2 = Split(path);
        Add(path2);
    }

    /// <summary>
    /// 無視すべき名前かどうかを返す
    /// </summary>
    /// <param name="entryName"></param>
    /// <returns></returns>
    public bool Contains(string entryName)
        => Nodes.TryGetValue(entryName, out var node) && node is IgnoreEntriesLeaf;

    public IgnoreEntries GetSubEntries(string entryName)
    {
        if (!Nodes.TryGetValue(entryName, out var node))
            return _empty;
        if (node is IgnoreEntriesLeaf)
            return _empty;
        return (IgnoreEntries)node;
    }

    void Add(Span<string> path)
    {
        var head = path[0];
        var body = path[1..path.Length];
        if (body.Length == 0)
        {
            if (Nodes.ContainsKey(head))
            {
                throw new ArgumentException($"Ignore entry already exists: {string.Join(Path.DirectorySeparatorChar, path)}");
            }
            Nodes[head] = new IgnoreEntriesLeaf();
        }
        else
        {
            if (Nodes.TryGetValue(head, out IgnoreEntriesNode? node))
            {
                switch (node)
                {
                    case IgnoreEntriesLeaf:
                        throw new ArgumentException($"Ignore entry already exists: {string.Join(Path.DirectorySeparatorChar, path)}");
                    case IgnoreEntries entries:
                        entries.Add(body);
                        break;
                }
            }
            else
            {
                var newEntries = new IgnoreEntries();
                newEntries.Add(body);
                Nodes[head] = newEntries;
            }
        }
    }

    /// <summary>
    /// 無視パス名を分解する<br/>
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    static string[] Split(string path)
    {
        var path2 = path.Split(_PathSeparator, StringSplitOptions.None);
        for (int i = 0; i < path2.Length; i++)
        {
            var s = path2[i].Trim();
            if (s.Length == 0)
                throw new ArgumentException($"パス名が不正です。{path}");
            path2[i] = s;
        }
        return path2;
    }
}
