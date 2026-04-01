using SyncFolderPair.Core;

namespace SyncFolderPair.Commands;

/// <summary>
/// 設定ファイルから、フォルダペアを削除する
/// </summary>
public sealed class DeleteCommand : AbstractCommand
{
    public override string Name => "delete";
    public override string Usage => "<pair name>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Parameter count error.");

        Core.Core.DeleteDirectoryPair(args[0]);

        return 0;
    }
}
