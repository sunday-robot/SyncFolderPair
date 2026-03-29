using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectoryPairEntryEnumerator
{
#if false
    record FileProperties(DateTime T, long L);

    static readonly Comparison<string> _fileNameComparison = Win32.StrCmpLogicalW;

    public static IEnumerable<EntryPair> Enumerate(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var leftNames = EnumerateName(leftDirectoryPath, ignoreEntries);
        var rightNames = EnumerateName(rightDirectoryPath, ignoreEntries);
        foreach (var (leftName, rightName) in PairEnumerator.Enumerate(leftNames, rightNames, _fileNameComparison))
        {
            if (leftName != null)
            {
                var leftPath = Path.Combine(leftDirectoryPath, leftName);
                if (rightName != null)
                {
                    var rightPath = Path.Combine(rightDirectoryPath, rightName);
                    if (Directory.Exists(leftPath))
                        if (Directory.Exists(rightPath))
                            yield return new EntryPair.DirDir(leftName, Enumerate(leftPath, rightPath, ignoreEntries.GetSubEntries(leftName)));
                        else
                            yield return new EntryPair.DirFile(leftName, EnumerateLeft(leftPath, ignoreEntries.GetSubEntries(leftName)), CreateFileProperties(rightPath));
                    else
                        if (Directory.Exists(rightPath))
                            yield return new EntryPair.FileDir(leftName, CreateFileProperties(leftPath), EnumerateRight(rightPath, ignoreEntries));
                        else
                            yield return new EntryPair.FileFile(leftName, CreateFileProperties(leftPath), CreateFileProperties(rightPath));
                }
                else
                    if (Directory.Exists(leftPath))
                        yield return new EntryPair.DirNone(leftName, EnumerateLeft(leftPath, ignoreEntries.GetSubEntries(leftName)));
                    else
                        yield return new EntryPair.FileNone(leftName, CreateFileProperties(leftPath));
            }
            else
            {
                var rightPath = Path.Combine(rightDirectoryPath, rightName!);
                if (Directory.Exists(rightPath))
                    yield return new EntryPair.NoneDir(rightName!, EnumerateRight(rightPath, ignoreEntries.GetSubEntries(rightName!)));
                else
                    yield return new EntryPair.NoneFile(rightName!, CreateFileProperties(rightPath));
            }
        }
    }

    /// <summary>
    /// 左のディレクトリのコンテンツを列挙する
    /// </summary>
    static IEnumerable<EntryPair> EnumerateLeft(
        string directoryPath,
        IgnoreEntries ignoreEntries)
    {
        foreach (var name in EnumerateName(directoryPath, ignoreEntries))
        {
            var path = Path.Combine(directoryPath, name);
            if (Directory.Exists(path))
                yield return new EntryPair.DirNone(name, EnumerateLeft(path, ignoreEntries.GetSubEntries(name)));
            else
                yield return new EntryPair.FileNone(name, CreateFileProperties(path));
        }
    }

    /// <summary>
    /// 右のディレクトリのコンテンツを列挙する
    /// </summary>
    static IEnumerable<EntryPair> EnumerateRight(
        string directoryPath,
        IgnoreEntries ignoreEntries)
    {
#if false
        foreach (var name in EntryEnumerator.Enumerate(directoryPath, ignoreEntries))
        {
            var path = Path.Combine(directoryPath, name);
            if (Directory.Exists(path))
                yield return new EntryPair.NoneDir(name, EnumerateRight(path, ignoreEntries.GetSubEntries(name)));
            else
                yield return new EntryPair.NoneFile(name, CreateFileProperties(path));
        }
#endif
    }

    static FileProperties CreateFileProperties(string path)
    {
        var t = File.GetLastWriteTimeUtc(path);
        var l = (new FileInfo(path)).Length;
        return new FileProperties(t, l);
    }

    static IEnumerable<string> EnumerateName(string directoryPath, IgnoreEntries ignoreEntries)
        => Directory.EnumerateFileSystemEntries(directoryPath)
        .Select(path => Path.GetFileName(path)!)
        .Where(name => !ignoreEntries.Contains(name));
#endif
}
