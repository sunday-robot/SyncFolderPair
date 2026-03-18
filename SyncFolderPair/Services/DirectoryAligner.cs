using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using Win32Api;

namespace SyncFolderPair.Services;

public static class DirectoryAligner
{
    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプ、サイズも同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、コピーなどはしないが、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプが同じなのにサイズが異なる場合も何もしないが、異常事態であるとしてその旨をユーザーに通知する。
    /// </summary>
    public static void Align(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        AlignCore(
            leftDirectoryPath,
            rightDirectoryPath,
            "",
            ignoreEntries,
            CreateDirectory,
            (leftToRight, leftBaseDir, rightBaseDir, path) =>
            {
                CopyFile(leftToRight, leftBaseDir, rightBaseDir, path);
                return true;
            },
            (leftToRight, _, _, path) =>
            {
                PrintSkipFile(leftToRight, path);
                return true;
            });
    }

    public static void CheckAlign(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        AlignCore(
            leftDirectoryPath,
            rightDirectoryPath,
            "",
            ignoreEntries,
            (isLeft, _, path) =>
            {
                PrintCreateDirectory(isLeft, path);
            },
            (leftToRight, _, _, path) =>
            {
                PrintCopyFile(leftToRight, path);
                return true;
            },
            (leftToRight, _, _, path) =>
            {
                PrintSkipFile(leftToRight, path);
                return true;
            });
    }

    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプ、サイズも同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、古い方のファイルをゴミ箱に移し、新しい方のファイルをもう型のウホディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプが同じなのにサイズが異なる場合も何もしないが、異常事態であるとしてその旨をユーザーに通知する。
    /// </summary>
    public static void ForceAlign(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        AlignCore(
            leftDirectoryPath,
            rightDirectoryPath,
            "",
            ignoreEntries,
            CreateDirectory,
            (leftToRight, leftBaseDir, rightBaseDir, path) =>  // ファイルをコピーする(上書きコピーではない)
            {
                CopyFile(leftToRight, leftBaseDir, rightBaseDir, path);
                return true;
            },
            (leftToRight, leftBaseDir, rightBaseDir, path) =>  // ファイルを上書きコピーする
            {
                ReplaceFile(leftToRight, leftBaseDir, rightBaseDir, path);
                return true;
            });
    }

    static bool AlignCore(
        string leftBaseDir,
        string rightBaseDir,
        string path,
        IgnoreEntries ignoreEntries,
        Action<bool, string, string> createDirectory,
        Func<bool, string, string, string, bool> copyFile,
        Func<bool, string, string, string, bool> overwriteFile)
    {
        var leftDirectoryPath = Path.Combine(leftBaseDir, path);
        var rightDirectoryPath = Path.Combine(rightBaseDir, path);

        foreach (var e in EntryEnumerator.Enumerate(leftDirectoryPath, rightDirectoryPath, ignoreEntries))
        {
            var name = e.Item1;
            var p = Path.Combine(path, name);
            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    // 左にしかない
                    if (IsDirectory(leftDirectoryPath, name))
                    {
                        if (!CopyDirectory(true, leftBaseDir, rightBaseDir, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                            return false;
                    }
                    else
                    {
                        if (!copyFile(true, leftBaseDir, rightBaseDir, p))
                            return false;
                    }
                    break;
                case PairEnumerator.Existance.Both:
                    // 左右両方にある
                    if (IsDirectory(leftDirectoryPath, name))
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左右両方ともディレクトリである
                            if (!AlignCore(leftBaseDir, rightBaseDir, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile, overwriteFile))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            // 左はディレクトリ、右はファイルである
                            Console.WriteLine($"[!!!!] Left is directory {p}");
                        }
                    }
                    else
                    {
                        if (IsDirectory(rightDirectoryPath, name))
                        {
                            // 左はファイル、右はディレクトリである
                            Console.WriteLine($"[!!!!] Right is directory {p}");
                        }
                        else
                        {
                            // 左右どちらもファイルである
                            var leftUpdateTime = GetLastWriteTimeUtc(leftDirectoryPath, name);
                            var rightUpdateTime = GetLastWriteTimeUtc(rightDirectoryPath, name);
                            if (leftUpdateTime > rightUpdateTime)
                            {
                                if (!overwriteFile(true, leftDirectoryPath, rightDirectoryPath, p))
                                    return false;
                            }
                            else if (leftUpdateTime < rightUpdateTime)
                            {
                                if (!overwriteFile(false, leftDirectoryPath, rightDirectoryPath, p))
                                    return false;
                            }
                        }
                    }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    // 右にしかない
                    if (IsDirectory(rightDirectoryPath, name))
                    {
                        if (!CopyDirectory(false, rightBaseDir, leftBaseDir, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                            return false;
                    }
                    else
                    {
                        if (!copyFile(false, leftBaseDir, rightBaseDir, p))
                            return false;
                    }
                    break;
            }
        }

        return true;
    }

    static bool CopyDirectory(
        bool leftToRight,
        string sourceBaseDirectoryPath,
        string destinationBaseDirectoryPath,
        string relativePath,
        IgnoreEntries ignoreEntries,
        Action<bool, string, string> createDirectory,
        Func<bool, string, string, string, bool> copyFile)
    {
        var sourceDirectoryPath = Path.Combine(sourceBaseDirectoryPath, Path.Combine(relativePath));

        createDirectory(!leftToRight, destinationBaseDirectoryPath, relativePath);
        foreach (var name in EntryEnumerator.Enumerate(sourceDirectoryPath, ignoreEntries))
        {
            var p = Path.Combine(relativePath, name);
            if (IsDirectory(sourceDirectoryPath, name))
            {
                if (!CopyDirectory(leftToRight, sourceBaseDirectoryPath, destinationBaseDirectoryPath, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                    return false;
            }
            else
            {
                if (!copyFile(leftToRight, sourceBaseDirectoryPath, destinationBaseDirectoryPath, p))
                    return false;
            }
        }
        return true;
    }

    static void PrintCreateDirectory(bool isLeft, string path)
    {
        if (isLeft)
            Console.WriteLine($"[<     MKDIR] {path}");
        else
            Console.WriteLine($"[MKDIR     >] {path}");
    }

    static void PrintCopyFile(bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[COPY      >] {path}");
        else
            Console.WriteLine($"[<      COPY] {path}");
    }

    static void PrintSkipFile(bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[SKIP      >] {path}");
        else
            Console.WriteLine($"[<      SKIP] {path}");
    }

    static void PrintOverwriteFile(bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[OVERWRITE >] {path}");
        else
            Console.WriteLine($"[< OVERWRITE] {path}");
    }

    static void CreateDirectory(bool isLeft, string baseDir, string path)
    {
        PrintCreateDirectory(isLeft, path);

        var p = Path.Combine(baseDir, path);
        Directory.CreateDirectory(p);
    }

    static void CopyFile(bool leftToRight, string leftBase, string rightBase, string path)
    {
        PrintCopyFile(leftToRight, path);

        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);
        if (leftToRight)
            File.Copy(left, right, false);
        else
            File.Copy(right, left, false);
    }

    static void ReplaceFile(bool leftToRight, string leftBaseDirectory, string rightBaseDirectory, string path)
    {
        PrintOverwriteFile(leftToRight, path);

        string src;
        string dest;
        if (leftToRight)
        {
            src = Path.Combine(leftBaseDirectory, path);
            dest = Path.Combine(rightBaseDirectory, path);
        }
        else
        {
            src = Path.Combine(rightBaseDirectory, path);
            dest = Path.Combine(leftBaseDirectory, path);
        }

        FileUtils.ReplaceFile(src, dest);
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    private static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));
}
