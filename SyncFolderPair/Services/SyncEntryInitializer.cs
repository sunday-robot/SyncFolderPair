using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.Diagnostics;

namespace SyncFolderPair.Services;

public static class SyncEntryInitializer
{
    public static SyncEntries Initialize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var syncEntries = CreateSyncEntries(leftDirectoryPath, rightDirectoryPath, "", EntryPairs.Enumerate(leftDirectoryPath, rightDirectoryPath, CreateFileProperties, ignoreEntries))
            ?? throw new Exception("Synchronization initialization failed due to directory differences.");
        return syncEntries;
    }

    record FileProperties(DateTime T, long L);
    static FileProperties CreateFileProperties(string path)
    {
        var t = File.GetLastWriteTimeUtc(path);
        var l = (new FileInfo(path)).Length;
        return new FileProperties(t, l);
    }

    static SyncEntries? CreateSyncEntries(string leftBasePath, string rightBasePath, string path, IEnumerable<EntryPair> entryPairs)
    {
        var directorySyncEntry = new SyncEntries();
        var errorOccurred = false;
        foreach (var e in entryPairs)
        {
            var p = Path.Combine(path, e.Name);
            switch (e)
            {
                case EntryPair.NoneDir:
                case EntryPair.NoneFile:
                    // エラー。右にしかない
                    Console.WriteLine($"Error: right only : {p}");
                    errorOccurred = true;
                    break;
                case EntryPair.DirNone:
                case EntryPair.FileNone:
                    // エラー。左にしかない
                    Console.WriteLine($"Error: Left only : {p}");
                    errorOccurred = true;
                    break;

                case EntryPair.DirFile:
                    // エラー。左がディレクトリ、右がファイル
                    Console.WriteLine($"Error: left is directory, right is file : {p}");
                    errorOccurred = true;
                    break;
                case EntryPair.FileDir:
                    // エラー。左がファイル、右がディレクトリ
                    Console.WriteLine($"Error: left is file, right is directory : {p}");
                    errorOccurred = true;
                    break;

                case EntryPair.DirDir x:
                    // どちらもディレクトリ
                    var childEntries = CreateSyncEntries(leftBasePath, rightBasePath, p, x.Children);
                    if (childEntries == null)
                    {
                        errorOccurred = true;
                        break;
                    }
                    directorySyncEntry.Add(e.Name, childEntries);
                    break;

                case EntryPair.FileFile:
                    // どちらもファイル
                    var r = FileComparator.Compare(Path.Combine(leftBasePath, p), Path.Combine(rightBasePath, p));
                    if (r is not FileCompareResult.Same same)
                    {
                        // エラー。異なるファイル(更新日時が異なる)
                        switch (r)
                        {
                            case FileCompareResult.LeftIsNewer a:
                                Console.WriteLine($"Error: Left is newer : {p} (left: {a.Left}, right: {a.Right})");
                                break;
                            case FileCompareResult.RightIsNewer a:
                                Console.WriteLine($"Right is newer : {p} (left: {a.Left}, right: {a.Right})");
                                break;
                            default:
                                throw new UnreachableException();
                        }
                        errorOccurred = true;
                        continue;
                    }
                    directorySyncEntry.Add(e.Name, new SyncEntriesLeaf(same.LastWriteTimeUtc));
                    break;
            }
        }
        if (errorOccurred)
        {
            return null;
        }
        return directorySyncEntry;
    }
}
