using SyncFolderPair.Services;

namespace SyncFolderPair.Commands;

/// <summary>
/// フォルダペアのフォルダ内容を同期させる
/// 
/// TODO 多分このコマンドだけ警告を意味する1を返すようにすることになると思う。
/// </summary>
public sealed class SyncCommand : AbstractCommand
{
    public override string Name => "sync";
    public override string Usage => "<pair name>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Parameter count error.");

        AppService.Synchronize(args[0]);

        return 0;
    }
}
