using SyncFolderPair.Services;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// 二つのフォルダの差異を出力する
    /// </summary>
    public static class DiffCommand
    {
        public static void Run(Span<string> args)
        {
            if (args.Length != 2)
                throw new ArgumentException("Parameter count error.");
            DirectoryDifferencePrinter.Check(args[0], args[1]);
        }
    }
}
