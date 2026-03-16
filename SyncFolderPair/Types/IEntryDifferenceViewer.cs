namespace SyncFolderPair.Types;

public interface IEntryDifferenceViewer
{
    void OnLeftOnly(string relativePath);

    void OnLeftIsNewer(string relativePath, DateTime leftLastWriteTimeUtc, DateTime rightLastWriteTimeUtc);

    void OnSame(string relativePath, DateTime lastWriteTimeUtc, long size);

    void OnRightIsNewer(string relativePath, DateTime leftLastWriteTimeUtc, DateTime rightLastWriteTimeUtc);

    void OnRightOnly(string relativePath);

    void OnAbnormal(string relativePath, DateTime leftLastWriteTimeUtc, long leftSize, long rightSize);
}
