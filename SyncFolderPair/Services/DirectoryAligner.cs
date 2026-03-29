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
            leftBase, rightBase, ignoreEntries);
    }

    /// <summary>
    /// 上のメソッドのファイルコピーなどを行わない版
    /// </summary>
    public static void CheckAlign(string leftBase, string rightBase, IgnoreEntries ignoreEntries)
    {
        AlignDirectory(PrintCreateDirectory, PrintCopyFile, PrintSkipFile,
            leftBase, rightBase, ignoreEntries);
    }

    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプがも同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、古い方のファイルをゴミ箱に移し、新しい方のファイルをもう型のウホディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// </summary>
    public static void ForceAlign(string leftBase, string rightBase, IgnoreEntries ignoreEntries)
    {
        AlignDirectory(CreateDirectory, CopyFile, ReplaceFile,
            leftBase, rightBase, ignoreEntries);
    }

    static void AlignDirectory(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string, string> copyFile,
        Action<bool, string, string, string> overwriteFile,
        string leftBase,
        string rightBase,
        IgnoreEntries ignoreEntries)
    {
        var entryPairs = EntryPairs.Enumerate(leftBase, rightBase, path => File.GetLastWriteTimeUtc(path), ignoreEntries);
        AlignDirectory(createDirectory, copyFile, overwriteFile, leftBase, rightBase, "", entryPairs);
    }

    static void AlignDirectory(
        Action<bool, string, string> createDirectory,
        Action<bool, string, string, string> copyFile,
        Action<bool, string, string, string> overwriteFile,
        string leftBase,
        string rightBase,
        string path,
        IEnumerable<EntryPair> entryPairs)
    {
        var left = Path.Combine(leftBase, path);
        var right = Path.Combine(rightBase, path);
        foreach (var e in entryPairs)
        {
            var p = Path.Combine(path, e.Name);
            switch (e)
            {
                case EntryPair.DirFile:
                    Console.WriteLine($"[!!!!] Left is directory, but right is file. {p}");
                    break;
                case EntryPair.FileDir:
                    Console.WriteLine($"[!!!!] Right is directory, but right is directory. {p}");
                    break;

                case EntryPair.DirNone x:
                    createDirectory(false, rightBase, path);
                    AlignDirectory(createDirectory, copyFile, overwriteFile,
                        leftBase, rightBase, p, x.ChildrenEnumerable);
                    break;
                case EntryPair.NoneDir x:
                    createDirectory(true, leftBase, path);
                    AlignDirectory(createDirectory, copyFile, overwriteFile,
                        leftBase, rightBase, p, x.ChildrenEnumerable);
                    break;

                case EntryPair.FileNone:
                    copyFile(true, leftBase, rightBase, p);
                    break;
                case EntryPair.NoneFile:
                    copyFile(false, rightBase, leftBase, p);
                    break;

                case EntryPair.DirDir x:
                    AlignDirectory(createDirectory, copyFile, overwriteFile,
                        leftBase, rightBase, p, x.ChildrenEnumerable);
                    break;

                case EntryPair.FileFile x:
                    var lt = (DateTime)x.Left!;
                    var rt = (DateTime)x.Right!;
                    if (lt > rt)
                        overwriteFile(true, leftBase, rightBase, p);    // 左の新しいファイルで右のものを上書きする
                    else if (lt < rt)
                        overwriteFile(false, rightBase, leftBase, p);   // 右の新しいファイルで左のものを上書きする
                    // 更新日時が同じファイルに対しては何もしない
                    break;
            }
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
}
