using SyncFolderPair.Core;

namespace SyncFolderPair.Commands;

/// <summary>
/// フォルダペアのフォルダ内容を同期させる
/// </summary>
public sealed class SyncCommand : AbstractCommand
{
    public override string Name => "sync";
    public override string Usage => "<pair name> [check]";

    public override int Run(Span<string> args)
    {
        switch (args.Length)
        {
            case 1:
                Core.Core.Synchronize(args[0], ProgressPrinter.Print, Console.WriteLine);
                break;
            case 2:
                if (args[1] != "check")
                    throw new ArgumentException("Invalid parameter.");
                Core.Core.CheckSynchronize(args[0], ProgressPrinter.Print, Console.WriteLine);
                break;
            default:
                throw new ArgumentException("Parameter count error.");
        }

        return 0;
    }
}
