namespace SyncFolderPair.Utils;

public static class FileComparator
{
    /// <summary>
    /// 二つのファイルの更新日時とファイルサイズを比較する。<br/>
    /// (ファイルの内容までは比較しない)
    /// </summary>
    /// <param name="leftPath"></param>
    /// <param name="rightPath"></param>
    /// <returns></returns>
    public static FileCompareResult Compare(string leftPath, string rightPath)
    {
        var leftTime = File.GetLastWriteTimeUtc(leftPath);
        var rightTime = File.GetLastWriteTimeUtc(rightPath);
        if (leftTime > rightTime)
            return new FileCompareResult.LeftIsNewer(leftTime, rightTime);
        if (leftTime == rightTime)
        {
            var leftSize = new FileInfo(leftPath).Length;
            var rightSize = new FileInfo(rightPath).Length;
            if (leftSize != rightSize)
                return new FileCompareResult.InconsistentSize(leftTime, leftSize, rightSize);
            return new FileCompareResult.Same(leftTime, leftSize);
        }
        return new FileCompareResult.RightIsNewer(leftTime, rightTime);
    }
}
