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



    }
}
