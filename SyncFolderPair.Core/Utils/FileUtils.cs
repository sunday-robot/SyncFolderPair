namespace SyncFolderPair.Core.Utils;

public static class FileUtils
{
    /// <summary>
    /// ファイルを安全にコピーする
    /// ここでは以下のことを行うことを「安全」とする。
    /// (1) コピー元のファイルが他のプロセスで使用中ではないことを確認する(使用中の場合はfalseを返す)
    /// (2) コピー先のファイルが存在していないことを確認する(呼び出し元で存在しないことが確認されていることを前提としているため、falseを返すのではなく、例外を投げる)
    /// 
    /// (1)については、実運用時に普通に発生する状況への対応。
    /// (2)については、本アプリの安全性を確保するためのもの。理論的には実運用時でも発生する状況ではあるが、事実上発生しないので、例外を投げるようにした。
    /// </summary>
    /// <param name="srcPath"></param>
    /// <param name="destPath"></param>
    /// <returns></returns>
    public static bool SafeCopy(string srcPath, string destPath)
    {
        try
        {
            // コピー元を独占読み込みモードでオープンし、コピーする
            using var srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var destStream = new FileStream(destPath, FileMode.CreateNew, FileAccess.Write);
            srcStream.CopyTo(destStream);
        }
        catch (IOException ex) when ((uint)ex.HResult == 0x80070020) // ERROR_SHARING_VIOLATION
        {
            // 他プロセスでコピー元ファイルが使用中で独占読み込みモードオープンが失敗した場合は例外を投げずにfalseを返す
            return false;
        }

        // コピー先の最終更新日時を設定する
        var lastWriteTime = File.GetLastWriteTimeUtc(srcPath);
        File.SetLastWriteTimeUtc(destPath, lastWriteTime);

        return true;
    }

    /// <summary>
    /// dest側にあるファイルをゴミ箱に移動してから、source側のファイルをコピーする
    /// </summary>
    /// <param name="sourceBaseDirectory"></param>
    /// <param name="destinationBaseDirectory"></param>
    /// <param name="relativePath"></param>
    public static bool ReplaceFile(string src, string dest)
    {
        // GUID を使って一時ファイル名を生成し、ファイルをコピーする
        var tempPath = CreateTempFilePath(dest);
        if (!SafeCopy(src, tempPath))
        {
            // コピー元のファイルが使用中だった場合は、falseを返す
            return false;
        }
         
        // destination側のファイルをゴミ箱に移動し、一時ファイルの名前を本来の名前に変える。
        try
        {
            RecycleBin.MoveToRecycleBin(dest);
            File.Move(tempPath, dest);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
        return true;
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
}
