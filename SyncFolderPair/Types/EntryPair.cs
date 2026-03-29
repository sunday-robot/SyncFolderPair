namespace SyncFolderPair.Types;

public abstract record EntryPair(string Name)
{
    public interface IHasChildren
    {
        IEnumerable<EntryPair> Children { get; }
    }

    public interface IHasFileInfo
    {
        object? FileInfo { get; }
    }

    public sealed record NoneDir(string Name, IEnumerable<EntryPair> Children) : EntryPair(Name), IHasChildren;
    public sealed record NoneFile(string Name, object? FileInfo) : EntryPair(Name), IHasFileInfo;
    public sealed record DirNone(string Name, IEnumerable<EntryPair> Children) : EntryPair(Name), IHasChildren;
    public sealed record DirDir(string Name, IEnumerable<EntryPair> Children) : EntryPair(Name), IHasChildren;
    public sealed record DirFile(string Name, IEnumerable<EntryPair> Children, object? FileInfo) : EntryPair(Name), IHasChildren, IHasFileInfo;
    public sealed record FileNone(string Name, object? FileInfo) : EntryPair(Name), IHasFileInfo;
    public sealed record FileDir(string Name, object? FileInfo, IEnumerable<EntryPair> Children) : EntryPair(Name), IHasChildren, IHasFileInfo;
    public sealed record FileFile(string Name, object? Left, object? Right) : EntryPair(Name), IHasFileInfo
    {
        public object? FileInfo => Left;
    }
}
