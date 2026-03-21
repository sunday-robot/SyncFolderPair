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
                            switch (FileComparator.Compare(Path.Combine(left, name), Path.Combine(right, name)))
                            {
                                case FileCompareResult.LeftIsNewer:
                                    overwriteFile(true, leftBase, rightBase, p);    // 左の新しいファイルで右のものを上書きする
                                    break;
                                case FileCompareResult.RightIsNewer:
                                    overwriteFile(false, rightBase, leftBase, p);   // 右の新しいファイルで左のものを上書きする
                                    break;
                                default:
                                    break;  // 更新日時が同じファイルに対しては何もしない
                            }
                        }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    if (IsDirectory(right, name))
                        CopyDirectory(createDirectory, copyFile,    // 右にだけディレクトリがある
                             false, rightBase, leftBase, p, ignoreEntries.GetSubEntries(name));
                    else
                        copyFile(false, rightBase, leftBase, p);    // 右にだけファイルがある
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

    static void PrintMessage(string operation, bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[{operation,10}>] {path}");
        else
            Console.WriteLine($"[<{operation,-10}] {path}");
    }

    static void PrintCreateDirectory(bool isLeft, string _, string path) => PrintMessage("MKDIR", !isLeft, path);

    static void PrintCopyFile(bool leftToRight, string _, string __, string path) => PrintMessage("COPY", leftToRight, path);

    static void PrintSkipFile(bool leftToRight, string _, string __, string path) => PrintMessage("SKIP", leftToRight, path);

    static void CreateDirectory(bool isLeft, string baseDir, string path)
    {
        PrintCreateDirectory(isLeft, "", path);
        var p = Path.Combine(baseDir, path);
        Directory.CreateDirectory(p);
    }

    static void CopyFile(bool leftToRight, string srcBase, string destBase, string path)
    {
        PrintCopyFile(leftToRight, "", "", path);
        File.Copy(Path.Combine(srcBase, path), Path.Combine(destBase, path), false);
    }

    static void ReplaceFile(bool leftToRight, string srcBase, string destBase, string path)
    {
        PrintMessage("OVERWRITE", leftToRight, path);
        FileUtils.ReplaceFile(Path.Combine(srcBase, path), Path.Combine(destBase, path));
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));
}
