using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public abstract class DirectorySynchronizer(string leftBasePath, string rightBasePath)
{
    #region 公開staticメソッド群
    /// <summary>
    /// 二つのディレクトリの更新内容を互いに反映する。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    /// <returns>今回の更新結果</returns>
    public static SyncEntries Synchronize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries,
        Action<Operation, bool /* isTargetLeft */, string /*path*/> entryOperationStarted,
        Action<string /* message */>? errorOccurred)
    {
        var synchronizer = new Synchronizer(leftDirectoryPath, rightDirectoryPath);
        synchronizer.EntryOperationStarted += entryOperationStarted;
        synchronizer.ErrorOccurred += errorOccurred;
        return synchronizer.Synchronize(ignoreEntries, oldSyncEntries);
    }

    /// <summary>
    /// Synchronizeのプレビューをする。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    public static void CheckSynchronize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries,
        Action<Operation, bool /* isTargetLeft */, string /*path*/> entryOperationStarted,
        Action<string /* message */>? errorOccurred)
    {
        var checker = new Checker(leftDirectoryPath, rightDirectoryPath);
        checker.EntryOperationStarted += entryOperationStarted;
        checker.ErrorOccurred += errorOccurred;
        checker.Synchronize(ignoreEntries, oldSyncEntries);
    }
    #endregion 公開staticメソッド群

    #region 本来の抽象クラス定義
    static readonly SyncEntries _emptySyncEntries = [];

    public event Action<Operation, bool /* isTargetLeft */, string /* path */>? EntryOperationStarted;
    public event Action<string /* message */>? ErrorOccurred;

    readonly string _leftBasePath = leftBasePath;
    readonly string _rightBasePath = rightBasePath;

    public SyncEntries Synchronize(IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        var entryPairs = EntryPairs.Enumerate(_leftBasePath, _rightBasePath, path => File.GetLastWriteTimeUtc(path), ignoreEntries);
        return SynchronizeEntryPairs("", entryPairs, oldSyncEntries);
    }

    SyncEntries SynchronizeEntryPairs(string path, IEnumerable<EntryPair> entryPairs, SyncEntries oldSyncEntries)
    {
        var newSyncEntries = new SyncEntries();
        foreach (var entryPair in entryPairs)
        {
            var newSyncEntryContent = SynchronizeEntryPair(Path.Combine(path, entryPair.Name), entryPair, oldSyncEntries.Get(entryPair.Name));
            if (newSyncEntryContent != null)
                newSyncEntries.Add(entryPair.Name, newSyncEntryContent);
        }
        return newSyncEntries;
    }

    SyncEntryContent? SynchronizeEntryPair(string path, EntryPair entryPair, SyncEntryContent? oldSyncEntryContent)
    {
        return entryPair switch
        {
            EntryPair.DirDir x => SynchronizeDirectory(path, x, oldSyncEntryContent),
            EntryPair.FileFile x => SynchronizeFile(path, x, oldSyncEntryContent),
            EntryPair.DirFile x => SynchronizeDirFile(path, x, oldSyncEntryContent),
            EntryPair.FileDir x => SynchronizeFileDir(path, x, oldSyncEntryContent),
            EntryPair.DirNone x => SynchronizeDirNone(path, x, oldSyncEntryContent),
            EntryPair.NoneDir x => SynchronizeNoneDir(path, x, oldSyncEntryContent),
            EntryPair.FileNone x => SynchronizeFileNone(path, x, oldSyncEntryContent),
            EntryPair.NoneFile x => SynchronizeNoneFile(path, x, oldSyncEntryContent),
            _ => null,// ここに到達することはない
        };
    }

    SyncEntryContent.Directory? SynchronizeDirectory(string path, EntryPair.DirDir dirDir, SyncEntryContent? oldSyncEntryContent)
    {
        return new SyncEntryContent.Directory(SynchronizeEntryPairs(path, dirDir.Children, oldSyncEntryContent switch
        {
            SyncEntryContent.Directory y => y.Children,
            _ => _emptySyncEntries, // null or SyncEntryContent.File
        }));
    }

    SyncEntryContent? SynchronizeFile(string path, EntryPair.FileFile fileFile, SyncEntryContent? oldSyncEntryContent)
    {
        var lt = (DateTime)fileFile.Left!;
        var rt = (DateTime)fileFile.Right!;
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.File y:
                switch (DateTime.Compare(lt, rt))
                {
                    case > 0:
                        if (rt != y.LastWriteTimeUtc)
                        {
                            // 運用ミス。左右で別々に更新された
                            ErrorOccurred?.Invoke($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryContent;
                        }
                        // 左のファイルが更新された
                        return OverwriteFile(false, path);
                    case < 0:
                        if (lt != y.LastWriteTimeUtc)
                        {
                            // 運用ミス。左右で別々に更新された
                            ErrorOccurred?.Invoke($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryContent;
                        }
                        // 右のファイルが更新された
                        return OverwriteFile(true, path);
                    case 0:
                        if (lt != y.LastWriteTimeUtc)
                        {
                            // 特殊運用。ファイルが更新され、手動同期済み
                            return new SyncEntryContent.File(lt);
                        }
                        // ファイルは更新されていない
                        return oldSyncEntryContent;
                }
            default:    // null or SyncEntries
                if (lt != rt)
                {
                    // 運用ミス。
                    // 左右でファイルが新規作成されたが、更新日時が異なる
                    // あるいは、左右でディレクトリが削除され、さらにファイルが新規作成されたが、更新日時が異なる
                    ErrorOccurred?.Invoke($"[Operation Error] File was created on both sides, but they are different: {path}");
                    return oldSyncEntryContent;
                }
                // ファイルが作成され、手動同期済み
                // あるいは、左右ディレクトリが削除され、ファイルが作成され、手動同期済み
                return new SyncEntryContent.File(lt);
        }
    }

    SyncEntryContent? SynchronizeDirFile(string path, EntryPair.DirFile dirFile, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeDifferentType(true, path, dirFile.Children, (DateTime)dirFile.FileInfo!, oldSyncEntryContent);

    SyncEntryContent? SynchronizeFileDir(string path, EntryPair.FileDir fileDir, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeDifferentType(false, path, fileDir.Children, (DateTime)fileDir.FileInfo!, oldSyncEntryContent);

    SyncEntryContent? SynchronizeDifferentType(bool isLeftDirectory, string path, IEnumerable<EntryPair> directoryChildren, DateTime fileLastWriteTimeUtc, SyncEntryContent? oldSyncEntryContent)
    {
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.Directory y:
                if (IsDirectoryUpdated(directoryChildren, y.Children))
                {
                    // 運用ミス。片方のディレクトリが削除され、ファイルが作成されたのに、もう片方のディレクトリが更新されている
                    ErrorOccurred?.Invoke("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + $" while the directory was updated on the other. {path}");
                    return oldSyncEntryContent;
                }
                // 片方のディレクトリが削除され、ファイルが作成された
                // もう片方のディレクトリを削除し、ファイルをコピーする
                DeleteDirectory(isLeftDirectory, path, directoryChildren);
                return CopyFile(isLeftDirectory, path);
            case SyncEntryContent.File y:
                if (fileLastWriteTimeUtc != y.LastWriteTimeUtc)
                {
                    // 運用ミス。片方のファイルが削除され、ディレクトリが作成されたのに、もう片方のファイルが更新されている
                    ErrorOccurred?.Invoke("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + $" while the file was updated on the other. {path}");
                    return oldSyncEntryContent;
                }
                // 片方のファイルが削除され、ディレクトリが作成された
                // もう片方のファイルを削除し、ディレクトリをコピーする
                DeleteFile(!isLeftDirectory, path);
                CreateDirectory(!isLeftDirectory, path);
                return new SyncEntryContent.Directory(SynchronizeEntryPairs(path, directoryChildren, _emptySyncEntries));
            default:    // null
                // 運用ミス。左右で異なる種類のものが新規作成された
                ErrorOccurred?.Invoke($"[Operation Error] A directory was created on one side and a file on the other. {path}");
                return null;
        }
    }

    SyncEntryContent.Directory? SynchronizeDirNone(string path, EntryPair.DirNone dirNone, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeOrphanDirectory(true, path, dirNone.Children, oldSyncEntryContent);

    SyncEntryContent.Directory? SynchronizeNoneDir(string path, EntryPair.NoneDir noneDir, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeOrphanDirectory(false, path, noneDir.Children, oldSyncEntryContent);

    SyncEntryContent.Directory? SynchronizeOrphanDirectory(bool leftDirectoryExists, string path, IEnumerable<EntryPair> directoryChildren, SyncEntryContent? oldSyncEntryContent)
    {
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.Directory y:
                if (IsDirectoryUpdated(directoryChildren, y.Children))
                {
                    // 運用ミス。片方のディレクトリが削除されたのに、もう片方のディレクトリが更新された
                    ErrorOccurred?.Invoke($"[Operation Error] A directory was deleted on one side, but the directory was updated on the other. {path}");
                    return y;
                }
                // 片方のディレクトリが削除された
                // 残ったディレクトリを削除する
                DeleteDirectory(leftDirectoryExists, path, directoryChildren);
                return null;
            default:    // null or SyncEntriesLeaf
                // 片方にディレクトリが新規作成された
                // あるいは、左右でファイルが削除され、片方にディレクトリが新規作成された
                // もう片方にディレクトリを作成し、ディレクトリの内容をコピーする
                CreateDirectory(!leftDirectoryExists, path);
                return new SyncEntryContent.Directory(SynchronizeEntryPairs(path, directoryChildren, _emptySyncEntries));
        }
    }

    SyncEntryContent.File? SynchronizeFileNone(string path, EntryPair.FileNone fileNone, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeOrphanFile(true, path, (DateTime)fileNone.FileInfo!, oldSyncEntryContent);

    SyncEntryContent.File? SynchronizeNoneFile(string path, EntryPair.NoneFile noneFile, SyncEntryContent? oldSyncEntryContent)
        => SynchronizeOrphanFile(false, path, (DateTime)noneFile.FileInfo!, oldSyncEntryContent);

    SyncEntryContent.File? SynchronizeOrphanFile(bool leftFileExists, string path, DateTime fileLastWriteTimeUtc, SyncEntryContent? oldSyncEntryContent)
    {
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.File y:
                if (fileLastWriteTimeUtc != y.LastWriteTimeUtc)
                {
                    // 運用ミス。片方でファイルが削除されたが、もう片方のファイルは更新されている
                    ErrorOccurred?.Invoke($"[Operation Error] A file was deleted on one side, but the file was updated on the other. {path}");
                    return y;
                }
                // 片方のファイルが削除された
                // 残ったファイルを削除する
                DeleteFile(leftFileExists, path);
                return null;
            default:    // null or SyncEntries
                // 片方にファイルが作成された
                // あるいは、左右のディレクトリが削除され、片方にファイルが作成された
                // 作成されたファイルをコピーする
                return CopyFile(!leftFileExists, path);
        }
    }

    /// <summary>
    /// entryPairsに記載されているディレクトリ、ファイルが更新されているかどうかを返す。<br/>
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ignoreEntries"></param>
    /// <param name="oldSyncEntries"></param>
    /// <returns></returns>
    static bool IsDirectoryUpdated(IEnumerable<EntryPair> entryPairs, SyncEntries oldSyncEntries)
    {
        foreach (var entryPair in entryPairs)
        {
            if (IsEntryUpdated(entryPair, oldSyncEntries.Get(entryPair.Name)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// entryPairに記載されているディレクりあるいはファイルが更新されているかどうかを返す。
    /// </summary>
    /// <param name="entryPair"></param>
    /// <param name="oldSyncEntryContent"></param>
    /// <returns></returns>
    /// <exception cref="UnreachableException"></exception>
    static bool IsEntryUpdated(EntryPair entryPair, SyncEntryContent? oldSyncEntryContent)
    {
        return entryPair switch
        {
            EntryPair.IHasChildren x => oldSyncEntryContent switch
            {
                SyncEntryContent.Directory y => IsDirectoryUpdated(x.Children, y.Children),
                _ => true,// ディレクトリが作成された。あるいは、元はファイルだったのに、削除され、ディレクトリが作成されていた
            },
            EntryPair.IHasFileInfo x => oldSyncEntryContent switch
            {
                SyncEntryContent.File y => ((DateTime)x.FileInfo!) != y.LastWriteTimeUtc,
                _ => true,// ファイルが作成された。あるいは、元はディレクトリだったのに、削除され、ファイルが作成されていた
            },
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>
    /// entryPairsに記載されているディレクトリ、ファイルを削除する。<br/>
    /// ただし、ディレクトリに関しては、空になった場合にのみ削除する。<br/>
    /// </summary>
    void DeleteDirectory(bool isTargetLeft, string path, IEnumerable<EntryPair> entryPairs)
    {
        foreach (var entryPair in entryPairs)
            DeleteEntry(Path.Combine(path, entryPair.Name), entryPair);
        DeleteEmptyDirectory(isTargetLeft, path);
    }

    void DeleteEntry(string path, EntryPair entryPair)
    {
        switch (entryPair)
        {
            case EntryPair.NoneDir x:
                DeleteDirectory(false, path, x.Children);
                break;
            case EntryPair.DirNone x:
                DeleteDirectory(true, path, x.Children);
                break;
            case EntryPair.NoneFile:
                DeleteFile(false, path);
                break;
            case EntryPair.FileNone:
                DeleteFile(true, path);
                break;
            default:
                throw new UnreachableException();
        }
    }

    void CreateDirectory(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.CreateDirectory, isTargetLeft, path);
        CreateDirectory(GetPath(isTargetLeft, path));
    }

    protected abstract void CreateDirectory(string path);

    void DeleteEmptyDirectory(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.DeleteDirectory, isTargetLeft, path);
        DeleteEmptyDirectory(GetPath(isTargetLeft, path));
    }

    protected abstract void DeleteEmptyDirectory(string path);

    SyncEntryContent.File CopyFile(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.CopyFile, isTargetLeft, path);
        var (src, dest) = GetSrcDest(isTargetLeft, path);
        CopyFile(src, dest);
        return new SyncEntryContent.File(File.GetLastWriteTimeUtc(src));
    }

    protected abstract void CopyFile(string srcPath, string destPath);

    SyncEntryContent.File OverwriteFile(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.OverwriteFile, isTargetLeft, path);
        var (src, dest) = GetSrcDest(isTargetLeft, path);
        OverwriteFile(src, dest);
        return new SyncEntryContent.File(File.GetLastWriteTimeUtc(src));
    }

    protected abstract void OverwriteFile(string srcPath, string destPath);

    void DeleteFile(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.DeleteFile, isTargetLeft, path);
        DeleteFile(GetPath(isTargetLeft, path));
    }

    protected abstract void DeleteFile(string path);

    string GetPath(bool isTargetLeft, string path) => isTargetLeft ? Path.Combine(_leftBasePath, path) : Path.Combine(_rightBasePath, path);

    (string src, string dest) GetSrcDest(bool isTargetLeft, string path)
    {
        var (s, d) = isTargetLeft ? (_rightBasePath, _leftBasePath) : (_leftBasePath, _rightBasePath);
        return (Path.Combine(s, path), Path.Combine(d, path));
    }
    #endregion 本来の抽象クラス定義

    #region 派生クラス群
    public sealed class Synchronizer(string leftBasePath, string rightBasePath) : DirectorySynchronizer(leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) => Directory.CreateDirectory(path);
        protected override void DeleteEmptyDirectory(string path)
        {
            if (!Win32.RemoveDirectory(path))
            {
                // ディレクトリ削除失敗の理由が、ディレクトリが空でないためであれば、正常扱いとする(無視ディレクトリがあれば、ディレクトリが空にならないため)
                int error = Marshal.GetLastWin32Error();
                if (error != 145) // ERROR_DIR_NOT_EMPTY
                    throw new Win32Exception(error);
            }
        }
        protected override void CopyFile(string srcPath, string destPath) => File.Copy(srcPath, destPath, false);
        protected override void OverwriteFile(string srcPath, string destPath) => FileUtils.ReplaceFile(srcPath, destPath);
        protected override void DeleteFile(string path) => RecycleBin.MoveToRecycleBin(path);
    }

    public sealed class Checker(string leftBasePath, string rightBasePath) : DirectorySynchronizer(leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) { }
        protected override void DeleteEmptyDirectory(string path) { }
        protected override void CopyFile(string srcPath, string destPath) { }
        protected override void OverwriteFile(string srcPath, string destPath) { }
        protected override void DeleteFile(string path) { }
    }
    #endregion 派生クラス群
}
