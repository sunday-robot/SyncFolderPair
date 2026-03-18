using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectorySynchronizer
{
    static readonly SyncEntriesLeaf _dummySyncEntriesLeaf = new(DateTime.MinValue, 0);

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
        return SynchronizeCore(
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
        SynchronizeCore(
            PrintCreateDirectory, PrintDeleteEmptyDirectory, PrintCopyFile, PrintReplaceFile, PrintDeleteFile,
            leftDirectoryPath, rightDirectoryPath, "", ignoreEntries, oldSyncEntries);
    }

    static SyncEntries SynchronizeCore(
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
            var oldEntry = oldSyncEntries.Nodes[name];

            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    // 左にしかない
                    SynchronizeOrphanEntry(
                        createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                        true, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                        newSyncEntries.Nodes);
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
                            newSyncEntries.Nodes[name] = SynchronizeCore(
                                createDirectory, deleteEmptyDirectory, copyFile, replaceFile, deleteFile,
                                leftBase, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name), oldSubSyncEntries);
                        }
                        else
                        {
                            // 左はディレクトリ、右はファイルである
                            SynchronizeDifferentTypeEntry(
                                createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                                true, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                                newSyncEntries.Nodes);
                            break;
                        }
                    }
                    else
                    {
                        if (IsDirectory(right, name))
                        {
                            // 左はファイル、右はディレクトリである
                            switch (oldEntry)
                            {
                                case null:
                                    // 運用ミス。左にはファイルが新規作成され、右にはディレクトリが新規作成された
                                    Console.WriteLine($"[Conflict] 一方ではディレクトリ、もう片方ではファイルが新規作成されています。");
                                    // newSyncEntriesには追加しない
                                    continue;
                                case SyncEntries:
                                    // 右にはディレクトリが存在し続けているが、左のディレクトリは削除され、同名のファイルが作成された
                                    if (IsDirectoryUpdated(Path.Combine(right, name), ignoreEntries.GetSubEntries(name), (SyncEntries)oldEntry))
                                    {
                                        // 運用ミス。左のディレクトリは削除され、同名のファイルが作成されたのに、右のディレクトリは更新されている
                                        Console.WriteLine($"[Operation Mistake] Directory updated on right side: {Path.Combine(right, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左のディレクトリが削除され、同名のファイルが作成された
                                    DeleteDirectory(
                                        deleteFile, deleteEmptyDirectory,
                                        false, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name));
                                    newSyncEntries.Nodes[name] = copyFile(true, leftBase, rightBase, Path.Combine(path, name));
                                    break;
                                default:    // SyncEntriesLeaf
                                    // 左にはファイルが存在し続けているが、右はファイルが削除され、ディレクトリが新規作成された
                                    if (IsFileUpdated(left, name, (SyncEntriesLeaf)oldEntry))
                                    {
                                        // 運用ミス。右のファイルは削除され、同名のディレクトリが作成されたのに、左のファイルは更新されている
                                        Console.WriteLine($"[Operation Mistake] File updated on left side: {Path.Combine(left, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。右のファイルが削除され、同名のディレクトリが新規作成された
                                    deleteFile(true, leftBase, Path.Combine(path, name));
                                    newSyncEntries.Nodes[name] = CopyDirectory(false, leftBase, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name),
                                        createDirectory, copyFile);
                                    break;
                            }
                        }
                        else
                        {
                            // 左右どちらもファイルである
                            var leftUpdateTime = GetLastWriteTimeUtc(left, name);
                            var rightUpdateTime = GetLastWriteTimeUtc(right, name);
                            switch (oldEntry)
                            {
                                case SyncEntriesLeaf:
                                    // 左右でファイルが存在し続けている
                                    if (leftUpdateTime > rightUpdateTime)
                                    {
                                        if (rightUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            // 運用ミス。左右で別々に更新された
                                            Console.WriteLine($"[Operation Mistake] File updated on both side: {left}, {right}, {name}");
                                            newSyncEntries.Nodes[name] = oldEntry;
                                            continue;
                                        }
                                        // 左のファイルが更新された
                                        newSyncEntries.Nodes[name] = replaceFile(true, leftBase, rightBase, Path.Combine(path, name));
                                        continue;
                                    }
                                    else if (leftUpdateTime < rightUpdateTime)
                                    {
                                        if (leftUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            // 運用ミス。左右で別々に更新された
                                            Console.WriteLine($"[Operation Mistake] File updated on both side: {left}, {right}, {name}");
                                            newSyncEntries.Nodes[name] = oldEntry;
                                            continue;
                                        }
                                        // 右のファイルが更新された
                                        newSyncEntries.Nodes[name] = replaceFile(false, leftBase, rightBase, Path.Combine(path, name));
                                        continue;
                                    }
                                    else
                                    {
                                        if (leftUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            // 特殊運用。左右で別々に更新されたが、手動同期済みだった
                                            var size = GetFileSize(left, name);
                                            newSyncEntries.Nodes[name] = new SyncEntriesLeaf(leftUpdateTime, size);
                                        }
                                        else
                                        {
                                            // ファイルは更新されていない
                                            newSyncEntries.Nodes[name] = oldEntry;
                                        }
                                    }
                                    break;
                                default:    // null or SyncEntries
                                    // 左右でファイルが新規作成された
                                    // あるいは、左右でディレクトリが削除され、ファイルが新規作成された
                                    if (leftUpdateTime != rightUpdateTime)
                                    {
                                        // 運用ミス。左右でファイルが新規作成されたが、更新日時が異なる
                                        Console.WriteLine($"[Conflict] File:{name} was created on both sides with different timestamps: {left} ({leftUpdateTime}), {right} ({rightUpdateTime})");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    var leftSize = GetFileSize(left, name);
                                    var rightSize = GetFileSize(right, name);
                                    if (leftSize != rightSize)
                                    {
                                        // 異常事態。左右でファイルが新規作成されたが、更新日時が同じなのにファイルサイズが異なる
                                        Console.WriteLine($"[Operation Mistake] File:{name} was created on both sides with different sizes: {left} ({leftSize}), {right} ({rightSize})");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左右でファイルが新規作成されたが、更新日時もファイルサイズも同じ
                                    newSyncEntries.Nodes[name] = new SyncEntriesLeaf(leftUpdateTime, leftSize);
                                    break;
                            }
                        }
                    }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    // 右にしかない
                    SynchronizeOrphanEntry(
                        createDirectory, deleteEmptyDirectory, copyFile, deleteFile,
                        false, leftBase, rightBase, path, name, ignoreEntries, oldEntry,
                        newSyncEntries.Nodes);
                    break;
            }
        }

        return newSyncEntries;
    }

    /// <summary>
    /// 片方にしかファイルあるいはディレクトリがない場合の処理<br/>
    /// SynchronizeCoreの下請け。
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
        SyncEntriesNode oldEntry,
        IDictionary<string, SyncEntriesNode> newSyncEntriesNodes)
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
                        newSyncEntriesNodes[name] = oldEntry;
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
                    newSyncEntriesNodes[name] = CopyDirectory(isLeftOrphan, leftBase, rightBase, p, ie, createDirectory, copyFile);
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
                        newSyncEntriesNodes[name] = oldEntry;
                        return;
                    }
                    // ファイルが削除された。
                    deleteFile(isLeftOrphan, orphanBase, Path.Combine(path, name));
                    break;
                default:    // null or SyncEntries
                    // ファイルが新規作成された
                    // あるいは、特殊運用。両方からディレクトリが削除され、一方にファイルが新規作成された
                    newSyncEntriesNodes[name] = copyFile(isLeftOrphan, leftBase, rightBase, Path.Combine(path, name));
                    break;
            }
        }
    }

    /// <summary>
    /// 同名エントリーだが、一つはファイル、もう一つはディレクトリという違うタイプの場合の処理。<br/>
    /// SynchronizeCoreの下請け。
    /// </summary>
    static void SynchronizeDifferentTypeEntry(
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
        SyncEntriesNode oldEntry,
        IDictionary<string, SyncEntriesNode> newSyncEntriesNode)
    {
        if (oldEntry == null)
        {
            // 運用ミス。左右で異なる種類のものが新規作成された
            Console.WriteLine($"[Conflict] A directory was created on one side and a file on the other.{Path.Combine(path, name)}");
            return;
        }

        var (directoryBase, fileBase) = GetSrcDest(isLeftDirectory, leftBase, rightBase);
        var directoryPath = Path.Combine(directoryBase, path, name);
        var filePath = Path.Combine(fileBase, path, name);

        switch (oldEntry)
        {
            case SyncEntries:
                if (IsDirectoryUpdated(directoryPath, ignoreEntries.GetSubEntries(name), (SyncEntries)oldEntry))
                {
                    // 運用ミス。一方ではディレクトリが削除され、同名のファイルが作成されたのに、もう一方ではディレクトリが更新されている
                    Console.WriteLine("[Operation Mistake]"
                        + " A directory was deleted and a file with the same name was created on one side,"
                        + " while the directory was updated on the other. "
                        + Path.Combine(path, name));
                    newSyncEntriesNode[name] = oldEntry;
                    return;
                }
                // 特殊運用。ディレクトリが削除され、同名のファイルが作成された
                DeleteDirectory(
                    deleteFile, deleteEmptyDirectory,
                    isLeftDirectory, directoryBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name));
                newSyncEntriesNode[name] = copyFile(!isLeftDirectory, leftBase, rightBase, Path.Combine(path, name));
                break;
            default:    // SyncEntriesLeaf
                if (IsFileUpdated(Path.Combine(fileBase, path), name, (SyncEntriesLeaf)oldEntry))
                {
                    // 運用ミス。右のファイルは削除され、同名のディレクトリが作成されたのに、左のファイルは更新されている
                    Console.WriteLine("[Operation Mistake]"
                        + " A file was deleted and a directory with the same name was created on one side,"
                        + " while the file was updated on the other. "
                        + Path.Combine(path, name));
                    newSyncEntriesNode[name] = oldEntry;
                    return;
                }
                // 特殊運用。ファイルが削除され、同名のディレクトリが新規作成された
                deleteFile(isLeftDirectory, fileBase, Path.Combine(path, name));
                newSyncEntriesNode[name] = CopyDirectory(isLeftDirectory, leftBase, rightBase, Path.Combine(path, name), ignoreEntries.GetSubEntries(name),
                    createDirectory, copyFile);
                break;
        }
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    private static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));

    static long GetFileSize(string directoryPath, string fileName) => FileUtils.GetSize(Path.Combine(directoryPath, fileName));

    /// <summary>
    /// ディレクトリ内に、更新されたファイルがあるかどうかを返す。
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="ignoreEntries"></param>
    /// <param name="oldSyncEntries"></param>
    /// <returns></returns>
    private static bool IsDirectoryUpdated(string directoryPath, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        foreach (var name in EntryEnumerator.Enumerate(directoryPath, ignoreEntries))
        {
            var node = oldSyncEntries.Nodes[name];
            if (IsDirectory(directoryPath, name))
            {
                switch (node)
                {
                    case SyncEntriesLeaf:
                        // 以前はファイルだったのに、ディレクトリに変わっている
                        return true;
                    default:
                        if (node == null)
                            node = new SyncEntries();
                        if (IsDirectoryUpdated(Path.Combine(directoryPath, name), ignoreEntries.GetSubEntries(name), (SyncEntries)node))
                        {
                            return true;
                        }
                        break;
                }
            }
            else
            {
                switch (node)
                {
                    case SyncEntriesLeaf leaf:
                        if (IsFileUpdated(directoryPath, name, leaf))
                        {
                            return true;
                        }
                        break;
                    default:    // null or SyncEntries
                        // 以前は存在しなかったが、ファイルが新規作成された
                        // あるいは、以前はディレクトリだったのに、削除され、ファイルが作成された
                        return true;
                }
            }
        }
        return false;
    }

    private static bool IsFileUpdated(string directoryPath, string fileName, SyncEntriesLeaf oldEntry)
    {
        var modifiedTime = GetLastWriteTimeUtc(directoryPath, fileName);
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
    static SyncEntries CopyDirectory(bool leftToRight, string leftBase, string rightBase, string path, IgnoreEntries ignoreEntries,
        Action<bool, string, string> createDirectory,
        Func<bool, string, string, string, SyncEntriesNode> copyFile)
    {
        var newEntries = new SyncEntries();
        var srcBase = leftToRight ? leftBase : rightBase;
        var src = Path.Combine(srcBase, path);
        createDirectory(!leftToRight, srcBase, path);
        foreach (var name in EntryEnumerator.Enumerate(src, ignoreEntries))
        {
            var p = Path.Combine(path, name);
            if (IsDirectory(src, name))
                newEntries.Nodes[name] = CopyDirectory(leftToRight, leftBase, rightBase, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile);
            else
                newEntries.Nodes[name] = copyFile(leftToRight, leftBase, rightBase, p);
        }
        return newEntries;
    }

    static SyncEntriesLeaf CreateSyncEntriesLeaf(string filePath)
    {
        var lastModifiedUtc = File.GetLastWriteTimeUtc(filePath);
        var size = FileUtils.GetSize(filePath);
        return new SyncEntriesLeaf(lastModifiedUtc, size);
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
        var dirPath = Path.Combine(basePath, path);
        foreach (var name in EntryEnumerator.Enumerate(dirPath, ignoreEntries))
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
        deleteEmptyDirectory(false, "", path);
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

    static void DeleteEmptyDirectory(bool isLeft, string _, string path)
    {
        PrintDeleteEmptyDirectory(isLeft, "", path);
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
