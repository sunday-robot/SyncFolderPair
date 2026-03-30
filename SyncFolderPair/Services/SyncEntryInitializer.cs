using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.Diagnostics;

namespace SyncFolderPair.Services;

public static class SyncEntryInitializer
{
    public static SyncEntries Initialize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var syncEntries = CreateSyncEntries("", EntryPairs.Enumerate(leftDirectoryPath, rightDirectoryPath, path => File.GetLastWriteTimeUtc(path), ignoreEntries))
            ?? throw new Exception("Synchronization initialization failed due to directory differences.");
        return syncEntries;
    }

    static SyncEntries? CreateSyncEntries(string path, IEnumerable<EntryPair> entryPairs)
    {
        var syncEntries = new SyncEntries();
        var errorOccurred = false;
        foreach (var e in entryPairs)
        {
            var syncEntry = CreateSyncEntry(Path.Combine(path, e.Name), e);
            if (syncEntry == null)
            {
                errorOccurred = true;
                continue;
            }
            syncEntries.Add(e.Name, syncEntry);
        }
        if (errorOccurred)
            return null;
        return syncEntries;
    }

    static SyncEntriesNode? CreateSyncEntry(string path, EntryPair entryPair)
    {
        switch (entryPair)
        {
            case EntryPair.NoneDir:
            case EntryPair.NoneFile:
                // エラー。右にしかない
                Console.WriteLine($"Error: right only : {path}");
                return null;
            case EntryPair.DirNone:
            case EntryPair.FileNone:
                // エラー。左にしかない
                Console.WriteLine($"Error: Left only : {path}");
                return null;

            case EntryPair.DirFile:
                // エラー。左がディレクトリ、右がファイル
                Console.WriteLine($"Error: left is directory, right is file : {path}");
                return null;
            case EntryPair.FileDir:
                // エラー。左がファイル、右がディレクトリ
                Console.WriteLine($"Error: left is file, right is directory : {path}");
                return null;

            case EntryPair.DirDir x:
                // どちらもディレクトリ
                return CreateSyncEntries(Path.Combine(path, x.Name), x.Children);

            case EntryPair.FileFile x:
                // どちらもファイル
                var lt = (DateTime)x.Left!;
                var rt = (DateTime)x.Right!;
                if (lt != rt)
                {
                    // エラー。別ファイルである(更新日時が異なる)
                    if (lt > rt)
                        Console.WriteLine($"Error: Left is newer : {path} (left: {lt}, right: {rt})");
                    else
                        Console.WriteLine($"Error: Right is newer : {path} (left: {lt}, right: {rt})");
                    return null;
                }
                return new SyncEntriesLeaf(lt);
            default:
                throw new UnreachableException();
        }
    }
}
