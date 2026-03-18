using System;
using System.Collections.Generic;
using System.Text;

namespace SyncFolderPair.Utils
{
    public static class FileUtils
    {
        public static long GetSize(string filePath)
        {
            var info = new FileInfo(filePath)!;
            var size = info.Length;
            return size;
        }

        /// <summary>
        /// dest側にあるファイルをゴミ箱に移動してから、source側のファイルをコピーする
        /// </summary>
        /// <param name="sourceBaseDirectory"></param>
        /// <param name="destinationBaseDirectory"></param>
        /// <param name="relativePath"></param>
        public static void ReplaceFile(string src, string dest)
        {
            // GUID を使って衝突不可能な一時ファイル名を生成し、ファイルをコピーする
            var tempPath = CreateTempFilePath(dest);
            File.Copy(src, tempPath, true);

            // destination側のファイルをゴミ箱に移動し、一時ファイルの名前を本来の名前に変える。失敗したら、上で作った一時ファイルを削除する。
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
}
