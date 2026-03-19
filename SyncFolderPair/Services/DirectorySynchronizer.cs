using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectorySynchronizer
{
    static readonly SyncEntriesLeaf _dummySyncEntriesLeaf = new(DateTime.MinValue);

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
            leftDirectoryPath, rightDirectoryPath, "", ignoreEntries, oldSyncEntries);
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
        var dummySyncEntries = new SyncEntries();
        SynchronizeDirectoryPair(
            PrintCreateDirectory, PrintDeleteEmptyDirectory, PrintCopyFile, PrintReplaceFile, PrintDeleteFile,
            leftDirectoryPath, rightDirectoryPath, "", ignoreEntries, oldSyncEntries);
    }

    static SyncEntries SynchronizeDirectoryPair(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        Action<bool, string, string> deleteFile,
        string leftBase,
        string rightBase,
        string path,
        IgnoreEntries ignoreEntries,
        SyncEntries oldSyncEntries)
    {
        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);
        var newSyncEntries = new SyncEntries();
        foreach (var e in EntryEnumerator.Enumerate(left, right, ignoreEntries))
        {
            var name = e.Item1;
            var oldEntry = oldSyncEntries.Get(name);

            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    // 左にしかない
                    SynchronizeOrphanEntry(
                        createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                        true, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                        newSyncEntries);
                    break;
                case PairEnumerator.Existance.Both:
                    // 左右両方にある
                    if (IsDirectory(left, name))
                    {
                        if (IsDirectory(right, name))
                        {
                            // 左右両方ともディレクトリである
                            SyncEntries oldSubSyncEntries = oldEntry switch
                            {
                                SyncEntries => (SyncEntries)oldEntry,// 左右でディレクトリが存在し続けている
                                _ => new SyncEntries(),// 左右でディレクトリが新規作成された。あるいは、左右両方からファイルが削除され、左右どちらにもディレクトリが新規作成された
                            };
                            newSyncEntries.Add(name, SynchronizeDirectoryPair(
                                createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name), oldSubSyncEntries));
                        }
                        else
                        {
                            // 左はディレクトリ、右はファイルである
                            SynchronizeDirectoryAndFile(
                                createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                                true, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                                newSyncEntries);
                        }
                    }
                    else
                    {
                        if (IsDirectory(right, name))
                        {
                            // 左はファイル、右はディレクトリである
                            SynchronizeDirectoryAndFile(
                                createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                                false, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                                newSyncEntries);
                        }
                        else
                        {
                            // 左右どちらもファイルである
                            SynchronizeFilePair(
                                replaceFile,
                                leftBase, rightBase, path, name, oldEntry,
                                newSyncEntries);
                        }
                    }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    // 右にしかない
                    SynchronizeOrphanEntry(
                        createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                        false, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                        newSyncEntries);
                    break;
            }
        }

        return newSyncEntries;
    }

    /// <summary>
    /// 片方にしかファイルあるいはディレクトリがない場合の処理<br/>
    /// SynchronizeDirectoryPairの下請け。
    /// </summary>
    static void SynchronizeOrphanEntry(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Action<bool, string, string> deleteFile,
        bool isLeftOrphan,
        string leftBase,
        string rightBase,
        string path,
        string name,
        IgnoreEntries ignoreEntries,
        SyncEntriesNode? oldEntry,
        SyncEntries newSyncEntries)
    {
        var (orphanBase, missingBase) = GetSrcDest(isLeftOrphan, leftBase, rightBase);
        var p = Path.Combine(path, name);
        var left = Path.Combine(leftBase, p);
        var right = Path.Combine(rightBase, p);
        var (orphan, missing) = GetSrcDest(isLeftOrphan, left, right);

        if (Directory.Exists(orphan))
        {
            var ie = ignoreEntries.GetSubEntries(name);
            switch (oldEntry)
            {
                case SyncEntries:
                    if (IsDirectoryUpdated(orphan, ie, (SyncEntries)oldEntry))
                    {
                        // 運用ミス。Bのディレクトリが削除されたのに、Aのディレクトリが更新された
                        Console.WriteLine($"[Operation Mistake] Directory {missing} was deleted, but directory {orphan} was updated.");
                        newSyncEntries.Add(name, oldEntry);
                        return;
                    }
                    // ディレクトリが削除された
                    DeleteDirectory(
                        deleteFile, deleteEmptyDirectory,
                        isLeftOrphan, orphanBase, Path.Combine(path, name), ie);
                    break;
                default: // null, SyncEntriesLeaf
                    // ディレクトリが新規作成された
                    // あるいは、特殊運用。両方からファイルが削除され、一方にディレクトリが新規作成された
                    newSyncEntries.Add(name, CopyDirectory(createDirectory, copyFile, isLeftOrphan, leftBase, rightBase, p, ie));
                    break;
            }
        }
        else
        {
            switch (oldEntry)
            {
                case SyncEntriesLeaf oldLeaf:
                    var ut = File.GetLastWriteTimeUtc(orphan);
                    if (ut != oldLeaf.LastModifiedUtc)
                    {
                        // 運用ミス。一方でファイルが削除され、他方のファイルが更新された
                        Console.WriteLine($"[Operation Mistake] File {missing} was deleted, but file {orphan} was updated.");
                        newSyncEntries.Add(name, oldEntry);
                        return;
                    }
                    // ファイルが削除された。
                    deleteFile(isLeftOrphan, orphanBase, Path.Combine(path, name));
                    break;
                default:    // null or SyncEntries
                    // ファイルが新規作成された
                    // あるいは、特殊運用。両方からディレクトリが削除され、一方にファイルが新規作成された
                    newSyncEntries.Add(name, copyFile(isLeftOrphan, leftBase, rightBase, Path.Combine(path, name)));
                    break;
            }
        }
    }

    /// <summary>
    /// 同名エントリーだが、一つはファイル、もう一つはディレクトリという違うタイプの場合の処理。<br/>
    /// SynchronizeDirectoryPairの下請け。
    /// </summary>
    static void SynchronizeDirectoryAndFile(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string> deleteEmptyDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        Action<bool, string, string> deleteFile,
        bool isLeftDirectory,
        string leftBase,
        string rightBase,
        string path,
        string name,
        IgnoreEntries ignoreEntries,
        SyncEntriesNode? oldEntry,
        SyncEntries newSyncEntries)
    {
        if (oldEntry == null)
        {
            // 運用ミス。左右で異なる種類のものが新規作成された
            Console.WriteLine($"[Operation Error] A directory was created on one side and a file on the other.{Path.Combine(path, name)}");
            return;
        }

        var (directoryBase, fileBase) = GetSrcDest(isLeftDirectory, leftBase, rightBase);
        switch (oldEntry)
        {
            case SyncEntries entries:
                var directoryPath = Path.Combine(directoryBase, path, name);
                if (IsDirectoryUpdated(directoryPath, ignoreEntries.GetSubEntries(name), entries))
                {
                    // 運用ミス。一方ではディレクトリが削除され、同名のファイルが作成されたのに、もう一方ではディレクトリが更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + " while the directory was updated on the other. "
                        + Path.Combine(path, name));
                    newSyncEntries.Add(name, entries);
                    return;
                }
                // 特殊運用。ディレクトリが削除され、同名のファイルが作成された
                DeleteDirectory(deleteFile, deleteEmptyDirectory,
                    isLeftDirectory, directoryBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name));
                newSyncEntries.Add(name, copyFile(!isLeftDirectory, leftBase, rightBase, Path.Combine(path, name)));
                break;
            case SyncEntriesLeaf leaf:
                var filePath = Path.Combine(fileBase, path, name);
                if (IsFileUpdated(filePath, leaf))
                {
                    // 運用ミス。右のファイルは削除され、同名のディレクトリが作成されたのに、左のファイルは更新されている
                    Console.WriteLine("[Operation Error]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + " while the file was updated on the other. "
                        + Path.Combine(path, name));
                    newSyncEntries.Add(name, leaf);
                    return;
                }
                // 特殊運用。ファイルが削除され、同名のディレクトリが新規作成された
                deleteFile(isLeftDirectory, fileBase,
                    Path.Combine(path, name));
                newSyncEntries.Add(name, CopyDirectory(createDirectory, copyFile,
                    isLeftDirectory, leftBase, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name)));
                break;
        }
    }

    /// <summary>
    /// 二つのファイルの同期処理<br/>
    /// SynchronizeDirectoryPairの下請け。
    /// </summary>
    static void SynchronizeFilePair(
        Func<bool, string, string, string, SyncEntriesNode> replaceFile,
        string leftBase, string rightBase, string path, string name, SyncEntriesNode? oldEntry,
        SyncEntries newSyncEntries)
    {
        var p = Path.Combine(path, name);
        var left = Path.Combine(leftBase, p);
        var right = Path.Combine(rightBase, p);
        var leftUpdateTime = File.GetLastWriteTimeUtc(left);
        var rightUpdateTime = File.GetLastWriteTimeUtc(right);
        switch (oldEntry)
        {
            case null:
                // 左右でファイルが新規作成された
                if (!IsSameFile(left, right))
                {
                    // 運用ミス。左右でファイルが新規作成されたが、更新日時が異なる
                    Console.WriteLine($"[Operation Error] File was created on both sides, but they are different: {p}");
                    return;
                }
                // 特殊運用。左右でファイルが新規作成されたが、更新日時が同じ
                newSyncEntries.Add(name, CreateSyncEntriesLeaf(left));
                break;
            case SyncEntries:
                // 左右でディレクトリが削除され、ファイルが新規作成された
                if (!IsSameFile(left, right))
                {
                    // 運用ミス。左右でファイルが新規作成されたが、更新日時が異なる
                    Console.WriteLine($"[Operation Error] File was created on both sides, but they are different: {p}");
                    newSyncEntries.Add(name, oldEntry);
                    return;
                }
                // 特殊運用。左右でファイルが新規作成されたが、更新日時が同じ
                newSyncEntries.Add(name, CreateSyncEntriesLeaf(left));
                break;
            case SyncEntriesLeaf leaf:
                // 左右でファイルが存在し続けている
                if (leftUpdateTime > rightUpdateTime)
                {
                    if (rightUpdateTime != leaf.LastModifiedUtc)
                    {
                        // 運用ミス。左右で別々に更新された
                        Console.WriteLine($"[Operation Error] File was updated on both side: {p}");
                        newSyncEntries.Add(name, oldEntry);
                        return;
                    }
                    // 左のファイルが更新された
                    newSyncEntries.Add(name, replaceFile(true, leftBase, rightBase, p));
                }
                else if (leftUpdateTime < rightUpdateTime)
                {
                    if (leftUpdateTime != leaf.LastModifiedUtc)
                    {
                        // 運用ミス。左右で別々に更新された
                        Console.WriteLine($"[Operation Error] File was updated on both side: {p}");
                        newSyncEntries.Add(name, oldEntry);
                        return;
                    }
                    // 右のファイルが更新された
                    newSyncEntries.Add(name, replaceFile(false, leftBase, rightBase, p));
                }
                else
                {
                    if (leftUpdateTime != leaf.LastModifiedUtc)
                    {
                        // 特殊運用。左右で別々に更新されたが、手動同期済みだった
                        newSyncEntries.Add(name, new SyncEntriesLeaf(leftUpdateTime));
                    }
                    else
                    {
                        // ファイルは更新されていない
                        newSyncEntries.Add(name, oldEntry);
                    }
                }
                break;
        }
    }

    static bool IsDirectory(string path, string entryName) => Directory.Exists(Path.Combine(path, entryName));

    /// <summary>
    /// ディレクトリ内に、更新されたファイルがあるかどうかを返す。
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ignoreEntries"></param>
    /// <param name="oldSyncEntries"></param>
    /// <returns></returns>
    private static bool IsDirectoryUpdated(string path, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        foreach (var name in EntryEnumerator.Enumerate(path, ignoreEntries))
        {
            var p = Path.Combine(path, name);
            var node = oldSyncEntries.Get(name);
            switch (node)
            {
                case null:
                    return true;    // ファイルまたはディレクトリが新規作成された
                case SyncEntries entries:
                    if (!Directory.Exists(p))
                    {
                        return true;    // 以前はディレクトリだったのに、ファイルに変わっている
                    }
                    if (IsDirectoryUpdated(p, ignoreEntries.GetSubEntries(name), entries))
                    {
                        return true;
                    }
                    break;
                case SyncEntriesLeaf leaf:
                    if (Directory.Exists(p))
                    {
                        return true;    // 以前はファイルだったのに、ディレクトリに変わっている
                    }
                    if (IsFileUpdated(path, leaf))
                    {
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    static bool IsSameFile(string left, string right)
    {
        var leftUpdateTime = File.GetLastWriteTimeUtc(left);
        var rightUpdateTime = File.GetLastWriteTimeUtc(right);
        return leftUpdateTime != rightUpdateTime;
    }

    static bool IsFileUpdated(string path, SyncEntriesLeaf oldEntry)
    {
        var modifiedTime = File.GetLastWriteTimeUtc(path);
        return modifiedTime != oldEntry.LastModifiedUtc;
    }

    /// <summary>
    /// ディレクトリをコピーする<br/>
    /// 
    /// </summary>
    /// <param name="sourceDirectoryPath"></param>
    /// <param name="destinationDirectoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <returns>コピーしたエントリー(ファイル、ディレクトリ)の情報</returns>
    /// <exception cref="NotImplementedException"></exception>
    static SyncEntries CopyDirectory(
        Action<bool, string, string> createDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile,
        bool leftToRight, string leftBase, string rightBase, string path, IgnoreEntries ignoreEntries)
    {
        var newEntries = new SyncEntries();
        var srcBase = leftToRight ? leftBase : rightBase;
        var src = Path.Combine(srcBase, path);
        createDirectory(!leftToRight, srcBase, path);
        foreach (var name in EntryEnumerator.Enumerate(src, ignoreEntries))
        {
            var p = Path.Combine(path, name);
            if (IsDirectory(src, name))
                newEntries.Add(name, CopyDirectory(createDirectory, copyFile, leftToRight, leftBase, rightBase, p, ignoreEntries.GetSubEntries(name)));
            else
                newEntries.Add(name, copyFile(leftToRight, leftBase, rightBase, p));
        }
        return newEntries;
    }

    static SyncEntriesLeaf CreateSyncEntriesLeaf(string filePath)
    {
        var lastModifiedUtc = File.GetLastWriteTimeUtc(filePath);
        return new SyncEntriesLeaf(lastModifiedUtc);
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
        IgnoreEntries ignoreEntries)
    {
        foreach (var name in EntryEnumerator.Enumerate(Path.Combine(basePath, path), ignoreEntries))
        {
            var p = Path.Combine(path, name);
            if (Directory.Exists(Path.Combine(basePath, p)))
            {
                DeleteDirectory(
                    deleteFile, deleteEmptyDirectory,
                    isLeft, basePath, p, ignoreEntries.GetSubEntries(name));
            }
            else
            {
                deleteFile(isLeft, basePath, p);
            }
        }
        deleteEmptyDirectory(isLeft, basePath, path);
    }

    static void PrintCreateDirectory(bool isLeft, string _, string path)
    {
        if (isLeft)
            Console.WriteLine($"[CREATE  >] {path}");
        else
            Console.WriteLine($"[<  CREATE] {path}");
    }

    static void CreateDirectory(bool isLeft, string basePath, string path)
    {
        PrintCreateDirectory(isLeft, basePath, path);
        Directory.CreateDirectory(Path.Combine(basePath, path));
    }

    static void PrintDeleteEmptyDirectory(bool isLeft, string _, string path)
    {
        if (isLeft)
            Console.WriteLine($"[< DELETE] {path}");
        else
            Console.WriteLine($"[DELETE >] {path}");
    }

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
        if (leftToRight)
            Console.WriteLine($"[COPY    >] {path}");
        else
            Console.WriteLine($"[<    COPY] {path}");
        return _dummySyncEntriesLeaf;
    }

    static SyncEntriesLeaf CopyFile(bool leftToRight, string leftBase, string rightBase, string path)
    {
        PrintCopyFile(leftToRight, leftBase, rightBase, path);
        var (src, dest) = GetSrcDest(leftToRight, Path.Combine(leftBase, path), Path.Combine(rightBase, path));
        File.Copy(src, dest, false);
        return CreateSyncEntriesLeaf(src);
    }

    static SyncEntriesLeaf PrintReplaceFile(bool leftToRight, string _, string __, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[REPLACE >] {path}");
        else
            Console.WriteLine($"[< REPLACE] {path}");
        return _dummySyncEntriesLeaf;
    }

    private static SyncEntriesLeaf ReplaceFile(bool leftToRight, string leftBase, string rightBase, string path)
    {
        PrintReplaceFile(leftToRight, leftBase, rightBase, path);
        var (src, dest) = GetSrcDest(leftToRight, Path.Combine(leftBase, path), Path.Combine(rightBase, path));
        FileUtils.ReplaceFile(src, dest);
        return CreateSyncEntriesLeaf(src);
    }

    static void PrintDeleteFile(bool isLeft, string _, string path)
    {
        if (isLeft)
            Console.WriteLine($"[< DELETE] {path}");
        else
            Console.WriteLine($"[DELETE >] {path}");
    }

    static void DeleteFile(bool isLeft, string basePath, string path)
    {
        PrintDeleteFile(isLeft, basePath, path);
        RecycleBin.MoveToRecycleBin(Path.Combine(basePath, path));
    }

    static (string src, string dest) GetSrcDest(bool leftToRight, string leftPath, string rightPath)
    {
        if (leftToRight)
            return (leftPath, rightPath);
        else
            return (rightPath, leftPath);
    }
}
