namespace SyncFolderPair.Core.Types;

public sealed record DirectoryPair(
    string Name,
    string LeftDirectoryPath,
    string RightDirectoryPath,
    IgnoreEntries IgnoreEntries)
{
    public DirectoryPair()
        : this(string.Empty, string.Empty, string.Empty, new IgnoreEntries()) { }

    public DirectoryPair(string name, string leftDirectoryPath, string rightDirectoryPath)
        : this(name, leftDirectoryPath, rightDirectoryPath, new IgnoreEntries()) { }
}
