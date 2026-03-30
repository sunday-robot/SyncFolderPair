using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectorySynchronizer
{
    /// <summary>
    /// 二つのディレクトリの更新内容を互いに反映する。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    /// <returns>今回の更新結果</returns>
    public static SyncEntries Synchronize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        var synchronizer = new DirectorySynchronizerX(leftDirectoryPath, rightDirectoryPath);
        return synchronizer.Synchronize(ignoreEntries, oldSyncEntries);
    }

    /// <summary>
    /// Synchronizeのプレビューをする。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    public static void CheckSynchronize(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        var synchronizer = new DirectorySynchronizeChecker(leftDirectoryPath, rightDirectoryPath);
        synchronizer.Synchronize(ignoreEntries, oldSyncEntries);
    }
}

public abstract class AbstractDirectorySynchronizer(string leftBasePath, string rightBasePath)
{
    static readonly SyncEntries _emptySyncEntries = new();

    readonly string _leftBasePath = leftBasePath;
    readonly string _rightBasePath = rightBasePath;

    protected abstract void CreateDirectory(string path);
    protected abstract void DeleteEmptyDirectory(string path);
    protected abstract void CopyFile(string srcPath, string destPath);
    protected abstract void ReplaceFile(string srcPath, string destPath);
    protected abstract void DeleteFile(string path);

    public SyncEntries Synchronize(IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        var entryPairs = EntryPairs.Enumerate(_leftBasePath, _rightBasePath, path => File.GetLastWriteTimeUtc(path), ignoreEntries);
        return Synchronize("", entryPairs, oldSyncEntries);
    }

    SyncEntries Synchronize(string path, IEnumerable<EntryPair> entryPairs, SyncEntries oldSyncEntries)
    {
        var newSyncEntries = new SyncEntries();
        foreach (var entryPair in entryPairs)
        {
            var newSyncEntry = SynchronizeEntryPair(Path.Combine(path, entryPair.Name), entryPair, oldSyncEntries.Get(entryPair.Name));
            if (newSyncEntry != null)
                newSyncEntries.Add(entryPair.Name, newSyncEntry);
        }
        return newSyncEntries;
    }

    SyncEntriesNode? SynchronizeEntryPair(string path, EntryPair entryPair, SyncEntriesNode? oldSyncEntryNode)
    {
        return entryPair switch
        {
            EntryPair.DirDir x => SynchronizeDirectory(path, x, oldSyncEntryNode),
            EntryPair.FileFile x => SynchronizeFile(path, x, oldSyncEntryNode),
            EntryPair.DirFile x => SynchronizeDifferentType(true, path, x, oldSyncEntryNode),
            EntryPair.FileDir x => SynchronizeDifferentType(false, path, x, oldSyncEntryNode),
            EntryPair.DirNone x => SynchronizeOrphanDirectory(true, path, x, oldSyncEntryNode),
            EntryPair.NoneDir x => SynchronizeOrphanDirectory(false, path, x, oldSyncEntryNode),
            EntryPair.FileNone x => SynchronizeOrphanFile(true, path, x, oldSyncEntryNode),
            EntryPair.NoneFile x => SynchronizeOrphanFile(false, path, x, oldSyncEntryNode),
            _ => null,// ここに到達することはない
        };
    }

    SyncEntries? SynchronizeDirectory(string path, EntryPair.DirDir dirDir, SyncEntriesNode? oldSyncEntryNode)
    {
        switch (oldSyncEntryNode)
        {
            case SyncEntries y:
                return Synchronize(path, dirDir.Children, y);
            default:    // null or SyncEntriesLeaf
                return Synchronize(path, dirDir.Children, _emptySyncEntries);
        }
    }

    SyncEntriesNode? SynchronizeFile(string path, EntryPair.FileFile fileFile, SyncEntriesNode? oldSyncEntryNode)
    {
        var lt = (DateTime)fileFile.Left!;
        var rt = (DateTime)fileFile.Right!;

        switch (oldSyncEntryNode)
        {
            case SyncEntriesLeaf y:
                switch (DateTime.Compare(lt, rt))
                {
                    case > 0:
                        if (rt != y.LastWriteTimeUtc)
                        {
                            // 運用ミス。左右で別々に更新された
                            Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryNode;
                        }
                        // 左のファイルが更新された
                        return ReplaceFile(true, path);
                    case < 0:
                        if (lt != y.LastWriteTimeUtc)
                        {
                            // 運用ミス。左右で別々に更新された
                            Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryNode;
                        }
                        // 右のファイルが更新された
                        return ReplaceFile(false, path);
                    case 0:
                        if (lt != y.LastWriteTimeUtc)
                        {
                            // 特殊運用。ファイルが更新され、手動同期済み
                            return new SyncEntriesLeaf(lt);
                        }
                        // ファイルは更新されていない
                        return oldSyncEntryNode;
                }
            default:    // null or SyncEntries
                if (lt != rt)
                {
                    // 運用ミス。
                    // 左右でファイルが新規作成されたが、更新日時が異なる
                    // あるいは、左右でディレクトリが削除され、さらにファイルが新規作成されたが、更新日時が異なる
                    Console.WriteLine($"[Operation Error] File was created on both sides, but they are different: {path}");
                    return oldSyncEntryNode;
                }
                // ファイルが作成され、手動同期済み
                // あるいは、左右ディレクトリが削除され、ファイルが作成され、手動同期済み
                return new SyncEntriesLeaf(lt);
        }
    }

    SyncEntriesNode? SynchronizeDifferentType(bool isLeftDirectory, string path, EntryPair entryPair, SyncEntriesNode? oldSyncEntryNode)
    {
        var children = ((EntryPair.IHasChildren)entryPair).Children;
        var t = (DateTime)((EntryPair.IHasFileInfo)entryPair).FileInfo!;
        switch (oldSyncEntryNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(children, y))
                {
                    // 運用ミス。片方のディレクトリが削除され、ファイルが作成されたのに、もう片方のディレクトリが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + $" while the directory was updated on the other. {path}");    // TODO
                    return oldSyncEntryNode;
                }
                // 片方のディレクトリが削除され、ファイルが作成された
                // もう片方のディレクトリを削除し、ファイルをコピーする
                DeleteDirectory(isLeftDirectory, path, children);
                return CopyFile(!isLeftDirectory, path);

            case SyncEntriesLeaf y:
                if (t != y.LastWriteTimeUtc)
                {
                    // 運用ミス。片方のファイルが削除され、ディレクトリが作成されたのに、もう片方のファイルが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + $" while the file was updated on the other. {path}"); // TODO
                    return oldSyncEntryNode;
                }
                // 片方のファイルが削除され、ディレクトリが作成された
                // もう片方のファイルを削除し、ディレクトリをコピーする
                DeleteFile(!isLeftDirectory, path);
                CreateDirectory(!isLeftDirectory, path);
                return Synchronize(path, children, _emptySyncEntries);
            default:    // null
                // 運用ミス。左右で異なる種類のものが新規作成された
                Console.WriteLine($"[Operation Error] A directory was created on one side and a file on the other. {path}");
                return null;
        }
    }

    SyncEntries? SynchronizeOrphanDirectory(bool leftDirectoryExists, string path, EntryPair.IHasChildren x, SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(x.Children, y))
                {
                    // 運用ミス。片方のディレクトリが削除されたのに、もう片方のディレクトリが更新された
                    Console.WriteLine($"[Operation Mistake] Left directory was deleted, but Right directory was updated. {path}");  // TODO
                    return y;
                }
                // 片方のディレクトリが削除された
                // 残ったディレクトリを削除する
                DeleteDirectory(leftDirectoryExists, path, x.Children);
                return null;
            default:    // null or SyncEntriesLeaf
                // 片方にディレクトリが新規作成された
                // あるいは、左右でファイルが削除され、片方にディレクトリが新規作成された
                // もう片方にディレクトリを作成し、ディレクトリの内容をコピーする
                CreateDirectory(!leftDirectoryExists, path);
                return Synchronize(path, x.Children, _emptySyncEntries);
        }
    }

    SyncEntriesLeaf? SynchronizeOrphanFile(bool leftFileExists, string path, EntryPair.IHasFileInfo x, SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntriesLeaf y:
                if ((DateTime)x.FileInfo! != y.LastWriteTimeUtc)
                {
                    // 運用ミス。片方でファイルが削除されたが、もう片方のファイルは更新されている
                    Console.WriteLine($"[Operation Error] Left? file was deleted, but right? file was updated. {path}");    // TODO
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
                return CopyFile(leftFileExists, path);
        }
    }

    /// <summary>
    /// ディレクトリが更新(ディレクトリ内に新規にディレクトリ、ファイルが作成された、ディレクトリ内のファイルが更新された)かどうかを返す。
    /// ただし、ファイル、ディレクトリが削除されたとしても、それは無視する。
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
        return false;   // ディレクトリは更新されていない
    }

    static bool IsEntryUpdated(EntryPair entryPair, SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (entryPair)
        {
            case EntryPair.IHasChildren x:
                switch (oldSyncEntriesNode)
                {
                    case SyncEntries y:
                        return IsDirectoryUpdated(x.Children, y);
                    default:    // null or SyncEntriesLeaf
                        // ディレクトリが作成された
                        // あるいは、元はファイルだったのに、削除され、ディレクトリが作成されていた
                        return true;
                }
            case EntryPair.IHasFileInfo x:
                switch (oldSyncEntriesNode)
                {
                    case SyncEntriesLeaf y:
                        return ((DateTime)x.FileInfo!) != y.LastWriteTimeUtc;
                    default:    // null or SyncEntries
                        // ファイルが作成された
                        // あるいは、元はディレクトリだったのに、削除され、ファイルが作成されていた
                        return true;
                }
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    /// ディレクトリを削除する<br/>
    /// 上記説明は不正確。正確にはディレクトリ内の各ファイルをゴミ箱に移動させ、ディレクトリを削除する。<br/>
    /// ただし、無視ディレクトリ内のファイルについては削除しない。また、このようなファイルを含むディレクトリも削除はしない。<br/>
    /// </summary>
    void DeleteDirectory(bool isLeft, string path, IEnumerable<EntryPair> entryPairs)
    {
        foreach (var entryPair in entryPairs)
            DeleteEntry(Path.Combine(path, entryPair.Name), entryPair);
        DeleteEmptyDirectory(isLeft, path);
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

    void CreateDirectory(bool isLeft, string path)
    {
        PrintMessage("CREATE", !isLeft, path);
        CreateDirectory(GetPath(isLeft, path));
    }

    void DeleteEmptyDirectory(bool isLeft, string path)
    {
        PrintMessage("DELETE", !isLeft, path);
        DeleteEmptyDirectory(GetPath(isLeft, path));
    }

    SyncEntriesLeaf CopyFile(bool leftToRight, string path)
    {
        PrintMessage("COPY", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        CopyFile(src, dest);
        return CreateSyncEntriesLeaf(src);
    }

    SyncEntriesLeaf ReplaceFile(bool leftToRight, string path)
    {
        PrintMessage("REPLACE", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        ReplaceFile(src, dest);
        return CreateSyncEntriesLeaf(src);
    }

    void DeleteFile(bool isLeft, string path)
    {
        PrintMessage("DELETE", !isLeft, path);
        DeleteFile(GetPath(isLeft, path));
    }

    static void PrintMessage(string operation, bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[{operation,10}>] {path}");
        else
            Console.WriteLine($"[<{operation,-10}] {path}");
    }

    protected static SyncEntriesLeaf CreateSyncEntriesLeaf(string filePath)
    {
        var lastModifiedUtc = File.GetLastWriteTimeUtc(filePath);
        return new SyncEntriesLeaf(lastModifiedUtc);
    }

    string GetPath(bool isLeft, string path) => isLeft ? Path.Combine(_leftBasePath, path) : Path.Combine(_rightBasePath, path);

    (string src, string dest) GetSrcDest(bool leftToRight, string path)
    {
        var (s, d) = leftToRight ? (_leftBasePath, _rightBasePath) : (_rightBasePath, _leftBasePath);
        return (Path.Combine(s, path), Path.Combine(d, path));
    }
}

public sealed class DirectorySynchronizerX(string leftBasePath, string rightBasePath) : AbstractDirectorySynchronizer(leftBasePath, rightBasePath)
{
    protected override void CreateDirectory(string path) => Directory.CreateDirectory(path);

    protected override void DeleteEmptyDirectory(string path)
    {
        if (!Win32.RemoveDirectory(path))
        {
            // ディレクトリ削除失敗の理由が、ディレクトリが空でないためであれば、正常扱いとする(無視ディレクトリがあれば、ディレクトリが空にならないため)
            int error = Marshal.GetLastWin32Error();
            if (error != 145) // ERROR_DIR_NOT_EMPTY
            {
                throw new Win32Exception(error);
            }
        }
    }

    protected override void CopyFile(string srcPath, string destPath) => File.Copy(srcPath, destPath, false);

    protected override void ReplaceFile(string srcPath, string destPath) => FileUtils.ReplaceFile(srcPath, destPath);

    protected override void DeleteFile(string path) => RecycleBin.MoveToRecycleBin(path);
}

public sealed class DirectorySynchronizeChecker(string leftBasePath, string rightBasePath) : AbstractDirectorySynchronizer(leftBasePath, rightBasePath)
{
    protected override void CreateDirectory(string path) { }
    protected override void DeleteEmptyDirectory(string path) { }
    protected override void CopyFile(string srcPath, string destPath) { }
    protected override void ReplaceFile(string srcPath, string destPath) { }
    protected override void DeleteFile(string path) { }
}
