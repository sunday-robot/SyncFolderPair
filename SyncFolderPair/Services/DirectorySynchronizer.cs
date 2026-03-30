using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public abstract class DirectorySynchronizer(string leftBasePath, string rightBasePath)
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
        var synchronizer = new Synchronizer(leftDirectoryPath, rightDirectoryPath);
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
        var checker = new Checker(leftDirectoryPath, rightDirectoryPath);
        checker.Synchronize(ignoreEntries, oldSyncEntries);
    }

    static readonly SyncEntries _emptySyncEntries = [];

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
            EntryPair.DirFile x => SynchronizeDifferentType(true, path, x, oldSyncEntryContent),
            EntryPair.FileDir x => SynchronizeDifferentType(false, path, x, oldSyncEntryContent),
            EntryPair.DirNone x => SynchronizeOrphanDirectory(true, path, x, oldSyncEntryContent),
            EntryPair.NoneDir x => SynchronizeOrphanDirectory(false, path, x, oldSyncEntryContent),
            EntryPair.FileNone x => SynchronizeOrphanFile(true, path, x, oldSyncEntryContent),
            EntryPair.NoneFile x => SynchronizeOrphanFile(false, path, x, oldSyncEntryContent),
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
                            Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryContent;
                        }
                        // 左のファイルが更新された
                        return ReplaceFile(true, path);
                    case < 0:
                        if (lt != y.LastWriteTimeUtc)
                        {
                            // 運用ミス。左右で別々に更新された
                            Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                            return oldSyncEntryContent;
                        }
                        // 右のファイルが更新された
                        return ReplaceFile(false, path);
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
                    Console.WriteLine($"[Operation Error] File was created on both sides, but they are different: {path}");
                    return oldSyncEntryContent;
                }
                // ファイルが作成され、手動同期済み
                // あるいは、左右ディレクトリが削除され、ファイルが作成され、手動同期済み
                return new SyncEntryContent.File(lt);
        }
    }

    SyncEntryContent? SynchronizeDifferentType(bool isLeftDirectory, string path, EntryPair entryPair, SyncEntryContent? oldSyncEntryContent)
    {
        var children = ((EntryPair.IHasChildren)entryPair).Children;
        var t = (DateTime)((EntryPair.IHasFileInfo)entryPair).FileInfo!;
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.Directory y:
                if (IsDirectoryUpdated(children, y.Children))
                {
                    // 運用ミス。片方のディレクトリが削除され、ファイルが作成されたのに、もう片方のディレクトリが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + $" while the directory was updated on the other. {path}");    // TODO
                    return oldSyncEntryContent;
                }
                // 片方のディレクトリが削除され、ファイルが作成された
                // もう片方のディレクトリを削除し、ファイルをコピーする
                DeleteDirectory(isLeftDirectory, path, children);
                return CopyFile(!isLeftDirectory, path);

            case SyncEntryContent.File y:
                if (t != y.LastWriteTimeUtc)
                {
                    // 運用ミス。片方のファイルが削除され、ディレクトリが作成されたのに、もう片方のファイルが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + $" while the file was updated on the other. {path}"); // TODO
                    return oldSyncEntryContent;
                }
                // 片方のファイルが削除され、ディレクトリが作成された
                // もう片方のファイルを削除し、ディレクトリをコピーする
                DeleteFile(!isLeftDirectory, path);
                CreateDirectory(!isLeftDirectory, path);
                return new SyncEntryContent.Directory(SynchronizeEntryPairs(path, children, _emptySyncEntries));
            default:    // null
                // 運用ミス。左右で異なる種類のものが新規作成された
                Console.WriteLine($"[Operation Error] A directory was created on one side and a file on the other. {path}");
                return null;
        }
    }

    SyncEntryContent.Directory? SynchronizeOrphanDirectory(bool leftDirectoryExists, string path, EntryPair.IHasChildren x, SyncEntryContent? oldSyncEntryContent)
    {
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.Directory y:
                if (IsDirectoryUpdated(x.Children, y.Children))
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
                return new SyncEntryContent.Directory(SynchronizeEntryPairs(path, x.Children, _emptySyncEntries));
        }
    }

    SyncEntryContent.File? SynchronizeOrphanFile(bool leftFileExists, string path, EntryPair.IHasFileInfo x, SyncEntryContent? oldSyncEntryContent)
    {
        switch (oldSyncEntryContent)
        {
            case SyncEntryContent.File y:
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

    SyncEntryContent.File CopyFile(bool leftToRight, string path)
    {
        PrintMessage("COPY", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        CopyFile(src, dest);
        return new SyncEntryContent.File(File.GetLastWriteTimeUtc(src));
    }

    SyncEntryContent.File ReplaceFile(bool leftToRight, string path)
    {
        PrintMessage("REPLACE", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        ReplaceFile(src, dest);
        return new SyncEntryContent.File(File.GetLastWriteTimeUtc(src));
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

    string GetPath(bool isLeft, string path) => isLeft ? Path.Combine(_leftBasePath, path) : Path.Combine(_rightBasePath, path);

    (string src, string dest) GetSrcDest(bool leftToRight, string path)
    {
        var (s, d) = leftToRight ? (_leftBasePath, _rightBasePath) : (_rightBasePath, _leftBasePath);
        return (Path.Combine(s, path), Path.Combine(d, path));
    }

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
                {
                    throw new Win32Exception(error);
                }
            }
        }

        protected override void CopyFile(string srcPath, string destPath) => File.Copy(srcPath, destPath, false);

        protected override void ReplaceFile(string srcPath, string destPath) => FileUtils.ReplaceFile(srcPath, destPath);

        protected override void DeleteFile(string path) => RecycleBin.MoveToRecycleBin(path);
    }

    public sealed class Checker(string leftBasePath, string rightBasePath) : DirectorySynchronizer(leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) { }
        protected override void DeleteEmptyDirectory(string path) { }
        protected override void CopyFile(string srcPath, string destPath) { }
        protected override void ReplaceFile(string srcPath, string destPath) { }
        protected override void DeleteFile(string path) { }
    }
}
