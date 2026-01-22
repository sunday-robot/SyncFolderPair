using SyncFolderPair.Models;

namespace SyncFolderPair.Commands;

/// <summary>
/// フォルダーペアに、無視するディレクトリを追加する。
/// </summary>
public sealed class AddIgnoreCommand : AbstractCommand
{
    public override string Name => "add_ignore";
    public override string Usage => "<pair name> <relative path>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Parameter count error.");

        DirectoryPairs.AddIgnoreDirectoryPath(args[0], args[1]);

        return 0;
    }
}
