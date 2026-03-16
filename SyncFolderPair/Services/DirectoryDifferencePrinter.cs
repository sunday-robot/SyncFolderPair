using SyncFolderPair.Utils;

namespace SyncFolderPair.Services;

public static class DirectoryDifferencePrinter
{
    public static void Print(string leftDirectoryPath, string rightDirectoryPath)
    {
        Print(
            leftDirectoryPath,
            rightDirectoryPath,
            "",
            rel =>
            {
                Console.WriteLine($"[<   ] {rel}");
            },
            (rel, _, _) =>
            {
                Console.WriteLine($"[ << ] {rel}");
            },
            (rel, _, _) => { },    // サイズ、タイムスタンプが同じ場合は何もしない
            (rel, _, _) =>
            {
                Console.WriteLine($"[ >> ] {rel}");
            },
            rel =>
            {
                Console.WriteLine($"[   >] {rel}");
            },
            rel =>
            {
                Console.WriteLine($"[D  F] {rel}");
            },
            rel =>
            {
                Console.WriteLine($"[F  D] {rel}");
            },
            (rel, timeStamp, leftSize, rightSize) =>
            {
                Console.WriteLine($"[ !!!! ] {rel}, {timeStamp}, {leftSize}, {rightSize}");
            });
    }

    static void Print(
        string leftBaseDir,
        string rightBaseDir,
        string relativePath,
        Action<string> leftOnly,
        Action<string, DateTime, DateTime> leftIsNewer,
        Action<string, DateTime, long> same,
        Action<string, DateTime, DateTime> rightIsNewer,
        Action<string> rightOnly,
        Action<string> leftIsDirectory,
        Action<string> rightIsDirectory,
        Action<string, DateTime, long, long> abnormal)
    {
        var leftDirectoryPath = Path.Combine(leftBaseDir, relativePath);
        var rightDirectoryPath = Path.Combine(rightBaseDir, relativePath);

        foreach (var e in EntryEnumerator.Enumerate(leftDirectoryPath, rightDirectoryPath))
        {
            var name = e.Item1;
            var p = Path.Combine(relativePath, name);
            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    // 左にしかない
                    if (IsDirectory(leftDirectoryPath, name))
                    {
                        PrintDirectory(leftBaseDir, p, leftOnly);
                    }
                    else
                    {
                        leftOnly(p);
                    }
                    break;
                case PairEnumerator.Existance.Both:
                    // 左右両方にある
                    if (IsDirectory(leftDirectoryPath, name))
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左右両方ともディレクトリである
                            Print(leftBaseDir, rightBaseDir, p, leftOnly, leftIsNewer, same, rightIsNewer, rightOnly, leftIsDirectory, rightIsDirectory, abnormal);
                        }
                        else
                        {
                            // 左はディレクトリ、右はファイルである
                            leftIsDirectory(p);
                        }
                    }
                    else
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左はファイル、右はディレクトリである
                            rightIsDirectory(p);
                        }
                        else
                        {
                            // 左右どちらもファイルである
                            var leftUpdateTime = GetLastWriteTimeUtc(leftDirectoryPath, name);
                            var rightUpdateTime = GetLastWriteTimeUtc(rightDirectoryPath, name);
                            if (leftUpdateTime > rightUpdateTime)
                            {
                                leftIsNewer(p, leftUpdateTime, rightUpdateTime);
                            }
                            else if (leftUpdateTime < rightUpdateTime)
                            {
                                rightIsNewer(p, leftUpdateTime, rightUpdateTime);
                            }
                            else
                            {
                                var size = GetFileSize(leftDirectoryPath, name);
                                same(p, leftUpdateTime, size);
                            }
                        }
                    }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    // 右にしかない
                    if (IsDirectory(rightDirectoryPath, name))
                    {
                        PrintDirectory(rightBaseDir, p, rightOnly);
                    }
                    else
                    {
                        rightOnly(p);
                    }
                    break;
            }
        }
    }

    static void PrintDirectory(
        string baseDirectoryPath,
        string relativePath,
        Action<string> print)
    {
        var directoryPath = Path.Combine(baseDirectoryPath, relativePath);
        foreach (var name in EntryEnumerator.Enumerate(directoryPath))
        {
            var p = Path.Combine(relativePath, name);
            if (IsDirectory(directoryPath, name))
            {
                PrintDirectory(baseDirectoryPath, p, print);
            }
            else
            {
                print(p);
            }
        }
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));

    static long GetFileSize(string filePath)
    {
        var info = new FileInfo(filePath)!;
        var size = info.Length;
        return size;
    }

    static long GetFileSize(string directoryPath, string fileName) => GetFileSize(Path.Combine(directoryPath, fileName));
}
