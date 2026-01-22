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
    /// 同じファイルである(更新日時もファイルサイズも同じ)
    /// </summary>
    /// <param name="LastWriteTimeUtc"></param>
    /// <param name="Length"></param> 
    public sealed record Same(DateTime LastWriteTimeUtc, long Length) : FileCompareResult;

    /// <summary>
    /// 右のファイルの方が新しい
    /// </summary>
    /// <param name="Left"></param>
    /// <param name="Right"></param>
    public sealed record RightIsNewer(DateTime Left, DateTime Right) : FileCompareResult;

    /// <summary>
    /// 異常な状態である(更新日時が同じなのに、ファイルのサイズが異なっている)
    /// </summary>
    /// <param name="LeftLastWriteTimeUtc"></param>
    /// <param name="Left"></param>
    /// <param name="Right"></param> 
    public sealed record InconsistentSize(DateTime LeftLastWriteTimeUtc, long Left, long Right) : FileCompareResult;
}
