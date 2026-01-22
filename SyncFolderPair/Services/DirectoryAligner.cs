using SyncFolderPair.Models;
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
    /// <param name="pairName"></param>
    public static void Align(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreDirectoryPathSet) = DirectoryPairs.Get(pairName);

        DirectoryDifferenceScanner.Scan(
            leftDirectory,
            rightDirectory,
            ignoreDirectoryPathSet,
            rel =>
            {
                Console.WriteLine($"[<   ] copy    {rel}");
                AddFile(leftDirectory, rightDirectory, rel);
                return true;
            },
            (rel, _, _) =>
            {
                Console.WriteLine($"[ << ] skip    {rel}");
                return true;
            },
            (rel, _, _) => true,    // サイズ、タイムスタンプが同じ場合は何もしない
            (rel, _, _) =>
            {
                Console.WriteLine($"[ >> ] skip    {rel}");
                return true;
            },
            rel =>
            {
                Console.WriteLine($"[   >] copy    {rel}");
                AddFile(rightDirectory, leftDirectory, rel);
                return true;
            },
            (rel, timeStamp, leftSize, rightSize) =>
            {
                Console.WriteLine($"[!!!!] Same timestamp but different size {rel}, {timeStamp}, {leftSize}, {rightSize}");
                return true;
            });
    }


    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプ、サイズも同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、古い方のファイルをゴミ箱に移し、新しい方のファイルをもう型のウホディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプが同じなのにサイズが異なる場合も何もしないが、異常事態であるとしてその旨をユーザーに通知する。
    /// </summary>
    /// <param name="pairName"></param>
    public static void ForceAlign(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreDirectoryPathSet) = DirectoryPairs.Get(pairName);

        DirectoryDifferenceScanner.Scan(
            leftDirectory,
            rightDirectory,
            ignoreDirectoryPathSet,
            rel =>
            {
                Console.WriteLine($"[<   ] copy    {rel}");
                AddFile(leftDirectory, rightDirectory, rel);
                return true;
            },
            (rel, _, _) =>
            {
                Console.WriteLine($"[ << ] replace {rel}");
                ReplaceFile(leftDirectory, rightDirectory, rel);
                return true;
            },
            (rel, _, _) => true,    // サイズ、タイムスタンプが同じ場合は何もしない
            (rel, _, _) =>
            {
                Console.WriteLine($"[ >> ] replace {rel}");
                ReplaceFile(rightDirectory, leftDirectory, rel);
                return true;
            },
            rel =>
            {
                Console.WriteLine($"[   >] copy    {rel}");
                AddFile(rightDirectory, leftDirectory, rel);
                return true;
            },
            (rel, timeStamp, leftSize, rightSize) =>
            {
                Console.WriteLine($"[!!!!] Same timestamp but different size {rel}, {timeStamp}, {leftSize}, {rightSize}");
                return true;
            }
            );
    }

    /// <summary>
    /// source側だけにあるファイルをdestinationにコピーする
    /// </summary>
    /// <param name="sourceBaseDirectory"></param>
    /// <param name="destinationBaseDirectory"></param>
    /// <param name="relativePath"></param>
    static void AddFile(string sourceBaseDirectory, string destinationBaseDirectory, string relativePath)
    {
        var sourcePath = Path.Combine(sourceBaseDirectory, relativePath);
        var destinationPath = Path.Combine(destinationBaseDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, false);
    }

    /// <summary>
    /// dest側にあるファイルをゴミ箱に移動してから、source側のファイルをコピーする
    /// </summary>
    /// <param name="sourceBaseDirectory"></param>
    /// <param name="destinationBaseDirectory"></param>
    /// <param name="relativePath"></param>
    static void ReplaceFile(string sourceBaseDirectory, string destinationBaseDirectory, string relativePath)
    {
        var sourcePath = Path.Combine(sourceBaseDirectory, relativePath);
        var destinationPath = Path.Combine(destinationBaseDirectory, relativePath);

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
}
