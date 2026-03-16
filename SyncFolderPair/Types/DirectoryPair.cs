namespace SyncFolderPair.Types;

public sealed record DirectoryPair(
    string Name,
    string LeftDirectory,
    string RightDirectory,
    IgnoreEntries IgnoreDirectories)
{
    public DirectoryPair()
        : this(string.Empty, string.Empty, string.Empty, new IgnoreEntries()) { }

    public DirectoryPair(string name, string leftDirectory, string rightDirectory)
        : this(name, leftDirectory, rightDirectory, new IgnoreEntries()) { }
}
