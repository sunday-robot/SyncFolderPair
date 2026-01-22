using SyncFolderPair.Commands;

namespace SyncFolderPair;

class Program
{
    static readonly List<AbstractCommand> _commands = [
        new AddCommand(),
        new AddIgnoreCommand(),
        new RemoveCommand(),
        new ListCommand(),
        new AlignCommand(),
        new SyncCommand(),
        new DiffCommand(),
        new InitCommand(),
    ];

    static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
                throw new ArgumentException("Specify command.");

            var command = _commands.Find(c => c.Name == args[0]) ?? throw new ArgumentException($"Wrong command. [${args[0]}]");
            return command.Run(args.AsSpan(1));
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage:");
            foreach (var command in _commands)
            {
                Console.Error.WriteLine($"  SyncFolderPair {command.Name} {command.Usage}");
            }
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            return 1;
        }
    }
}
