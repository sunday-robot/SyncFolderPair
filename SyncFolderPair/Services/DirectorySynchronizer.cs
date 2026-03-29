using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectorySynchronizer
{
    static readonly SyncEntriesLeaf _dummySyncEntriesLeaf = new(DateTime.MinValue);

    static readonly SyncEntries _emptySyncEntries = new();

    /// <summary>
    /// 二つのディレクトリの更新内容を互いに反映する。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    /// <returns>今回の更新結果</returns>
    public static SyncEntries Synchronize(
        string leftDirectoryPath,
        string rightDirectoryPath,
        IgnoreEntries ignoreEntries,
        SyncEntries oldSyncEntries)
    {
        return SynchronizeDirectoryPair(
            CreateDirectory, DeleteEmptyDirectory, CopyFile, ReplaceFile, DeleteFile,
            leftDirectoryPath, rightDirectoryPath, ignoreEntries, oldSyncEntries);
    }

    /// <summary>
    /// Synchronizeのプレビューをする。
    /// </summary>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <param name="ignoreEntries">無視するエントリー</param>
    /// <param name="oldSyncEntries">前回の更新結果</param>
    public static void CheckSynchronize(
        string leftDirectoryPath,
        string rightDirectoryPath,
        IgnoreEntries ignoreEntries,
        SyncEntries oldSyncEntries)
    {
        SynchronizeDirectoryPair(
            PrintCreateDirectory, PrintDeleteEmptyDirectory, PrintCopyFile, PrintReplaceFile, PrintDeleteFile,
            leftDirectoryPath, rightDirectoryPath, ignoreEntries, oldSyncEntries);
    }

    static SyncEntries SynchronizeDirectoryPair(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        IgnoreEntries ignoreEntries,
        SyncEntries oldSyncEntries)
    {
        var entryPairs = EntryPairs.Enumerate(leftBase, rightBase, path => File.GetLastWriteTimeUtc(path), ignoreEntries);
        return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
            leftBase, rightBase, "", entryPairs, oldSyncEntries);
    }

    static SyncEntries Synchronize(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        IEnumerable<EntryPair> entryPairs,
        SyncEntries oldSyncEntries)
    {
        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);
        var newSyncEntries = new SyncEntries();
        foreach (var e in entryPairs)
        {
            var nsen = SynchronizeEntryPair(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                leftBase, rightBase, Path.Combine(path, e.Name), e, oldSyncEntries.Get(e.Name));
            if (nsen != null)
                newSyncEntries.Add(e.Name, nsen);
        }
        return newSyncEntries;
    }

    static SyncEntriesNode? SynchronizeEntryPair(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair entryPair,
        SyncEntriesNode? oldSyncEntryNode)
    {
        return entryPair switch
        {
            EntryPair.DirDir x => SynchronizeDirDir(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.FileFile x => SynchronizeFileFile(replaceFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.DirFile x => SynchronizeDirFile(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.FileDir x => SynchronizeFileDir(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.NoneDir x => SynchronizeNoneDir(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.DirNone x => SynchronizeDirNone(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.FileNone x => SynchronizeFileNone(copyFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            EntryPair.NoneFile x => SynchronizeNoneFile(copyFile, deleteFile,
                                leftBase, rightBase, path, x, oldSyncEntryNode),
            _ => null,// ここに到達することはない
        };
    }

    static SyncEntries? SynchronizeDirDir(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.DirDir entryPair,
        SyncEntriesNode? oldSyncEntryNode)
    {
        var oldSyncEntries = (oldSyncEntryNode is SyncEntries entries) ? entries : _emptySyncEntries;
        return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
            leftBase, rightBase, path, entryPair.ChildrenEnumerable, oldSyncEntries);
    }

    static SyncEntriesNode? SynchronizeFileFile(
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.FileFile x,
        SyncEntriesNode? oldSyncEntryNode)
    {
        var lt = (DateTime)x.Left!;
        var rt = (DateTime)x.Right!;
        if (lt != rt)
        {
            if (oldSyncEntryNode is not SyncEntriesLeaf y)
            {
                // null: 運用ミス。左右でファイルが新規作成されたが、更新日時が異なる
                // SyncEntries: 運用ミス。左右でディレクトリが削除され、さらにファイルが新規作成されたが、更新日時が異なる
                Console.WriteLine($"[Operation Error] File was created on both sides, but they are different: {path}");
                return oldSyncEntryNode;
            }
            if (lt > rt)
            {
                if (rt != y.LastModifiedUtc)
                {
                    // 運用ミス。左右で別々に更新された
                    Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                    return oldSyncEntryNode;
                }
                // 左のファイルが更新された
                return replaceFile(true, leftBase, rightBase, path);
            }
            else
            {
                if (lt != y.LastModifiedUtc)
                {
                    // 運用ミス。左右で別々に更新された
                    Console.WriteLine($"[Operation Error] File was updated on both side: {path}");
                    return oldSyncEntryNode;
                }
                // 右のファイルが更新された
                return replaceFile(false, rightBase, leftBase, path);
            }
        }
        else
        {
            switch (oldSyncEntryNode)
            {
                case SyncEntriesLeaf y:
                    if (lt != y.LastModifiedUtc)
                        return new SyncEntriesLeaf(lt); // 特殊運用。ファイルが更新され、手動同期済み
                    else
                        return oldSyncEntryNode;    // ファイルは更新されていない
                default:
                    // null: 特殊運用。ファイルが作成され、手動同期済み
                    // SyncEntries: 特殊運用。左右ディレクトリが削除され、ファイルが作成され、手動同期済み
                    return new SyncEntriesLeaf(lt);
            }
        }
    }

    static SyncEntriesNode? SynchronizeDirFile(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.DirFile x,
        SyncEntriesNode? oldSyncEntryNode)
    {
        switch (oldSyncEntryNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                {
                    // 運用ミス。右のディレクトリが削除され、ファイルが作成されたのに、左のディレクトリが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + $" while the directory was updated on the other. {path}");
                    return oldSyncEntryNode;
                }

                // 右のディレクトリが削除され、ファイルが作成された
                // 左のディレクトリを削除し、右のファイルを左にコピーする
                DeleteDirectory(deleteFile, deleteEmptyDirectory,
                    true, leftBase, path, x.ChildrenEnumerable);
                return copyFile(false, rightBase, leftBase, path);
            case SyncEntriesLeaf y:
                var rt = (DateTime)x.FileInfo!;
                if (rt != y.LastModifiedUtc)
                {
                    // 運用ミス。左のファイルは削除され、ディレクトリが作成されているのに、右のファイルは更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + $" while the file was updated on the other. {Path.Combine(rightBase, path)}");
                    return oldSyncEntryNode;
                }
                // 左のファイルが削除され、ディレクトリが作成された
                // 右のファイルを削除し、右にディレクトリを作成し、左のディレクトリの内容を右にコピーする
                deleteFile(false, rightBase, path);
                createDirectory(false, rightBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
            default:
                // null: 運用ミス。左右で異なる種類のものが新規作成された
                Console.WriteLine($"[Operation Error] A directory was created on one side and a file on the other. {path}");
                return null;
        }
    }

    static SyncEntriesNode? SynchronizeFileDir(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.FileDir x,
        SyncEntriesNode? oldSyncEntryNode)
    {
        switch (oldSyncEntryNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                {
                    // 運用ミス。左のディレクトリが削除され、ファイルが作成されたのに、右のディレクトリが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + $" while the directory was updated on the other. {path}");
                    return oldSyncEntryNode;
                }
                // 左のディレクトリが削除され、ファイルが作成された
                // 右のディレクトリを削除し、左のファイルを右にコピーする
                DeleteDirectory(deleteFile, deleteEmptyDirectory,
                    true, leftBase, path, x.ChildrenEnumerable);
                return copyFile(true, leftBase, rightBase, path);

            case SyncEntriesLeaf y:
                var rt = (DateTime)x.FileInfo!;
                if (rt != y.LastModifiedUtc)
                {
                    // 運用ミス。右のファイルは削除され、ディレクトリが作成されているのに、左のファイルは更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + $" while the file was updated on the other. {Path.Combine(rightBase, path)}");
                    return oldSyncEntryNode;
                }
                // 右のファイルが削除され、ディレクトリが作成された
                // 左のファイルを削除し、左にディレクトリを作成し、右のディレクトリの内容を左にコピーする
                deleteFile(true, leftBase, path);
                createDirectory(true, leftBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
            default:
                // null: 運用ミス。左右で異なる種類のものが新規作成された
                Console.WriteLine($"[Operation Error] A directory was created on one side and a file on the other. {path}");
                return null;
        }
    }

    static SyncEntries? SynchronizeNoneDir(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.NoneDir x,
        SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                {
                    // 運用ミス。左のディレクトリが削除されたのに、右のディレクトリが更新された
                    Console.WriteLine($"[Operation Mistake] Left directory was deleted, but Right directory was updated. {path}");
                    return y;
                }

                // 左のディレクトリが削除された
                // 右のディレクトリを削除する
                DeleteDirectory(deleteFile, deleteEmptyDirectory,
                    false, rightBase, path, x.ChildrenEnumerable);
                return null;
            case SyncEntriesLeaf y:
                // 左右でファイルが削除され、右にディレクトリが新規作成された
                // 左にディレクトリを作成し、右のディレクトリの内容を左にコピーする
                createDirectory(true, leftBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
            default:
                // null: 右にディレクトリが新規作成された
                // 右のディレクトリを左にコピーする
                createDirectory(true, leftBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
        }
    }

    static SyncEntries? SynchronizeDirNone(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.DirNone x,
        SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntries y:
                if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                {
                    // 運用ミス。右のディレクトリが削除されたのに、左のディレクトリが更新された
                    Console.WriteLine($"[Operation Mistake] Right directory was deleted, but Left directory was updated. {path}");
                    return y;
                }
                // 右のディレクトリが削除された
                // 左のディレクトリを削除する
                DeleteDirectory(deleteFile, deleteEmptyDirectory,
                    true, leftBase, path, x.ChildrenEnumerable);
                return null;
            case SyncEntriesLeaf y:
                // 左右でファイルが削除され、左にディレクトリが新規作成された
                // 右にディレクトリを作成し、左のディレクトリの内容を右にコピーする
                createDirectory(false, rightBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
            default:
                // null: 左にディレクトリが新規作成された
                // 左のディレクトリを右にコピーする
                createDirectory(false, rightBase, path);
                return Synchronize(createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                    leftBase, rightBase, path, x.ChildrenEnumerable, _emptySyncEntries);
        }
    }

    static SyncEntriesNode? SynchronizeFileNone(
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.FileNone x,
        SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntriesLeaf y:
                if ((DateTime)x.FileInfo! != y.LastModifiedUtc)
                {
                    // 運用ミス。右のファイルが削除されたが、左のファイルは更新されている
                    Console.WriteLine($"[Operation Error] Right file was deleted, but left file was updated. {path}");
                    return y;
                }
                // 右のファイルが削除された
                // 左のファイルを削除する
                deleteFile(true, leftBase, path);
                return null;
            default:
                // null: 左にファイルが新規作成された
                // SyncEntries: 左右のディレクトリが削除され、左にファイルが作成された
                // 左のファイルを右にコピーする。
                return copyFile(true, leftBase, rightBase, path);
        }
    }

    static SyncEntriesNode? SynchronizeNoneFile(
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        EntryPair.NoneFile x,
        SyncEntriesNode? oldSyncEntriesNode)
    {
        switch (oldSyncEntriesNode)
        {
            case SyncEntriesLeaf y:
                if ((DateTime)x.FileInfo! != y.LastModifiedUtc)
                {
                    // 運用ミス。左のファイルが削除されたが、右のファイルは更新されている
                    Console.WriteLine($"[Operation Error] Left file was deleted, but right file was updated. {path}");
                    return y;
                }
                // 左のファイルが削除された
                // 右のファイルを削除する
                deleteFile(false, rightBase, path);
                return null;
            default:
                // null: 右にファイルが新規作成された
                // SyncEntries: 左右のディレクトリが削除され、右にファイルが作成された
                // 右のファイルを左にコピーする。
                return copyFile(false, rightBase, leftBase, path);
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
    private static bool IsDirectoryUpdated(IEnumerable<EntryPair> entryPairs, SyncEntries oldSyncEntries)
    {
        foreach (var e in entryPairs)
        {
            var node = oldSyncEntries.Get(e.Name);
            if (node == null)
                return true;    // ディレクトリあるいはファイルが作成された

            switch (e)
            {
                case EntryPair.NoneDir x:
                    {
                        if (node is not SyncEntries y)
                            return true;    // 元はファイルだったのに、削除され、ディレクトリが作成されていた
                        if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                            return true;
                    }
                    break;
                case EntryPair.DirNone x:
                    {
                        if (node is not SyncEntries y)
                            return true;    // 元はファイルだったのに、削除され、ディレクトリが作成されていた
                        if (IsDirectoryUpdated(x.ChildrenEnumerable, y))
                            return true;
                    }
                    break;
                case EntryPair.NoneFile x:
                    {
                        if (node is not SyncEntriesLeaf y)
                            return true;    // ディレクトリが削除されファイルが作成された
                        if (((DateTime)x.FileInfo!) != y.LastModifiedUtc)
                            return true;    // ファイルが更新された
                    }
                    break;
                case EntryPair.FileNone x:
                    {
                        if (node is not SyncEntriesLeaf y)
                            return true;    // ディレクトリが削除されファイルが作成された
                        if (((DateTime)x.FileInfo!) != y.LastModifiedUtc)
                            return true;    // ファイルが更新された
                    }
                    break;
            }
        }
        return false;   // ディレクトリは更新されていない
    }

    /// <summary>
    /// ディレクトリをコピーする<br/>
    /// </summary>
    /// <returns>コピーしたディレクトリの情報</returns>
    static SyncEntries CopyDirectory(
        Action<bool, string, string> createDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        bool leftToRight, string srcBase, string destBase, string path, IEnumerable<EntryPair> entryPairs)
    {
        var newEntries = new SyncEntries();
        var src = Path.Combine(srcBase, path);
        createDirectory(!leftToRight, destBase, path);
        foreach (var e in entryPairs)
        {
            var p = Path.Combine(path, e.Name);
            switch (e)
            {
                case EntryPair.NoneDir x:
                    newEntries.Add(e.Name,
                        CopyDirectory(createDirectory, copyFile,
                            false, srcBase, destBase, p, x.ChildrenEnumerable));
                    break;
                case EntryPair.DirNone x:
                    newEntries.Add(e.Name,
                        CopyDirectory(createDirectory, copyFile,
                      true, srcBase, destBase, p, x.ChildrenEnumerable));
                    break;

                case EntryPair.NoneFile:
                    newEntries.Add(e.Name, copyFile(false, srcBase, destBase, p));
                    break;
                case EntryPair.FileNone:
                    newEntries.Add(e.Name, copyFile(true, srcBase, destBase, p));
                    break;
            }
        }
        return newEntries;
    }

    /// <summary>
    /// ディレクトリを削除する<br/>
    /// 上記説明は不正確。正確にはディレクトリ内の各ファイルをゴミ箱に移動させ、ディレクトリを削除する。<br/>
    /// ただし、無視ディレクトリ内のファイルについては削除しない。また、このようなファイルを含むディレクトリも削除はしない。<br/>
    /// </summary>
    private static void DeleteDirectory(
        Action<bool, string, string> deleteFile,
        Action<bool, string, string> deleteEmptyDirectory,
        bool isLeft,
        string basePath,
        string path,
        IEnumerable<EntryPair> entryPairs)
    {
        foreach (var e in entryPairs)
        {
            switch (e)
            {
                case EntryPair.NoneDir x:
                    DeleteDirectory(deleteFile, deleteEmptyDirectory,
                        false, basePath, Path.Combine(path, e.Name), x.ChildrenEnumerable);
                    break;
                case EntryPair.DirNone x:
                    DeleteDirectory(deleteFile, deleteEmptyDirectory,
                        true, basePath, Path.Combine(path, e.Name), x.ChildrenEnumerable);
                    break;
                case EntryPair.NoneFile:
                    deleteFile(false, basePath, Path.Combine(path, e.Name));
                    break;
                case EntryPair.FileNone:
                    deleteFile(true, basePath, Path.Combine(path, e.Name));
                    break;
            }
        }
        deleteEmptyDirectory(isLeft, basePath, path);
    }

    static void PrintCreateDirectory(bool isLeft, string _, string path) => PrintMessage("CREATE", !isLeft, path);

    static void CreateDirectory(bool isLeft, string basePath, string path)
    {
        PrintCreateDirectory(isLeft, basePath, path);
        Directory.CreateDirectory(Path.Combine(basePath, path));
    }

    static void PrintDeleteEmptyDirectory(bool isLeft, string _, string path) => PrintMessage("DELETE", !isLeft, path);

    static void DeleteEmptyDirectory(bool isLeft, string basePath, string path)
    {
        PrintDeleteEmptyDirectory(isLeft, "", path);
        if (!Win32.RemoveDirectory(Path.Combine(basePath, path)))
        {
            // ディレクトリ削除失敗の理由が、ディレクトリが空でないためであれば、正常扱いとする(無視ディレクトリがあれば、ディレクトリが空にならないため)
            int error = Marshal.GetLastWin32Error();
            if (error != 145) // ERROR_DIR_NOT_EMPTY
            {
                throw new Win32Exception(error);
            }
        }
    }

    static SyncEntriesLeaf PrintCopyFile(bool leftToRight, string _, string __, string path)
    {
        PrintMessage("COPY", leftToRight, path);
        return _dummySyncEntriesLeaf;
    }

    static SyncEntriesLeaf CopyFile(bool leftToRight, string srcBase, string destBase, string path)
    {
        PrintCopyFile(leftToRight, "", "", path);
        var src = Path.Combine(srcBase, path);
        File.Copy(src, Path.Combine(destBase, path), false);
        return CreateSyncEntriesLeaf(src);
    }

    static SyncEntriesLeaf PrintReplaceFile(bool leftToRight, string _, string __, string path)
    {
        PrintMessage("REPLACE", leftToRight, path);
        return _dummySyncEntriesLeaf;
    }

    private static SyncEntriesLeaf ReplaceFile(bool leftToRight, string srcBase, string destBase, string path)
    {
        PrintReplaceFile(leftToRight, "", "", path);
        var src = Path.Combine(srcBase, path);
        FileUtils.ReplaceFile(src, Path.Combine(destBase, path));
        return CreateSyncEntriesLeaf(src);
    }

    static void PrintDeleteFile(bool isLeft, string _, string path) => PrintMessage("DELETE", !isLeft, path);

    static void DeleteFile(bool isLeft, string basePath, string path)
    {
        PrintDeleteFile(isLeft, basePath, path);
        RecycleBin.MoveToRecycleBin(Path.Combine(basePath, path));
    }

    static void PrintMessage(string operation, bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[{operation,10}>] {path}");
        else
            Console.WriteLine($"[<{operation,-10}] {path}");
    }

    static SyncEntriesLeaf CreateSyncEntriesLeaf(string filePath)
    {
        var lastModifiedUtc = File.GetLastWriteTimeUtc(filePath);
        return new SyncEntriesLeaf(lastModifiedUtc);
    }
}
