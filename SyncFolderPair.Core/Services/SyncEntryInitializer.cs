using SyncFolderPair.Core.Types;
using System.Diagnostics;

namespace SyncFolderPair.Core.Services;

public class SyncEntryInitializer(string leftBasePath, string rightBasePath)
{
    #region 公開staticメソッド群
    public static SyncEntries Initialize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries,
        Action<string /* message */>? errorOccurred)
    {
        var initializer = new SyncEntryInitializer(leftDirectoryPath, rightDirectoryPath);
        initializer.ErrorOccurred += errorOccurred;
        return initializer.Initialize(ignoreEntries);
    }
    #endregion 公開staticメソッド群

    #region 通常のクラス定義
    public event Action<string /* message */>? ErrorOccurred;
    readonly string _leftBasePath = leftBasePath;
    readonly string _rightBasePath = rightBasePath;

    public SyncEntries Initialize(IgnoreEntries ignoreEntries)
    {
        var entryPairs = EntryPairsEnumerator.Enumerate(path => File.GetLastWriteTimeUtc(path), _leftBasePath, _rightBasePath, ignoreEntries);
        var syncEntries = CreateSyncEntries("", entryPairs)
            ?? throw new Exception("Synchronization initialization failed due to directory differences.");
        return syncEntries;
    }

    SyncEntries? CreateSyncEntries(string path, IEnumerable<EntryPair> entryPairs)
    {
        var syncEntries = new SyncEntries();
        var errorOccurred = false;
        foreach (var entryPair in entryPairs)
        {
            var syncEntry = CreateSyncEntry(Path.Combine(path, entryPair.Name), entryPair);
            if (syncEntry == null)
            {
                errorOccurred = true;
                continue;
            }
            syncEntries.Add(entryPair.Name, syncEntry);
        }
        if (errorOccurred)
            return null;
        return syncEntries;
    }

    SyncEntryContent? CreateSyncEntry(string path, EntryPair entryPair)
    {
        switch (entryPair)
        {
            case EntryPair.NoneDir:
            case EntryPair.NoneFile:
                // エラー。右にしかない
                ErrorOccurred?.Invoke($"Error: Right only : {path}");
                return null;
            case EntryPair.DirNone:
            case EntryPair.FileNone:
                // エラー。左にしかない
                ErrorOccurred?.Invoke($"Error: Left only : {path}");
                return null;

            case EntryPair.DirFile:
                // エラー。左がディレクトリ、右がファイル
                ErrorOccurred?.Invoke($"Error: Left is directory, right is file : {path}");
                return null;
            case EntryPair.FileDir:
                // エラー。左がファイル、右がディレクトリ
                ErrorOccurred?.Invoke($"Error: Left is file, right is directory : {path}");
                return null;

            case EntryPair.DirDir x:
                // どちらもディレクトリ
                var newEntries = CreateSyncEntries(path, x.Children);
                if (newEntries == null)
                    return null;
                return new SyncEntryContent.Directory(newEntries);

            case EntryPair.FileFile x:
                // どちらもファイル
                var lt = (DateTime)x.Left!;
                var rt = (DateTime)x.Right!;
                if (lt != rt)
                {
                    // エラー。別ファイルである(更新日時が異なる)
                    if (lt > rt)
                        ErrorOccurred?.Invoke($"Error: Left is newer : {path} (left: {lt}, right: {rt})");
                    else
                        ErrorOccurred?.Invoke($"Error: Right is newer : {path} (left: {lt}, right: {rt})");
                    return null;
                }
                return new SyncEntryContent.File(lt);
            default:
                throw new UnreachableException();
        }
    }
    #endregion 通常のクラス定義
}
