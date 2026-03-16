using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Win32Api.Win32;

namespace SyncFolderPair.Services;

public static class DirectorySynchronizer
{
    public static SyncEntries Synchronize(
        string leftDirectoryPath,
        string rightDirectoryPath,
        IgnoreEntries ignoreEntries,
        SyncEntries oldSyncEntries)
    {
        var newSyncEntries = new SyncEntries();
        foreach (var e in EntryEnumerator.Enumerate(leftDirectoryPath, rightDirectoryPath, ignoreEntries))
        {
            var name = e.Item1;
            var oldEntry = oldSyncEntries.Nodes[name];

            SyncEntriesNode? entry;
            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    // 左にしかない
                    entry = SynchronizeSingleSideEntry(leftDirectoryPath, rightDirectoryPath, name, ignoreEntries, oldEntry);
                    if (entry != null)
                        newSyncEntries.Nodes[name] = entry;
                    break;
                case PairEnumerator.Existance.Both:
                    // 左右両方にある
                    if (IsDirectory(leftDirectoryPath, name))
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左右両方ともディレクトリである
                            SyncEntries oldSubSyncEntries = oldEntry switch
                            {
                                SyncEntries => (SyncEntries)oldEntry,// 左右でディレクトリが存在し続けている
                                _ => new SyncEntries(),// 左右でディレクトリが新規作成された。あるいは、左右両方からファイルが削除され、左右どちらにもディレクトリが新規作成された
                            };
                            newSyncEntries.Nodes[name] = Synchronize(
                                Path.Combine(leftDirectoryPath, name),
                                Path.Combine(rightDirectoryPath, name),
                                ignoreEntries.GetSubEntries(name),
                                oldSubSyncEntries);
                        }
                        else
                        {
                            // 左はディレクトリ、右はファイルである
                            switch (oldEntry)
                            {
                                case null:
                                    // 運用ミス。左にはディレクトリが新規作成され、右にはファイルが新規作成された
                                    Console.WriteLine($"[Conflict] 一方ではディレクトリ、もう片方ではファイルが新規作成されています。");
                                    // newSyncEntriesには追加しない
                                    continue;
                                case SyncEntries:
                                    // 左にはディレクトリが存在し続けているが、右はディレクトリが削除され、ファイルが新規作成された
                                    if (IsDirectoryUpdated(leftDirectoryPath, name, ignoreEntries.GetSubEntries(name), (SyncEntries)oldEntry))
                                    {
                                        // 運用ミス。左のディレクトリが更新されていた
                                        Console.WriteLine($"[Operation Mistake] Directory updated on left side: {Path.Combine(leftDirectoryPath, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左のディレクトリは更新されていないが、右のディレクトリが削除され、ファイルが新規作成された
                                    MoveDirectoryToRecycleBin(leftDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                                    newSyncEntries.Nodes[name] = CopyFile(rightDirectoryPath, leftDirectoryPath, name);
                                    break;
                                default:    // SyncEntriesLeaf
                                    // 左のファイルは削除され、同名のディレクトリが作成された
                                    if (IsFileUpdated(rightDirectoryPath, name, (SyncEntriesLeaf)oldEntry))
                                    {
                                        // 運用ミス。左のファイルは削除され、同名のディレクトリが作成されたのに、右のファイルは更新されている
                                        Console.WriteLine($"[Operation Mistake] File updated on left side: {Path.Combine(leftDirectoryPath, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左のファイルが削除され、同名のディレクトリが作成された
                                    MoveFileToRecycleBin(rightDirectoryPath, name);
                                    newSyncEntries.Nodes[name] = CopyDirectory(leftDirectoryPath, rightDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左はファイル、右はディレクトリである
                            switch (oldEntry)
                            {
                                case null:
                                    // 運用ミス。左にはファイルが新規作成され、右にはディレクトリが新規作成された
                                    Console.WriteLine($"[Conflict] 一方ではディレクトリ、もう片方ではファイルが新規作成されています。");
                                    // newSyncEntriesには追加しない
                                    continue;
                                case SyncEntriesLeaf:
                                    // 左にはファイルが存在し続けているが、右はファイルが削除され、ディレクトリが新規作成された
                                    if (IsFileUpdated(leftDirectoryPath, name, (SyncEntriesLeaf)oldEntry))
                                    {
                                        // 運用ミス。左のファイルは更新され、右のファイルは削除され、ディレクトリが新規作成された
                                        Console.WriteLine($"[Operation Mistake] File updated on left side: {Path.Combine(leftDirectoryPath, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左のファイルは更新されていないが、右のファイルが削除され、ディレクトリが新規作成された
                                    MoveFileToRecycleBin(leftDirectoryPath, name);
                                    newSyncEntries.Nodes[name] = CopyDirectory(rightDirectoryPath, leftDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                                    break;
                                default:    // SyncEntries
                                    // 左のディレクトリは削除され、同名のファイルが作成された
                                    if (IsDirectoryUpdated(rightDirectoryPath, name, ignoreEntries.GetSubEntries(name), (SyncEntries)oldEntry))
                                    {
                                        // 運用ミス。左のディレクトリは削除され、同名のファイルが作成されたのに、右のディレクトリは更新されている
                                        Console.WriteLine($"[Operation Mistake] Directory updated on left side: {Path.Combine(leftDirectoryPath, name)}");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    // 特殊運用。左のディレクトリが削除され、同名のファイルが作成された
                                    MoveDirectoryToRecycleBin(rightDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                                    newSyncEntries.Nodes[name] = CopyFile(leftDirectoryPath, rightDirectoryPath, name);
                                    break;
                            }
                        }
                        else
                        {
                            // 左右どちらもファイルである
                            var leftUpdateTime = GetLastWriteTimeUtc(leftDirectoryPath, name);
                            var rightUpdateTime = GetLastWriteTimeUtc(rightDirectoryPath, name);
                            switch (oldEntry)
                            {
                                case SyncEntriesLeaf:
                                    // 左右でファイルが存在し続けている
                                    if (leftUpdateTime > rightUpdateTime)
                                    {
                                        if (rightUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            // 運用ミス。左右で別々に更新された
                                            Console.WriteLine($"[Operation Mistake] File updated on both side: {leftDirectoryPath}, {rightDirectoryPath}, {name}");
                                            newSyncEntries.Nodes[name] = oldEntry;
                                            continue;
                                        }
                                        // 左のファイルが更新された
                                        newSyncEntries.Nodes[name] = CopyFile(leftDirectoryPath, rightDirectoryPath, name);
                                        continue;
                                    }
                                    else if (leftUpdateTime < rightUpdateTime)
                                    {
                                        if (leftUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            // 運用ミス。左右で別々に更新された
                                            Console.WriteLine($"[Operation Mistake] File updated on both side: {leftDirectoryPath}, {rightDirectoryPath}, {name}");
                                            newSyncEntries.Nodes[name] = oldEntry;
                                            continue;
                                        }
                                        // 右のファイルが更新された
                                        newSyncEntries.Nodes[name] = CopyFile(rightDirectoryPath, leftDirectoryPath, name);
                                        continue;
                                    }
                                    else
                                    {
                                        if (leftUpdateTime != ((SyncEntriesLeaf)oldEntry).LastModifiedUtc)
                                        {
                                            var size = GetFileSize(leftDirectoryPath, name);
                                            newSyncEntries.Nodes[name] = new SyncEntriesLeaf(leftUpdateTime, size);
                                        }
                                        else
                                        {
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
                                        Console.WriteLine($"[Conflict] File:{name} was created on both sides with different timestamps: {leftDirectoryPath} ({leftUpdateTime}), {rightDirectoryPath} ({rightUpdateTime})");
                                        newSyncEntries.Nodes[name] = oldEntry;
                                        continue;
                                    }
                                    var leftSize = GetFileSize(leftDirectoryPath, name);
                                    var rightSize = GetFileSize(rightDirectoryPath, name);
                                    if (leftSize != rightSize)
                                    {
                                        // 異常事態。左右でファイルが新規作成されたが、更新日時が同じなのにファイルサイズが異なる
                                        Console.WriteLine($"[Operation Mistake] File:{name} was created on both sides with different sizes: {leftDirectoryPath} ({leftSize}), {rightDirectoryPath} ({rightSize})");
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
                    entry = SynchronizeSingleSideEntry(rightDirectoryPath, leftDirectoryPath, name, ignoreEntries, oldEntry);
                    if (entry != null)
                        newSyncEntries.Nodes[name] = entry;
                    break;
            }
        }

        return newSyncEntries;
    }

    static SyncEntriesNode? SynchronizeSingleSideEntry(
        string aDirectoryPath,
        string bDirectoryPath,
        string name,
        IgnoreEntries ignoreEntries,
        SyncEntriesNode oldEntry)
    {
        if (IsDirectory(aDirectoryPath, name))
        {
            switch (oldEntry)
            {
                case null:
                    // Aにディレクトリが新規作成された
                    return CopyDirectory(aDirectoryPath, bDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                case SyncEntries:
                    if (IsDirectoryUpdated(aDirectoryPath, name, ignoreEntries.GetSubEntries(name), (SyncEntries)oldEntry))
                    {
                        // 運用ミス。Bのディレクトリが削除されたのに、Aのディレクトリが更新された
                        Console.WriteLine($"[Operation Mistake] Directory {Path.Combine(bDirectoryPath, name)} was deleted, but directory {Path.Combine(aDirectoryPath, name)} was updated.");
                        return oldEntry;
                    }
                    // Bからディレクトリが削除された
                    MoveDirectoryToRecycleBin(aDirectoryPath, name, ignoreEntries.GetSubEntries(name));
                    return null;
                default: // SyncEntriesLeaf
                    // 特殊運用。AB両方からファイルが削除され、Aにディレクトリが新規作成された
                    return CopyDirectory(aDirectoryPath, bDirectoryPath, name, ignoreEntries.GetSubEntries(name));
            }
        }
        else
        {
            switch (oldEntry)
            {
                case SyncEntriesLeaf oldLeaf:
                    var ut = GetLastWriteTimeUtc(aDirectoryPath, name);
                    if (ut != oldLeaf.LastModifiedUtc)
                    {
                        // 運用ミス。Bのファイルが削除されたのに、Aのファイルが更新された
                        Console.WriteLine($"[Operation Mistake] File {Path.Combine(bDirectoryPath, name)} was deleted, but file {{Path.Combine(aDirectoryPath, name)}} was updated.");
                        return oldEntry;
                    }
                    var size = GetFileSize(aDirectoryPath, name);
                    if (size != oldLeaf.Size)
                    {
                        // 異常事態。Aのファイルの更新日時は変わっていないのに、ファイルサイズが変わっている
                        Console.WriteLine($"TODO 更新日時が変わらないのに、ファイルサイズが変わっています。{Path.Combine(aDirectoryPath, name)}");
                        return oldEntry;
                    }
                    // Bのファイルが削除された。
                    MoveFileToRecycleBin(aDirectoryPath, name);
                    return null;
                default:    // null or SyncEntries
                    // Aにファイルが新規作成された
                    // あるいは、AB両方からディレクトリが削除され、Aにファイルが新規作成された
                    return CopyFile(aDirectoryPath, bDirectoryPath, name);
            }
        }
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    private static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));

    static long GetFileSize(string filePath)
    {
        var info = new FileInfo(filePath)!;
        var size = info.Length;
        return size;
    }

    static long GetFileSize(string directoryPath, string fileName) => GetFileSize(Path.Combine(directoryPath, fileName));

    private static bool IsDirectoryUpdated(string parentDirectoryPath, string directoryName, IgnoreEntries ignoreEntries, SyncEntries oldSyncEntries)
    {
        var directoryPath = Path.Combine(parentDirectoryPath, directoryName);
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
                        if (IsDirectoryUpdated(directoryPath, name, ignoreEntries.GetSubEntries(name), (SyncEntries)node))
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
    /// <returns>コピーしたエントリー（ファイル、ディレクトリ）の情報</returns>
    /// <exception cref="NotImplementedException"></exception>
    static SyncEntries CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath, string directoryName, IgnoreEntries ignoreEntries)
    {
        var newEntries = new SyncEntries();
        var src = Path.Combine(sourceDirectoryPath, directoryName);
        var dst = Path.Combine(destinationDirectoryPath, directoryName);
        Directory.CreateDirectory(dst);
        foreach (var name in EntryEnumerator.Enumerate(src, ignoreEntries))
        {
            if (IsDirectory(src, name))
            {
                newEntries.Nodes[name] = CopyDirectory(src, dst, name, ignoreEntries.GetSubEntries(name));
            }
            else
            {
                newEntries.Nodes[name] = CopyFile(src, dst, name);
            }
        }
        return newEntries;
    }

    static SyncEntriesLeaf CopyFile(string sourceDirectoryPath, string destinationDirectoryPath, string fileName)
    {
        var src = Path.Combine(sourceDirectoryPath, fileName);
        var dst = Path.Combine(destinationDirectoryPath, fileName);
        File.Copy(src, dst, false);
        var lastModifiedUtc = File.GetLastWriteTimeUtc(src);
        var size = GetFileSize(src);
        return new SyncEntriesLeaf(lastModifiedUtc, size);
    }

    /// <summary>
    /// ディレクトリをゴミ箱に移動させる<br/>
    /// 上記説明は不正確。正確にはディレクトリ内のファイルのうち、無視ディレクトリ内のファイル以外のファイルをゴミ箱に移動させ、ディレクトリ自体は削除する。<br/>
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="directoryName"></param>
    /// <param name="ignoreEntries"></param>
    /// <exception cref="Win32Exception"></exception>
    private static void MoveDirectoryToRecycleBin(string directoryPath, string directoryName, IgnoreEntries ignoreEntries)
    {
        var p = Path.Combine(directoryPath, directoryName);
        foreach (var name in EntryEnumerator.Enumerate(p, ignoreEntries))
        {
            if (IsDirectory(p, name))
            {
                MoveDirectoryToRecycleBin(p, name, ignoreEntries.GetSubEntries(name));
            }
            else
            {
                MoveFileToRecycleBin(p, name);
            }
        }
        if (!RemoveDirectory(p))
        {
            // ディレクトリ削除失敗の理由が、ディレクトリが空でないためであれば、正常扱いとする(無視ディレクトリがあれば、ディレクトリが空にならないため)
            int error = Marshal.GetLastWin32Error();
            if (error != 145) // ERROR_DIR_NOT_EMPTY
            {
                throw new Win32Exception(error);
            }
        }
    }

    private static void MoveFileToRecycleBin(string directoryPath, string fileName) => RecycleBin.MoveToRecycleBin(Path.Combine(directoryPath, fileName));

}
