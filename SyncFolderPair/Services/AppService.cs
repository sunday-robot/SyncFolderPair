#pragma warning disable SYSLIB1045 // 'GeneratedRegexAttribute' に変換します。

using SyncFolderPair.Models;
using SyncFolderPair.Types;
using System.Text.RegularExpressions;

namespace SyncFolderPair.Services;

public static class AppService
{
    static readonly Regex _pairNameRegex = new("^[A-Za-z0-9_\\-\\(\\)\\.,]+$"); // 英数字、アンダースコア、ハイフン、丸括弧、ドット、カンマのみ許可

    /// <summary>
    /// ディレクトリペアを追加する。<br/>
    /// </summary>
    /// <param name="pairName"></param>
    /// <param name="leftDirectory"></param>
    /// <param name="rightDirectory"></param>
    /// <exception cref="Exception"></exception>
    public static void AddDirectoryPair(string pairName, string leftDirectory, string rightDirectory)
    {
        // ペア名の検証
        pairName = pairName.Trim();
        if (!_pairNameRegex.IsMatch(pairName))
            throw new Exception($"不正な名前です: {pairName}");

        // フォルダの検証
        leftDirectory = Path.GetFullPath(leftDirectory);
        rightDirectory = Path.GetFullPath(rightDirectory);
        if (!Directory.Exists(leftDirectory))
            throw new Exception($"Left directory does not exist: {leftDirectory}");
        if (!Directory.Exists(rightDirectory))
            throw new Exception($"Right directory does not exist: {rightDirectory}");

        // 保存(重複チェックはここで行う)
        DirectoryPairStorage.Set(pairName, leftDirectory, rightDirectory);
    }

    public static void DeleteDirectoryPair(string pairName)
    {
        DirectoryPairStorage.Delete(pairName);
        SyncEntriesStorage.Delete(pairName);
    }

    public static void AddIgnoreDirectories(string pairName, Span<string> ignoreDirectoryPaths) => DirectoryPairStorage.AddIgnoreDirectoryPaths(pairName, ignoreDirectoryPaths);

    public static void ForEachPair(Action<string, string, string, IgnoreEntries> action)
    {
        DirectoryPairStorage.ForEach(action);
    }

    public static void AlignDirectoryPair(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.Align(leftDirectory, rightDirectory, ignoreEntries);
    }

    public static void CheckAlignDirectoryPair(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.CheckAlign(leftDirectory, rightDirectory, ignoreEntries);
    }

    public static void ForceAlignDirectoryPair(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.ForceAlign(leftDirectory, rightDirectory, ignoreEntries);
    }

    /// <summary>
    /// フォルダペアの初期化を行う。<br/>
    /// 具体的には管理ファイルの作成で、この管理ファイルには、フォルダペア内のすべてのファイルの相対パス、タイムスタンプを保持するものである。
    /// 
    /// 二つのフォルダ間に差異がないことを前提としており、差異がある場合は、その旨をユーザーに報告し、管理ファイルの作成は行わない。
    /// </summary>
    /// <param name="pairName"></param>
    public static void InitializeSyncEntries(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntryInitializer.Initialize(leftDirectory, rightDirectory, ignoreEntries);
        SyncEntriesStorage.Set(pairName, syncEntries);
    }

    internal static void Synchronize(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntriesStorage.Get(pairName);
        syncEntries = DirectorySynchronizer.Synchronize(leftDirectory, rightDirectory, ignoreEntries, syncEntries);
        SyncEntriesStorage.Set(pairName, syncEntries);
    }

    internal static void CheckSynchronize(string pairName)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntriesStorage.Get(pairName);
        DirectorySynchronizer.CheckSynchronize(leftDirectory, rightDirectory, ignoreEntries, syncEntries);
    }

    public static void PrintDirectoryDifferences(string leftDirectory, string rightDirectory)
    {
        DirectoryDifferencePrinter.Print(leftDirectory, rightDirectory);
    }

}
