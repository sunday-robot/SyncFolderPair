using SyncFolderPair.Core.Types;
using System.Diagnostics;

namespace SyncFolderPair.Core.Services;

/// <summary>
/// 二つのディレクトリで差異(*)のあるファイルを列挙するもの。<br/>
/// (*) ファイルの内容比較などはせず、単に更新日時が違う場合に差異があるとするだけ。
/// </summary>
public static class DifferentEntriesEnumerator
{
    public static IEnumerable<DifferentEntryPair> Enumerate(string leftDirectoryPath, string rightDirectoryPath)
        => Enumerate("", EntryPairsEnumerator.Enumerate(path => File.GetLastWriteTimeUtc(path), leftDirectoryPath, rightDirectoryPath));

    static IEnumerable<DifferentEntryPair> Enumerate(string path, IEnumerable<EntryPair> entryPairEnumerable)
    {
        foreach (var entryPair in entryPairEnumerable)
        {
            var dep = CreateDifferentEntryPair(Path.Combine(path, entryPair.Name), entryPair);
            if (dep != null)
                yield return dep;
        }
    }

    static DifferentEntryPair? CreateDifferentEntryPair(string path, EntryPair entryPair)
    {
        switch (entryPair)
        {
            case EntryPair.DirNone c:
                return new DifferentEntryPair.Dir(entryPair.Name, Enumerate(path, c.Children));
            case EntryPair.FileNone:
                return new DifferentEntryPair.FileNone(entryPair.Name);
            case EntryPair.DirFile:
                return new DifferentEntryPair.DirFile(entryPair.Name);
            case EntryPair.DirDir c:
                return new DifferentEntryPair.Dir(entryPair.Name, Enumerate(path, c.Children));
            case EntryPair.FileFile c:
                {
                    var left = (DateTime)c.Left!;
                    var right = (DateTime)c.Right!;
                    if (left != right)
                        return new DifferentEntryPair.Differ(entryPair.Name, left, right);
                    return null;
                }
            case EntryPair.FileDir:
                return new DifferentEntryPair.FileDir(entryPair.Name);
            case EntryPair.NoneDir c:
                return new DifferentEntryPair.Dir(entryPair.Name, Enumerate(path, c.Children));
            case EntryPair.NoneFile:
                return new DifferentEntryPair.NoneFile(entryPair.Name);
            default:
                throw new UnreachableException();
        }
    }
}
