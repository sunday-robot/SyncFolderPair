using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.Diagnostics;

namespace SyncFolderPair.Services;

public static class SyncEntryInitializer
{
    /// <summary>
    /// フォルダペアの初期化を行う。<br/>
    /// 具体的には管理ファイルの作成で、この管理ファイルには、フォルダペア内のすべてのファイルの相対パス、タイムスタンプ、サイズを保持するものである。
    /// 
    /// 二つのフォルダ間に差異がないことを前提としており、差異がある場合は、その旨をユーザーに報告し、管理ファイルの作成は行わない。
    /// </summary>
    /// <param name="pairName"></param>
    public static SyncEntries Initialize(string leftDirectory, string rightDirectory, IgnoreEntries ignoreEntries)
    {
        var syncEntries = CreateSyncEntries(leftDirectory, rightDirectory, ignoreEntries)
            ?? throw new Exception("Synchronization initialization failed due to directory differences.");
        return syncEntries;
    }

    static SyncEntries? CreateSyncEntries(string leftDir, string rightDir, IgnoreEntries ignoreEntries)
    {
        var directorySyncEntry = new SyncEntries();
        var errorOccurred = false;

        foreach (var e in EntryEnumerator.Enumerate(leftDir, rightDir, ignoreEntries))
        {
            var name = e.Item1;
            if (e.Item2 is not PairEnumerator.Existance.Both)
            {
                // エラー。片方にしかない
                if (e.Item2 is PairEnumerator.Existance.OnlyLeft)
                    Console.WriteLine($"Error: Left only : {Path.Combine(leftDir, name)}");
                else
                    Console.WriteLine($"Error: right only : {Path.Combine(rightDir, name)}");
                errorOccurred = true;
                continue;
            }
            // どちらにもある
            var leftPath = Path.Combine(leftDir, name);
            var rightPath = Path.Combine(rightDir, name);
            if (Directory.Exists(leftPath))
            {
                if (!Directory.Exists(rightPath))
                {
                    // エラー。左がディレクトリ、右がファイル
                    Console.WriteLine($"Error: left is directory, right is file : {leftPath} , {rightPath}");
                    errorOccurred = true;
                    continue;
                }

                // どちらもディレクトリ
                var childEntries = CreateSyncEntries(leftPath, rightPath, ignoreEntries.GetSubEntries(name));
                if (childEntries == null)
                {
                    errorOccurred = true;
                    continue;
                }
                directorySyncEntry.Nodes[name] = childEntries;
            }
            else
            {
                if (Directory.Exists(rightPath))
                {
                    // エラー。右がディレクトリ、左がファイル
                    Console.WriteLine($"Error: left is file, right is directory : {leftPath} , {rightPath}");
                    errorOccurred = true;
                    continue;
                }

                // どちらもファイル
                var r = FileComparator.Compare(leftPath, rightPath);
                if (r is not FileCompareResult.Same same)
                {
                    // エラー。異なるファイル(更新日時が異なる、またはサイズが異なる)
                    switch (r)
                    {
                        case FileCompareResult.LeftIsNewer a:
                            Console.WriteLine($"Error: Left is newer : {leftPath} , {rightPath} (left: {a.Left}, right: {a.Right})");
                            break;
                        case FileCompareResult.RightIsNewer a:
                            Console.WriteLine($"Right is newer : {leftPath} , {rightPath} (left: {a.Left}, right: {a.Right})");
                            break;
                        case FileCompareResult.InconsistentSize a:
                            Console.WriteLine($"Inconsistent size : {leftPath} , {rightPath} (last write time: {a.LeftLastWriteTimeUtc}, left size: {a.Left}, right size: {a.Right})");
                            break;
                        default:
                            throw new UnreachableException();
                    }
                    errorOccurred = true;
                    continue;
                }
                directorySyncEntry.Nodes[name] = new SyncEntriesLeaf(same.LastWriteTimeUtc, same.Length);
            }
        }
        if (errorOccurred)
        {
            return null;
        }
        return directorySyncEntry;
    }
}
