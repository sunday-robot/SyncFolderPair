namespace SyncFolderPair.Services
{
    public static class DirectoryDifferencePrinter
    {
        public static void Check(string left, string right)
        {
            DirectoryDiffScanner.Scan(
                left,
                right,
                rel =>
                {
                    Console.WriteLine($"[<   ] {rel}");
                    return true;
                },
                rel =>
                {
                    Console.WriteLine($"[ << ] {rel}");
                    return true;
                },
                rel =>
                {
                    Console.WriteLine($"[ >> ] {rel}");
                    return true;
                }, rel =>
                {
                    Console.WriteLine($"[   >] {rel}");
                    return true;
                },
                (rel, leftSize, rightSize) =>
                {
                    Console.WriteLine($"[ !!!! ] {rel}, {leftSize}, {rightSize}");
                    return true;
                }
                );
        }
    }
}
