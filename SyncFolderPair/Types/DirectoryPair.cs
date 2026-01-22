namespace SyncFolderPair.Types;

public sealed class DirectoryPair
{
    public string Name { get; set; }
    public string LeftDirectory { get; set; }
    public string RightDirectory { get; set; }
    public HashSet<string> IgnoreDirectoryPathSet { get; set; }

    public DirectoryPair()
    {
        Name = string.Empty;
        LeftDirectory = string.Empty;
        RightDirectory = string.Empty;
        IgnoreDirectoryPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public DirectoryPair(
        string name,
        string leftDirectory,
        string rightDirectory,
        IEnumerable<string> ignoreDirectoryPathSet
    )
    {
        Name = name;
        LeftDirectory = leftDirectory;
        RightDirectory = rightDirectory;
        IgnoreDirectoryPathSet =
            new HashSet<string>(ignoreDirectoryPathSet, StringComparer.OrdinalIgnoreCase);
    }
}
