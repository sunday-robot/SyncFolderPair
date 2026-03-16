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
            (baseDir, path) =>  // ディレクトリを作成する
            {
                CreateDirectory(baseDir, path);
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルをコピーする(上書きコピーではない)
            {
                CopyFile(srcBaseDir, destBaseDir, path);
                return true;
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルを上書きコピーする
            {
                // 何もしない
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
            (baseDir, path) =>  // ディレクトリを作成する
            {
                // 何もしない
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルをコピーする(上書きコピーではない)
            {
                // 何もしない
                return true;
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルを上書きコピーする
            {
                // 何もしない
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
            (baseDir, path) =>  // ディレクトリを作成する
            {
                CreateDirectory(baseDir, path);
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルをコピーする(上書きコピーではない)
            {
                CopyFile(srcBaseDir, destBaseDir, path);
                return true;
            },
            (srcBaseDir, destBaseDir, path) =>  // ファイルを上書きコピーする
            {
                ReplaceFile(srcBaseDir, destBaseDir, path);
                return true;
            });
    }

    static bool AlignCore(
        string leftBaseDir,
        string rightBaseDir,
        string path,
        IgnoreEntries ignoreEntries,
        Action<string, string> createDirectory,
        Func<string, string, string, bool> copyFile,
        Func<string, string, string, bool> overwriteFile)
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
                        if (!CopyDirectory(leftBaseDir, rightBaseDir, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                            return false;
                    }
                    else
                    {
                        Console.WriteLine($"[<   ] created    {p}");
                        if (!copyFile(leftBaseDir, rightBaseDir, p))
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
                                Console.WriteLine($"[<   ] modified    {p}, {leftUpdateTime}, {rightUpdateTime}");

                                if (!overwriteFile(leftDirectoryPath, rightDirectoryPath, p))
                                    return false;
                            }
                            else if (leftUpdateTime < rightUpdateTime)
                            {
                                Console.WriteLine($"[   >] modified    {p}, {leftUpdateTime}, {rightUpdateTime}");

                                if (!overwriteFile(rightDirectoryPath, leftDirectoryPath, p))
                                    return false;
                            }
                        }
                    }
                    break;
                case PairEnumerator.Existance.OnlyRight:
                    // 右にしかない
                    if (IsDirectory(rightDirectoryPath, name))
                    {
                        if (!CopyDirectory(rightBaseDir, leftBaseDir, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                            return false;
                    }
                    else
                    {
                        Console.WriteLine($"[   >] created    {p}");
                        if (!copyFile(rightBaseDir, leftBaseDir, p))
                            return false;
                    }
                    break;
            }
        }

        return true;
    }

    static bool CopyDirectory(
        string sourceBaseDirectoryPath,
        string destinationBaseDirectoryPath,
        string relativePath,
        IgnoreEntries ignoreEntries,
        Action<string, string> createDirectory,
        Func<string, string, string, bool> copyFile)
    {
        var sourceDirectoryPath = Path.Combine(sourceBaseDirectoryPath, Path.Combine(relativePath));

        createDirectory(destinationBaseDirectoryPath, relativePath);
        foreach (var name in EntryEnumerator.Enumerate(sourceDirectoryPath, ignoreEntries))
        {
            var p = Path.Combine(relativePath, name);
            if (IsDirectory(sourceDirectoryPath, name))
            {
                if (!CopyDirectory(sourceBaseDirectoryPath, destinationBaseDirectoryPath, p, ignoreEntries.GetSubEntries(name), createDirectory, copyFile))
                    return false;
            }
            else
            {
                if (!copyFile(sourceBaseDirectoryPath, destinationBaseDirectoryPath, p))
                    return false;
            }
        }
        return true;
    }

    static void CreateDirectory(string baseDir, string path)
    {
        var p = Path.Combine(baseDir, path);
        Directory.CreateDirectory(p);
    }

    static void CopyFile(string srcBase, string destBase, string path)
    {
        var src = Path.Combine(srcBase, path);
        var dest = Path.Combine(destBase, path);
        File.Copy(src, dest, false);
    }

    /// <summary>
    /// dest側にあるファイルをゴミ箱に移動してから、source側のファイルをコピーする
    /// </summary>
    /// <param name="sourceBaseDirectory"></param>
    /// <param name="destinationBaseDirectory"></param>
    /// <param name="relativePath"></param>
    static void ReplaceFile(string sourceBaseDirectory, string destinationBaseDirectory, string path)
    {
        var sourcePath = Path.Combine(sourceBaseDirectory, path);
        var destinationPath = Path.Combine(destinationBaseDirectory, path);

        // GUID を使って衝突不可能な一時ファイル名を生成し、ファイルをコピーする
        var tempPath = CreateTempFilePath(destinationPath);
        File.Copy(sourcePath, tempPath, true);

        // destination側のファイルをゴミ箱に移動し、一時ファイルの名前を本来の名前に変える。失敗したら、上で作った一時ファイルを削除する。
        try
        {
            MoveToRecycleBin(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// GUIDを使用して一時ファイルの名前を作る。
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    static string CreateTempFilePath(string path)
    {
        var dir = Path.GetDirectoryName(path)!;

        while (true)
        {
            var guid = Guid.NewGuid().ToString("N");
            var tempName = guid + ".tmp";
            var tempPath = Path.Combine(dir, tempName);

            // 既に存在しないことを確認する
            // パス長制限に引っかかる場合はここで例外が出る
            if (!File.Exists(tempPath) && !Directory.Exists(tempPath))
                return tempPath;
        }
    }

    /// <summary>
    /// ファイルをゴミ箱に移動させる。
    /// </summary>
    /// <param name="path"></param>
    static void MoveToRecycleBin(string path)
    {
        var op = new Win32.SHFILEOPSTRUCT
        {
            wFunc = Win32.FO_DELETE,
            pFrom = path + "\0",    // pFormには、複数のパス名をセットすることができる。空文字列がパス名リストの終端を示すルールになっているので、"\0"を追加する必要がある。
            fFlags = Win32.FOF_ALLOWUNDO |
                     Win32.FOF_NOCONFIRMATION |
                     Win32.FOF_SILENT
        };
        var result = Win32.SHFileOperation(ref op);
        if (result != 0)
            throw new IOException($"Failed to move to Recycle Bin: {path} (SHFileOperation returned {result})");
        if (op.fAnyOperationsAborted != 0)
            throw new IOException($"Recycle Bin operation was aborted: {path}");
    }

    static bool IsDirectory(string directoryPath, string entryName) => Directory.Exists(Path.Combine(directoryPath, entryName));

    private static DateTime GetLastWriteTimeUtc(string directoryPath, string fileName) => File.GetLastWriteTimeUtc(Path.Combine(directoryPath, fileName));
}
