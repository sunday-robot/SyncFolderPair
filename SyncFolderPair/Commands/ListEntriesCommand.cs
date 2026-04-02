using SyncFolderPair.Core.Types;

namespace SyncFolderPair.Commands;

/// <summary>
/// 二つのフォルダ内のファイル、ディレクトリを列挙する
/// </summary>
public sealed class ListEntriesCommand : AbstractCommand
{
    public override string Name => "list_entries";
    public override string Usage => "<left folder> <right folder>";

    public override int Run(Span<string> args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Parameter count error.");

        Print("", Core.Core.EnumerateEntries(args[0], args[1]));

        return 0;
    }

    static void Print(string parent, IEnumerable<EntryPair> entryPairs)
    {
        foreach (var e in entryPairs)
        {
            var path = Path.Combine(parent, e.Name);
            switch (e)
            {
                case EntryPair.NoneDir c:
                    Print(path, c.Children);
                    break;
                case EntryPair.NoneFile:
                    Console.WriteLine($"[  F] {path}");
                    break;
                case EntryPair.DirNone c:
                    Print(path, c.Children);
                    break;
                case EntryPair.DirDir c:
                    Print(path, c.Children);
                    break;
                case EntryPair.DirFile:
                    Console.WriteLine($"[D F] {path}");
                    break;
                case EntryPair.FileNone:
                    Console.WriteLine($"[F  ] {path}");
                    break;
                case EntryPair.FileDir:
                    Console.WriteLine($"[F D] {path}");
                    break;
                case EntryPair.FileFile:
                    Console.WriteLine($"[F F] {path}");
                    break;
            }
        }
    }
}
