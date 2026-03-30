using SyncFolderPair.Services;
using SyncFolderPair.Types;

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

        Print("", AppService.EnumerateDifferentEntries(args[0], args[1]));

        return 0;
    }

    static void Print(string path, IEnumerable<DifferentEntryPair> enumerable)
    {
        foreach (var e in enumerable)
        {
            var p = Path.Combine(path, e.Name);
            switch (e)
            {
                case DifferentEntryPair.Dir dep:
                    Print(p, dep.ChildrenEnumerable);
                    break;
                case DifferentEntryPair.FileNone:
                    Console.WriteLine($"[<   ] {p}");
                    break;
                case DifferentEntryPair.NoneFile:
                    Console.WriteLine($"[   >] {p}");
                    break;
                case DifferentEntryPair.DirFile:
                    Console.WriteLine($"[D  F] {p}");
                    break;
                case DifferentEntryPair.FileDir:
                    Console.WriteLine($"[F  D] {p}");
                    break;
                case DifferentEntryPair.Differ dep:
                    if (dep.Left > dep.Right)
                        Console.WriteLine($"[ << ] {p} {dep.Left} {dep.Right}");
                    else if (dep.Left < dep.Right)
                        Console.WriteLine($"[ >> ] {p} {dep.Left} {dep.Right}");
                    break;
            }
        }
    }
}
