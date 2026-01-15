namespace SyncFolderPair.Types
{
    public record SyncEntry(string RelativePath, DateTime LastModifiedUtc, long Size);
}
