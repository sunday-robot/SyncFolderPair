using SyncFolderPair.Types;
using Win32Api;

namespace SyncFolderPair.Utils;

public static class EntryPairs
{
    static readonly Comparison<string> _fileNameComparison = Win32.StrCmpLogicalW;

    public static IEnumerable<EntryPair> Enumerate(
        string leftDirectoryPath,
        string rightDirectoryPath,
        Func<string, object?> createFileInfo)
    {
        return Enumerate(leftDirectoryPath, rightDirectoryPath, createFileInfo, new IgnoreEntries());
    }

    public static IEnumerable<EntryPair> Enumerate(
        string leftDirectoryPath,
        string rightDirectoryPath,
        Func<string, object?> createFileInfo,
        IgnoreEntries ignoreEntries)
    {
        var leftNames = EnumerateEntryNames(leftDirectoryPath, ignoreEntries);
        var rightNames = EnumerateEntryNames(rightDirectoryPath, ignoreEntries);
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
                            yield return new EntryPair.DirDir(leftName, Enumerate(leftPath, rightPath, createFileInfo, ignoreEntries.GetSubEntries(leftName)));
                        else
                            yield return new EntryPair.DirFile(leftName, EnumerateLeft(createFileInfo, leftPath, ignoreEntries.GetSubEntries(leftName)), createFileInfo(rightPath));
                    else
                        if (Directory.Exists(rightPath))
                            yield return new EntryPair.FileDir(leftName, createFileInfo(leftPath), EnumerateRight(createFileInfo, rightPath, ignoreEntries.GetSubEntries(leftName)));
                        else
                            yield return new EntryPair.FileFile(leftName, createFileInfo(leftPath), createFileInfo(rightPath));
                }
                else
                    if (Directory.Exists(leftPath))
                        yield return new EntryPair.DirNone(leftName, EnumerateLeft(createFileInfo, leftPath, ignoreEntries.GetSubEntries(leftName)));
                    else
                        yield return new EntryPair.FileNone(leftName, createFileInfo(leftPath));
            }
            else
            {
                var rightPath = Path.Combine(rightDirectoryPath, rightName!);
                if (Directory.Exists(rightPath))
                    yield return new EntryPair.NoneDir(rightName!, EnumerateRight(createFileInfo, rightPath, ignoreEntries.GetSubEntries(rightName!)));
                else
                    yield return new EntryPair.NoneFile(rightName!, createFileInfo(rightPath));
            }
        }
    }

    /// <summary>
    /// 左のディレクトリのコンテンツを列挙する
    /// </summary>
    static IEnumerable<EntryPair> EnumerateLeft(
        Func<string, object?> createFileInfo,
        string directoryPath,
        IgnoreEntries ignoreEntries)
    {
        foreach (var name in EnumerateEntryNames(directoryPath, ignoreEntries))
        {
            var path = Path.Combine(directoryPath, name);
            if (Directory.Exists(path))
                yield return new EntryPair.DirNone(name, EnumerateLeft(createFileInfo, path, ignoreEntries.GetSubEntries(name)));
            else
                yield return new EntryPair.FileNone(name, createFileInfo(path));
        }
    }

    /// <summary>
    /// 右のディレクトリのコンテンツを列挙する
    /// </summary>
    static IEnumerable<EntryPair> EnumerateRight(
        Func<string, object?> createFileInfo,
        string directoryPath,
        IgnoreEntries ignoreEntries)
    {
        foreach (var name in EnumerateEntryNames(directoryPath, ignoreEntries))
        {
            var path = Path.Combine(directoryPath, name);
            if (Directory.Exists(path))
                yield return new EntryPair.NoneDir(name, EnumerateRight(createFileInfo, path, ignoreEntries.GetSubEntries(name)));
            else
                yield return new EntryPair.NoneFile(name, createFileInfo(path));
        }
    }

    /// <summary>
    /// ディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// ただし、ignoreEntriesで指定されたエントリーは列挙しない。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <returns></returns>
    static IEnumerable<string> EnumerateEntryNames(string directoryPath, IgnoreEntries ignoreEntries)
    {
        var names = Directory.EnumerateFileSystemEntries(directoryPath)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !ignoreEntries.Contains(name))
            .ToArray();
        Array.Sort(names, _fileNameComparison);
        return names;
    }
}
