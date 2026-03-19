namespace SyncFolderPair.Utils;

public abstract record FileCompareResult
{
    // コンストラクタを private にすることで、
    // このクラスの内部に定義された型以外は継承できなくなる
    private FileCompareResult() { }

    /// <summary>
    /// 左のファイルの方が新しい
    /// </summary>
    /// <param name="Left"></param>
    /// <param name="Right"></param>
    public sealed record LeftIsNewer(DateTime Left, DateTime Right) : FileCompareResult;

    /// <summary>
    /// 同じファイルである(更新日時が同じ)
    /// </summary>
    /// <param name="LastWriteTimeUtc"></param>
    public sealed record Same(DateTime LastWriteTimeUtc) : FileCompareResult;

    /// <summary>
    /// 右のファイルの方が新しい
    /// </summary>
    /// <param name="Left"></param>
    /// <param name="Right"></param>
    public sealed record RightIsNewer(DateTime Left, DateTime Right) : FileCompareResult;
}
