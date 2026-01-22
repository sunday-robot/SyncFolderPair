using SyncFolderPair.Models;
using SyncFolderPair.Types;

namespace SyncFolderPair.Services
{
    internal class DirectorySynchronizer
    {
        /// <summary>
        /// フォルダペアの初期化を行う。<br/>
        /// 具体的には管理ファイルの作成で、この管理ファイルには、フォルダペア内のすべてのファイルの相対パス、タイムスタンプ、サイズを保持するものである。
        /// 
        /// 二つのフォルダ間に差異がないことを前提としており、差異がある場合は、その旨をユーザーに報告し、管理ファイルの作成は行わない。
        /// </summary>
        /// <param name="pairName"></param>
        internal static void Initialize(string pairName)
        {
            var (leftDirectory, rightDirectory, ignoreDirectoryPathSet) = DirectoryPairs.Get(pairName);

            var syncEntries = new List<SyncEntry>();
            int errorCount = 0;

            DirectoryDifferenceScanner.Scan(leftDirectory, rightDirectory, ignoreDirectoryPathSet,
                rel =>
                {
                    Console.WriteLine($"[Left Only] {rel}");
                    errorCount++;
                    return true;
                },
                (rel, _, _) =>
                {
                    Console.WriteLine($"[Left Is Newer] {rel}");
                    errorCount++;
                    return true;
                },
                (rel, timeStamp, size) =>
                {
                    syncEntries.Add(new SyncEntry(rel, timeStamp, size));
                    return true;
                },
                (rel, _, _) =>
                {
                    Console.WriteLine($"[Right Is Newer] {rel}");
                    errorCount++;
                    return true;
                },
                rel =>
                {
                    Console.WriteLine($"[Right Only] {rel}");
                    errorCount++;
                    return true;
                },
                (rel, timeStamp, leftSize, rightSize) =>
                {
                    Console.WriteLine($"[Abnormal] {rel}, {timeStamp}, {leftSize}, {rightSize}");
                    errorCount++;
                    return true;
                });

            if (errorCount == 0)
            {
                SyncEntries.Save(pairName, syncEntries);
            }
            else
            {
                Console.WriteLine($"Synchronization initialization failed with {errorCount} errors.");
            }
        }
    }
}
