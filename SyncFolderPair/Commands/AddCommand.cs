using SyncFolderPair.Models;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// フォルダーペアを設定ファイルに追加する
    /// </summary>
    public static class AddCommand
    {
        public static void Run(Span<string> args)
        {
            if (args.Length != 3)
                throw new ArgumentException("Parameter count error.");
            DirectoryPairs.Add(args[0], args[1], args[2]);
        }
    }
}
