namespace SyncFolderPair.Services
{
    public class FileSizeMismatchException : Exception
    {
        public long LeftSize { get; }
        public long RightSize { get; }

        public FileSizeMismatchException(
            long leftSize,
            long rightSize)
            : base($"Same timestamp but different size: {leftSize} != {rightSize}")
        {
            LeftSize = leftSize;
            RightSize = rightSize;
        }
    }
}
