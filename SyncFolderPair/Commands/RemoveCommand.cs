using SyncFolderPair.Models;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// 設定ファイルから、フォルダペアを削除する
    /// </summary>
    public static class RemoveCommand
    {
        public static void Run(string pairName)
        {
            DirectoryPairs.Remove(pairName);
        }
    }
}
