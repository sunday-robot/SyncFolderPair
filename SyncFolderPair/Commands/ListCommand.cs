using SyncFolderPair.Core;
using SyncFolderPair.Core.Types;

namespace SyncFolderPair.Commands;

/// <summary>
/// 設定ファイルに設定されているフォルダペアのリストを出力する
/// </summary>
public sealed class ListCommand : AbstractCommand
{
    public override string Name => "list";
    public override string Usage => "";

    public override int Run(Span<string> args)
    {
        if (args.Length != 0)
            throw new ArgumentException("Parameter count error.");

        foreach (var (name, left, right, ignoreDirectorySet) in Core.Core.EnumeratePairs())
        {
            Console.WriteLine($"{name}:");
            Console.WriteLine($"  left  : {left}");
            Console.WriteLine($"  right : {right}");
            Console.WriteLine($"  ignore directory:");
            PrintIgnoreEntries(ignoreDirectorySet, "    ");
            Console.WriteLine();
        }
        return 0;
    }

    static void PrintIgnoreEntries(IgnoreEntries ignoreDirectorySet, string indent)
    {
        foreach (var node in ignoreDirectorySet.Nodes.OrderBy(x => x.Key))
        {
            switch (node.Value)
            {
                case IgnoreEntries entries:
                    Console.WriteLine($"{indent}{node.Key}/");
                    PrintIgnoreEntries(entries, indent + "  ");
                    break;
                case IgnoreEntriesLeaf:
                    Console.WriteLine($"{indent}{node.Key}");
                    break;
            }
        }
    }
}
