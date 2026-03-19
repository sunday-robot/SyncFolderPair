using SyncFolderPair.Types;
using SyncFolderPair.Utils;

namespace SyncFolderPair.Services;

public static class DirectoryAligner
{
    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプが同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、コピーなどはしないが、その旨をユーザーに通知する。
    /// </summary>
    public static void Align(string leftBase, string rightBase, IgnoreEntries ignoreEntries)
    {
        AlignDirectory(CreateDirectory, CopyFile, PrintSkipFile,
            leftBase, rightBase, "", ignoreEntries);
    }

    public static void CheckAlign(string leftBase, string rightBase, IgnoreEntries ignoreEntries)
    {
        AlignDirectory(PrintCreateDirectory, PrintCopyFile, PrintSkipFile,
            leftBase, rightBase, "", ignoreEntries);
    }

    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプがも同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、古い方のファイルをゴミ箱に移し、新しい方のファイルをもう型のウホディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// </summary>
    public static void ForceAlign(string leftBase, string rightBase, IgnoreEntries ignoreEntries)
    {
        AlignDirectory(CreateDirectory, CopyFile, ReplaceFile,
            leftBase, rightBase, "", ignoreEntries);
    }

    static void AlignDirectory(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string, string> copyFile,
        Action<bool, string, string, string> overwriteFile,
        string leftBase,
        string rightBase,
        string path,
        IgnoreEntries ignoreEntries)
    {
        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);

        foreach (var e in EntryEnumerator.Enumerate(left, right, ignoreEntries))
        {
            var name = e.Item1;
            var p = Path.Combine(path, name);
            switch (e.Item2)
            {
                case PairEnumerator.Existance.OnlyLeft:
                    if (IsDirectory(left, name))
                        CopyDirectory(createDirectory, copyFile,    // 左にだけディレクトリがある
                            true, leftBase, rightBase, p, ignoreEntries.GetSubEntries(name));
                    else
                        copyFile(true, leftBase, rightBase, p); // 左にだけファイルがある
                    break;
                case PairEnumerator.Existance.Both:
                    // 左右両方にある
                    if (IsDirectory(left, name))
                        if (IsDirectory(right, name))
                            AlignDirectory(createDirectory, copyFile, overwriteFile,    // 両方ともディレクトリである
                                leftBase, rightBase, p, ignoreEntries.GetSubEntries(name));
                        else
                            Console.WriteLine($"[!!!!] Left is directory {p}"); // 左はディレクトリ、右はファイルである
                    else
                        if (IsDirectory(right, name))
                            Console.WriteLine($"[!!!!] Right is directory {p}");    // 左はファイル、右はディレクトリである
                        else
                        {
                            var leftUpdateTime = GetLastWriteTimeUtc(left, name);
                            var rightUpdateTime = GetLastWriteTimeUtc(right, name);
                            if (leftUpdateTime != rightUpdateTime)
                                overwriteFile(leftUpdateTime > rightUpdateTime, leftBase, rightBase, p);    // 新しいファイルで古いほうのファイルを更新する
                            // 更新日時が同じファイルに対しては何もしない
                        }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    if (IsDirectory(right, name))
                        CopyDirectory(createDirectory, copyFile,    // 右にだけディレクトリがある
                             false, rightBase, leftBase, p, ignoreEntries.GetSubEntries(name));
                    else
                        copyFile(false, leftBase, rightBase, p);    // 右にだけファイルがある
                    break;
            }
        }
    }

    static void CopyDirectory(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string, string> copyFile,
        bool leftToRight,
        string srcBase,
        string destBase,
        string path,
        IgnoreEntries ignoreEntries)
    {
        var src = Path.Combine(srcBase, Path.Combine(path));
        createDirectory(!leftToRight, destBase, path);
        foreach (var name in EntryEnumerator.Enumerate(src, ignoreEntries))
        {
            var p = Path.Combine(path, name);
            if (IsDirectory(src, name))
                CopyDirectory(createDirectory, copyFile,
                    leftToRight, srcBase, destBase, p, ignoreEntries.GetSubEntries(name));
            else
                copyFile(leftToRight, srcBase, destBase, p);
        }
    }

    static void PrintCreateDirectory(bool isLeft, string _, string path)
    {
        if (isLeft)
            Console.WriteLine($"[<     MKDIR] {path}");
        else
            Console.WriteLine($"[MKDIR     >] {path}");
    }

    static void PrintCopyFile(bool leftToRight, string _, string __, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[COPY      >] {path}");
        else
            Console.WriteLine($"[<      COPY] {path}");
    }

    static void PrintSkipFile(bool leftToRight, string _, string __, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[SKIP      >] {path}");
        else
            Console.WriteLine($"[<      SKIP] {path}");
    }

    static void CreateDirectory(bool isLeft, string baseDir, string path)
    {
        PrintCreateDirectory(isLeft, "", path);
        var p = Path.Combine(baseDir, path);
        Directory.CreateDirectory(p);
    }

    static void CopyFile(bool leftToRight, string leftBase, string rightBase, string path)
    {
        PrintCopyFile(leftToRight, "", "", path);
        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);
        if (leftToRight)
            File.Copy(left, right, false);
        else
            File.Copy(right, left, false);
    }

    static void ReplaceFile(bool leftToRight, string leftBase, string rightBase, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[OVERWRITE >] {path}");
        else
            Console.WriteLine($"[< OVERWRITE] {path}");

        var (srcBase, destBase) = leftToRight ? (leftBase, rightBase) : (rightBase, leftBase);
        var src = Path.Combine(srcBase, path);
        var dest = Path.Combine(destBase, path);
        FileUtils.ReplaceFile(src, dest);
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    private static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));
}
