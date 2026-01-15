using SyncFolderPair.Models;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// 設定ファイルに設定されているフォルダぺのリストを出力する
    /// </summary>
    public static class ListCommand
    {
        public static void Run()
        {
            var pairs = DirectoryPairs.Get();
            foreach (var p in pairs)
            {
                Console.WriteLine($"{p.Name}:");
                Console.WriteLine($"  {p.LeftDirectory}");
                Console.WriteLine($"  {p.RightDirectory}");
                Console.WriteLine();
            }
        }
    }
}
