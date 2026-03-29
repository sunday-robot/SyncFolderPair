using SyncFolderPair.Types;
using SyncFolderPair.Utils;

namespace SyncFolderPair.Services;

/// <summary>
/// 二つのディレクトリで差異(*)のあるファイルを列挙するもの。<br/>
/// (*) ファイルの内容比較などはせず、単に更新日時が違う場合に差異があるとするだけ。
/// </summary>
public static class DifferentEntryEnumerator
{
    public static IEnumerable<DifferentEntryPair> Enumerate(string leftDirectoryPath, string rightDirectoryPath)
    {
        return Enumerate("", EntryPairs.Enumerate(leftDirectoryPath, rightDirectoryPath, path => File.GetLastWriteTimeUtc(path)));
    }

    static IEnumerable<DifferentEntryPair> Enumerate(string path, IEnumerable<EntryPair> entryPairEnumerable)
    {
        foreach (var e in entryPairEnumerable)
        {
            var rel = Path.Combine(path, e.Name);
            switch (e)
            {
                case EntryPair.DirNone c:
                    yield return new DifferentEntryPair.Dir(e.Name, Enumerate(rel, c.ChildrenEnumerable));
                    break;
                case EntryPair.FileNone:
                    yield return new DifferentEntryPair.FileNone(e.Name);
                    break;
                case EntryPair.DirFile:
                    yield return new DifferentEntryPair.DirFile(e.Name);
                    break;
                case EntryPair.DirDir c:
                    yield return new DifferentEntryPair.Dir(e.Name, Enumerate(rel, c.ChildrenEnumerable));
                    break;
                case EntryPair.FileFile c:
                    {
                        var left = (DateTime) c.Left!;
                        var right = (DateTime) c.Right!;
                        if (left != right)
                            yield return new DifferentEntryPair.Differ(e.Name, left, right);
                    }
                    break;
                case EntryPair.FileDir:
                    yield return new DifferentEntryPair.FileDir(e.Name);
                    break;
                case EntryPair.NoneDir c:
                    yield return new DifferentEntryPair.Dir(e.Name, Enumerate(rel, c.ChildrenEnumerable));
                    break;
                case EntryPair.NoneFile:
                    yield return new DifferentEntryPair.NoneFile(e.Name);
                    break;
            }
        }
    }
}
