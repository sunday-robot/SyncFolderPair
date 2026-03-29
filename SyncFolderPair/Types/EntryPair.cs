namespace SyncFolderPair.Types;

public abstract record EntryPair(string Name)
{
    public sealed record NoneDir(string Name, IEnumerable<EntryPair> ChildrenEnumerable) : EntryPair(Name);
    public sealed record NoneFile(string Name, object? FileInfo) : EntryPair(Name);
    public sealed record DirNone(string Name, IEnumerable<EntryPair> ChildrenEnumerable) : EntryPair(Name);
    public sealed record DirDir(string Name, IEnumerable<EntryPair> ChildrenEnumerable) : EntryPair(Name);
    public sealed record DirFile(string Name, IEnumerable<EntryPair> ChildrenEnumerable, object? FileInfo) : EntryPair(Name);
    public sealed record FileNone(string Name, object? FileInfo) : EntryPair(Name);
    public sealed record FileDir(string Name, object? FileInfo, IEnumerable<EntryPair> ChildrenEnumerable) : EntryPair(Name);
    public sealed record FileFile(string Name, object? Left, object? Right) : EntryPair(Name);
}
