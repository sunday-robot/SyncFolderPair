namespace SyncFolderPair.Core.Types;

public abstract record DifferentEntryPair(string Name)
{
    // ファイルが片方にしかない
    public sealed record FileNone(string Name) : DifferentEntryPair(Name);
    public sealed record NoneFile(string Name) : DifferentEntryPair(Name);

    // 片方がディレクトリ、もう片方がファイル
    public sealed record DirFile(string Name) : DifferentEntryPair(Name);
    public sealed record FileDir(string Name) : DifferentEntryPair(Name);

    // ディレクトリが片方にしかないか、両方ディレクトリ
    public sealed record Dir(string Name, IEnumerable<DifferentEntryPair> ChildrenEnumerable) : DifferentEntryPair(Name);

    // ファイルの更新日時が異なる
    public sealed record Differ(string Name, DateTime Left, DateTime Right) : DifferentEntryPair(Name);
}
