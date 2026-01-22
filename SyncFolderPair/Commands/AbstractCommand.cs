namespace SyncFolderPair.Commands
{
    public abstract class AbstractCommand
    {
        abstract public string Name { get; }
        abstract public string Usage { get; }

        abstract public int Run(Span<string> args);
    }
}
