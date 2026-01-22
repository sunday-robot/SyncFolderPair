using SyncFolderPair.Models;

namespace SyncFolderPair.Commands;

/// <summary>
/// 設定ファイルから、フォルダペアを削除する
/// </summary>
public sealed class RemoveCommand : AbstractCommand
{
    public override string Name => "remove";
    public override string Usage => "<pair name>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Parameter count error.");

        DirectoryPairs.Remove(args[0]);

        return 0;
    }
}
