using SyncFolderPair.Models;

namespace SyncFolderPair.Services;

public static class DirectoryDifferencePrinter
{
    static readonly IReadOnlySet<string> _emptyIgnoreDirectoryPathSet = new HashSet<string>();

    public static void Check(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreDirectoryPathSet) = DirectoryPairs.Get(pairName);
        Check(leftDirectory, rightDirectory, ignoreDirectoryPathSet);
    }

    public static void Check(string leftDirectory, string rightDirectory)
    {
        Check(leftDirectory, rightDirectory, _emptyIgnoreDirectoryPathSet);
    }

    static void Check(string leftDirectory, string rightDirectory, IReadOnlySet<string> ignoreDirectoryPathSet)
    {
        DirectoryDifferenceScanner.Scan(
            leftDirectory,
            rightDirectory,
            ignoreDirectoryPathSet,
            rel =>
            {
                Console.WriteLine($"[<   ] {rel}");
                return true;
            },
            (rel, _, _) =>
            {
                Console.WriteLine($"[ << ] {rel}");
                return true;
            },
            (rel, _, _) => true,    // サイズ、タイムスタンプが同じ場合は何もしない
            (rel, _, _) =>
            {
                Console.WriteLine($"[ >> ] {rel}");
                return true;
            },
            rel =>
            {
                Console.WriteLine($"[   >] {rel}");
                return true;
            },
            (rel, timeStamp, leftSize, rightSize) =>
            {
                Console.WriteLine($"[ !!!! ] {rel}, {timeStamp}, {leftSize}, {rightSize}");
                return true;
            });
    }
}
