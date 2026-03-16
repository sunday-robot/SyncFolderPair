using SyncFolderPair.Types;
using static SyncFolderPair.Utils.PairEnumerator;

namespace SyncFolderPair.Utils;

internal class EntryEnumerator
{
    /// <summary>
    /// 二つのディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// ただし、ignoreEntriesで指定されたエントリーは列挙しない。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <returns></returns>
    public static IEnumerable<(string, Existance)> Enumerate(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var leftNames = EnumerateOnly(leftDirectoryPath, ignoreEntries).ToArray();
        var rightNames = EnumerateOnly(rightDirectoryPath, ignoreEntries).ToArray();
        return PairEnumerator.Enumerate(leftNames, rightNames, NaturalStringComparer.Instance);
    }

    /// <summary>
    /// 二つのディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <returns></returns>
    public static IEnumerable<(string, Existance)> Enumerate(string leftDirectoryPath, string rightDirectoryPath)
    {
        var leftNames = EnumerateOnly(leftDirectoryPath).ToArray();
        var rightNames = EnumerateOnly(rightDirectoryPath).ToArray();
        return PairEnumerator.Enumerate(leftNames, rightNames, NaturalStringComparer.Instance);
    }

    /// <summary>
    /// ディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// ただし、ignoreEntriesで指定されたエントリーは列挙しない。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <returns></returns>
    public static IEnumerable<string> Enumerate(string directoryPath, IgnoreEntries ignoreEntries)
    {
        return EnumerateOnly(directoryPath, ignoreEntries).OrderBy(x => x, NaturalStringComparer.Instance);
    }

    /// <summary>
    /// ディレクトリ直下のエントリーの名前を、名前順に列挙する。
    /// (再帰的に処理したりはしないことに注意)
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <returns></returns>
    public static IEnumerable<string> Enumerate(string directoryPath)
    {
        return EnumerateOnly(directoryPath).OrderBy(x => x, NaturalStringComparer.Instance);
    }

    static IEnumerable<string> EnumerateOnly(string directoryPath, IgnoreEntries ignoreEntries)
        => EnumerateOnly(directoryPath)
        .Where(name => !ignoreEntries.Contains(name));

    static IEnumerable<string> EnumerateOnly(string directoryPath)
        => Directory.EnumerateFileSystemEntries(directoryPath)
        .Select(path => Path.GetFileName(path)!);
}
