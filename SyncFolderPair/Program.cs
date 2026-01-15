using SyncFolderPair.Commands;

namespace SyncFolderPair
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                    throw new ArgumentException("Specify command.");
                var command = args[0];
                var commandArgs = args.AsSpan(1);

                switch (command)
                {
                    case "add":
                        AddCommand.Run(commandArgs);
                        break;
                    case "remove":
                        if (args.Length != 2)
                            throw new ArgumentException("Parameter count error.");
                        RemoveCommand.Run(args[1]);
                        break;
                    case "list":
                        if (args.Length != 1)
                            throw new ArgumentException("Parameter count error.");
                        ListCommand.Run();
                        break;
                    case "align":
                        AlignCommand.Run(commandArgs);
                        break;
                    case "sync":
                        if (args.Length != 2)
                            throw new ArgumentException("Parameter count error.");
                        SyncCommand.Run(args[1]);
                        break;
                    case "diff":
                        DiffCommand.Run(commandArgs);
                        break;
                    default:
                        throw new ArgumentException("Wrong command.");
                }
                return 0;
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);

                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  SyncFolderPair add <pair name> <left folder> <right folder>");
                Console.Error.WriteLine("  SyncFolderPair remove <pair name>");
                Console.Error.WriteLine("  SyncFolderPair list");
                Console.Error.WriteLine("  SyncFolderPair align <pair name> [\"force\" or \"check\"]");
                Console.Error.WriteLine("  SyncFolderPair sync <pair name>");
                Console.Error.WriteLine("  SyncFolderPair diff <left folder> <right folder>");
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                return 1;
            }
        }
    }
}
