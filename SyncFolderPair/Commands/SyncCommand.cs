using SyncFolderPair.Services;
using SyncFolderPair.Types;
using System.Diagnostics;

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
                AppService.Synchronize(args[0], PrintProgress, Console.WriteLine);
                break;
            case 2:
                if (args[1] != "check")
                {
                    throw new ArgumentException("Invalid parameter.");
                }
                AppService.CheckSynchronize(args[0], PrintProgress, Console.WriteLine);
                break;
            default:
                throw new ArgumentException("Parameter count error.");
        }

        return 0;
    }

    static void PrintProgress(Operation operation, bool isTargetLeft, string path)
    {
        var s = OperationToString(operation);
        if (isTargetLeft)
            Console.WriteLine($"[<{s,-10}] {path}");
        else
            Console.WriteLine($"[{s,10}>] {path}");
    }

    static string OperationToString(Operation operation)
    {
        return operation switch
        {
            Operation.CreateDirectory => "CREATE",
            Operation.DeleteDirectory => "DELDIR",
            Operation.DeleteFile => "DELFIL",
            Operation.CopyFile => "COPY",
            Operation.OverwriteFile => "OVRWRT",
            _ => throw new UnreachableException()
        };
    }
}
