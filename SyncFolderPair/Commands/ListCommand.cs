using SyncFolderPair.Models;

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

        DirectoryPairs.ForEach((name, left, right, ignoreDirectorySet) =>
        {
            Console.WriteLine($"{name}:");
            Console.WriteLine($"  left Directory:  {left}");
            Console.WriteLine($"  right Directory: {right}");
            Console.WriteLine($"  ignore directory[{ignoreDirectorySet.Count}]:");
            foreach (var path in ignoreDirectorySet)
            {
                Console.WriteLine($"    {path}");
            }
            Console.WriteLine();
        });

        return 0;
    }
}
