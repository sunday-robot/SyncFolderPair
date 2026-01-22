using SyncFolderPair.Services;

namespace SyncFolderPair.Commands;

/// <summary>
/// 二つのフォルダの差異を出力する
/// </summary>
public sealed class DiffCommand : AbstractCommand
{
    public override string Name => "diff";
    public override string Usage => "<left folder> <right folder>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Parameter count error.");

        DirectoryDifferencePrinter.Check(args[0], args[1]);

        return 0;
    }
}
