#pragma warning disable CA1416 // プラットフォームの互換性を検証

using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using Win32Api;

namespace SyncFolderPair.Utils
{
    public static class RecycleBin
    {
        /// <summary>
        /// ファイルをゴミ箱に移動させる。
        /// </summary>
        /// <param name="filePath"></param>
        public static void MoveToRecycleBin(string filePath)
        {
            var fileSize = new FileInfo(filePath).Length;
            var rootPath = Path.GetPathRoot(filePath)!;
            var recycleBinFreeSize = GetRecycleBinFreeSize(rootPath);
            if (fileSize > recycleBinFreeSize)
            {
                throw new IOException($"Not enough free space in Recycle Bin to move the file: {filePath} (file size: {fileSize}, recycle bin free size: {recycleBinFreeSize})");
            }

            MoveToRecycleBinInternal(filePath);
        }

        static long GetRecycleBinFreeSize(string rootPath)
        {
            var driveSize = new DriveInfo(rootPath).TotalSize;
            var percent = GetRecycleBinPercent(rootPath);
            var used = GetRecycleBinUsedSize(rootPath);
            var freeSize = driveSize * percent / 100 - used;
            return freeSize;
        }

        static int GetRecycleBinPercent(string rootPath)
        {
            var volumeGuid = GetVolumeGuid(rootPath);

            using var key = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\{volumeGuid}");
            if (key == null) return 10; // デフォルト10%

            return (int)key.GetValue("MaxCapacity", 10);
        }

        static string GetVolumeGuid(string rootPath)
        {
            var sb = new StringBuilder(50);

            if (!Win32.GetVolumeNameForVolumeMountPoint(rootPath, sb, sb.Capacity))
                throw new IOException("Failed to get volume GUID.");

            // 例: "\\?\Volume{f4c3b8a1-0000-0000-0000-602f00000000}\"
            string volumeName = sb.ToString();

            // 中の GUID 部分だけ抽出する
            int start = volumeName.IndexOf('{');
            int end = volumeName.IndexOf('}');
            if (start < 0 || end < 0) throw new FormatException("Invalid volume GUID format.");

            return volumeName.Substring(start, end - start + 1);
        }

        static long GetRecycleBinUsedSize(string rootPath)
        {
            var info = new Win32.SHQUERYRBINFO();
            info.cbSize = Marshal.SizeOf(info);

            int hr = Win32.SHQueryRecycleBin(rootPath, ref info);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            return info.i64Size;
        }

        static void MoveToRecycleBinInternal(string path)
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
}
