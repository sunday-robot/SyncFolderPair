using SyncFolderPair.Types;
using System.Diagnostics;

namespace SyncFolderPair.Commands
{
    public static class ProgressPrinter
    {
        public static void Print(Operation operation, bool isTargetLeft, string path)
        {
            var s = OperationToString(operation);
            if (isTargetLeft)
                Console.WriteLine($"[<{s,-10}] {path}");
            else
                Console.WriteLine($"[{s,10}>] {path}");
        }

        static string OperationToString(Operation operation)
        {
            return operation switch
            {
                Operation.CreateDirectory => "CREATE",
                Operation.DeleteDirectory => "DELDIR",
                Operation.DeleteFile => "DELFIL",
                Operation.CopyFile => "COPY",
                Operation.OverwriteFile => "OVRWRT",
                Operation.Skip => "SKIP",
                _ => throw new UnreachableException()
            };
        }
    }
}
