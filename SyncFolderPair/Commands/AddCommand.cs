using SyncFolderPair.Models;

namespace SyncFolderPair.Commands;

/// <summary>
/// フォルダーペアを設定ファイルに追加する
/// </summary>
public sealed class AddCommand : AbstractCommand
{
    public override string Name => "add";
    public override string Usage => "<pair name> <left folder> <right folder>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Parameter count error.");

        DirectoryPairs.Add(args[0], args[1], args[2]);

        return 0;
    }
}
