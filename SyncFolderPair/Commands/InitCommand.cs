using SyncFolderPair.Models;
using SyncFolderPair.Services;

namespace SyncFolderPair.Commands;

/// <summary>
/// 指定されたフォルダペアの同期用の管理ファイルを作成する。<br/>
/// 
/// 管理ファイルは、現在フォルダに存在するすべてのファイルの相対パス名、更新日時、サイズを記録したものである。<br/>
/// </summary>
public sealed class InitCommand : AbstractCommand
{
    public override string Name => "init";
    public override string Usage => "<pair name>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Parameter count error.");

        DirectorySynchronizer.Initialize(args[0]);

        return 0;
    }
}
