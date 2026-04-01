#pragma warning disable SYSLIB1045 // 'GeneratedRegexAttribute' に変換します。

using SyncFolderPair.Core.Models;
using SyncFolderPair.Core.Services;
using SyncFolderPair.Core.Types;
using System.Text.RegularExpressions;

namespace SyncFolderPair.Core;

public static class Core
{
    static readonly Regex _pairNameRegex = new("^[A-Za-z0-9_\\-\\(\\)\\.,]+$"); // 英数字、アンダースコア、ハイフン、丸括弧、ドット、カンマのみ許可

    /// <summary>
    /// ディレクトリペアを追加する。<br/>
    /// </summary>
    /// <param name="pairName"></param>
    /// <param name="leftDirectoryPath"></param>
    /// <param name="rightDirectoryPath"></param>
    /// <exception cref="Exception"></exception>
    public static void AddDirectoryPair(string pairName, string leftDirectoryPath, string rightDirectoryPath)
    {
        // ペア名の検証
        pairName = pairName.Trim();
        if (!_pairNameRegex.IsMatch(pairName))
            throw new Exception($"不正な名前です: {pairName}");

        // フォルダの検証
        leftDirectoryPath = Path.GetFullPath(leftDirectoryPath);
        rightDirectoryPath = Path.GetFullPath(rightDirectoryPath);
        if (!Directory.Exists(leftDirectoryPath))
            throw new Exception($"Left directory does not exist: {leftDirectoryPath}");
        if (!Directory.Exists(rightDirectoryPath))
            throw new Exception($"Right directory does not exist: {rightDirectoryPath}");

        // 保存(重複チェックはここで行う)
        DirectoryPairStorage.Set(pairName, leftDirectoryPath, rightDirectoryPath);
    }

    /// <summary>
    /// ディレクトリペアを削除する
    /// </summary>
    /// <param name="pairName"></param>
    public static void DeleteDirectoryPair(string pairName)
    {
        DirectoryPairStorage.Delete(pairName);
        SyncEntriesStorage.Delete(pairName);
    }

    public static void AddIgnoreDirectories(string pairName, Span<string> ignoreDirectoryPaths)
        => DirectoryPairStorage.AddIgnoreDirectoryPaths(pairName, ignoreDirectoryPaths);

    public static IEnumerable<(string, string, string, IgnoreEntries)> EnumeratePairs()
        => DirectoryPairStorage.Enumerate();

    public static void AlignDirectoryPair(string pairName,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.Align(false, leftDirectory, rightDirectory, ignoreEntries,
            onEntryOperationStarted, onErrorOccurred);
    }

    public static void ForceAlignDirectoryPair(string pairName,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.Align(true, leftDirectory, rightDirectory, ignoreEntries,
            onEntryOperationStarted, onErrorOccurred);
    }

    public static void CheckAlignDirectoryPair(string pairName,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        DirectoryAligner.CheckAlign(leftDirectory, rightDirectory, ignoreEntries,
            onEntryOperationStarted, onErrorOccurred);
    }

    /// <summary>
    /// フォルダペアの初期化を行う。<br/>
    /// 具体的には管理ファイルの作成で、この管理ファイルには、フォルダペア内のすべてのファイルの相対パス、タイムスタンプを保持するものである。
    /// 
    /// 二つのフォルダ間に差異がないことを前提としており、差異がある場合は、その旨をユーザーに報告し、管理ファイルの作成は行わない。
    /// </summary>
    /// <param name="pairName"></param>
    public static void InitializeSyncEntries(string pairName,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntryInitializer.Initialize(leftDirectory, rightDirectory, ignoreEntries, onErrorOccurred);
        SyncEntriesStorage.Set(pairName, syncEntries);
    }

    public static void Synchronize(string pairName,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntriesStorage.Get(pairName) ?? throw new Exception("Not initialized.");
        syncEntries = DirectorySynchronizer.Synchronize(leftDirectory, rightDirectory, ignoreEntries, syncEntries,
            onEntryOperationStarted, onErrorOccurred);
        SyncEntriesStorage.Set(pairName, syncEntries);
    }

    public static void CheckSynchronize(string pairName,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred)
    {
        var (leftDirectory, rightDirectory, ignoreEntries) = DirectoryPairStorage.Get(pairName);
        var syncEntries = SyncEntriesStorage.Get(pairName) ?? throw new Exception("Not initialized.");
        DirectorySynchronizer.CheckSynchronize(leftDirectory, rightDirectory, ignoreEntries, syncEntries,
            onEntryOperationStarted, onErrorOccurred);
    }

    public static IEnumerable<DifferentEntryPair> EnumerateDifferentEntries(string leftDirectory, string rightDirectory)
       => DifferentEntriesEnumerator.Enumerate(leftDirectory, rightDirectory);

    public static IEnumerable<EntryPair> EnumerateEntries(string leftDirectory, string rightDirectory)
        => EntryPairsEnumerator.Enumerate(path => null, leftDirectory, rightDirectory, new IgnoreEntries());
}
