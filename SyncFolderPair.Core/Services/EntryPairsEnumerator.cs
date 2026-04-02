using SyncFolderPair.Core.Types;
using SyncFolderPair.Core.Utils;
using System.Diagnostics;
using Win32Api;

namespace SyncFolderPair.Core.Services;

public class EntryPairsEnumerator(Func<string, object?> createFileInfo)
{
    #region 公開staticメソッド群
    public static IEnumerable<EntryPair> Enumerate(
        Func<string, object?> createFileInfo,
        string leftDirectoryPath, string rightDirectoryPath)
    {
        return Enumerate(createFileInfo, leftDirectoryPath, rightDirectoryPath, new IgnoreEntries());
    }

    public static IEnumerable<EntryPair> Enumerate(
        Func<string, object?> createFileInfo,
        string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var enumerator = new EntryPairsEnumerator(createFileInfo);
        return enumerator.Enumerate(leftDirectoryPath, rightDirectoryPath, ignoreEntries);
    }
    #endregion 公開staticメソッド群

    #region 通常のクラス定義
    static readonly Comparison<string> _fileNameComparison = Win32.StrCmpLogicalW;

    readonly Func<string, object?> _createFileInfo = createFileInfo;

    IEnumerable<EntryPair> Enumerate(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var leftNames = EnumerateEntryNames(leftDirectoryPath, ignoreEntries);
        var rightNames = EnumerateEntryNames(rightDirectoryPath, ignoreEntries);
        foreach (var pair in PairsEnumerator.Enumerate(leftNames, rightNames, _fileNameComparison))
        {
            switch (pair)
            {
                case Pair<string>.Both x:
                    yield return CreateEntryPair(leftDirectoryPath, rightDirectoryPath, x.LValue, ignoreEntries);
                    break;
                case Pair<string>.Left x:
                    yield return CreateLeftEntryPair(leftDirectoryPath, x.Value, ignoreEntries);
                    break;
                case Pair<string>.Right x:
                    yield return CreateRightEntryPair(rightDirectoryPath, x.Value, ignoreEntries);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    EntryPair CreateEntryPair(string leftDirectoryPath, string rightDirectoryPath, string name, IgnoreEntries ignoreEntries)
    {
        var leftPath = Path.Combine(leftDirectoryPath, name);
        var rightPath = Path.Combine(rightDirectoryPath, name);
        if (Directory.Exists(leftPath))
            if (Directory.Exists(rightPath))
                return new EntryPair.DirDir(name, Enumerate(leftPath, rightPath, ignoreEntries.GetSubEntries(name)));
            else
                return new EntryPair.DirFile(name, EnumerateLeft(leftPath, ignoreEntries.GetSubEntries(name)), _createFileInfo(rightPath));
        else
            if (Directory.Exists(rightPath))
                return new EntryPair.FileDir(name, _createFileInfo(leftPath), EnumerateRight(rightPath, ignoreEntries.GetSubEntries(name)));
            else
                return new EntryPair.FileFile(name, _createFileInfo(leftPath), _createFileInfo(rightPath));
    }

    /// <summary>
    /// 左のディレクトリのコンテンツを列挙する
    /// </summary>
    IEnumerable<EntryPair> EnumerateLeft(string directoryPath, IgnoreEntries ignoreEntries)
    {
        foreach (var name in EnumerateEntryNames(directoryPath, ignoreEntries))
            yield return CreateLeftEntryPair(directoryPath, name, ignoreEntries);
    }

    /// <summary>
    /// 右のディレクトリのコンテンツを列挙する
    /// </summary>
    IEnumerable<EntryPair> EnumerateRight(string directoryPath, IgnoreEntries ignoreEntries)
    {
        foreach (var name in EnumerateEntryNames(directoryPath, ignoreEntries))
            yield return CreateRightEntryPair(directoryPath, name, ignoreEntries);
    }

    EntryPair CreateLeftEntryPair(string directoryPath, string name, IgnoreEntries ignoreEntries)
    {
        var path = Path.Combine(directoryPath, name);
        if (Directory.Exists(path))
            return new EntryPair.DirNone(name, EnumerateLeft(path, ignoreEntries.GetSubEntries(name)));
        else
            return new EntryPair.FileNone(name, _createFileInfo(path));
    }

    EntryPair CreateRightEntryPair(string directoryPath, string name, IgnoreEntries ignoreEntries)
    {
        var path = Path.Combine(directoryPath, name);
        if (Directory.Exists(path))
            return new EntryPair.NoneDir(name, EnumerateRight(path, ignoreEntries.GetSubEntries(name)));
        else
            return new EntryPair.NoneFile(name, _createFileInfo(path));
    }

    /// <summary>
    /// ディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// ただし、ignoreEntriesで指定されたエントリーは列挙しない。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <returns></returns>
    static string[] EnumerateEntryNames(string directoryPath, IgnoreEntries ignoreEntries)
    {
        var names = Directory.EnumerateFileSystemEntries(directoryPath)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !ignoreEntries.Contains(name))
            .ToArray();
        Array.Sort(names, _fileNameComparison);
        return names;
    }
    #endregion 通常のクラス定義
}
