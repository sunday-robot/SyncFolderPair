namespace SyncFolderPair.Commands;

/// <summary>
/// フォルダペアのフォルダ内容を同期させる
/// </summary>
public sealed class SyncCommand : AbstractCommand
{
    public override string Name => "sync";
    public override string Usage => "<pair name>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Parameter count error.");

        // TODO: var pair = PairLoader.Load(pairName);
        // TODO: SyncEngine.Run(pair);
        Console.WriteLine($"[sync] pair={args[0]}");

        return 0;
    }
}
