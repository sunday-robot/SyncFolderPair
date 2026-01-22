#pragma warning disable SYSLIB1045

using SyncFolderPair.Services;
using SyncFolderPair.Types;
using System.Text.RegularExpressions;

namespace SyncFolderPair.Models;

/// <summary>
/// ディレクトリペアのModel<br/>
/// </summary>
public static class DirectoryPairs
{
    const string _fileName = "directorypairs.json";

    static readonly Regex _nameRegex = new("^[A-Za-z0-9_\\-\\(\\)\\.,]+$"); // 英数字、アンダースコア、ハイフン、丸括弧、ドット、カンマのみ許可
    static readonly string _filePath;

    static DirectoryPairs()
    {
        _filePath = Path.Combine(App.DataDirectory, _fileName);
    }

    /// <summary>
    /// フォルダペアを追加する。<br/>
    /// 無視ディレクトリ集合は空で追加する。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="leftDirectory"></param>
    /// <param name="rightDirectory"></param>
    /// <exception cref="Exception"></exception>
    public static void Add(string name, string leftDirectory, string rightDirectory)
    {
        name = name.Trim();

        // ペア名の検証
        if (name.Length == 0)
            throw new Exception("Pair name is empty.");
        if (!_nameRegex.IsMatch(name))
            throw new Exception($"Pair name contains invalid characters: {name}");

        leftDirectory = Path.GetFullPath(leftDirectory);
        rightDirectory = Path.GetFullPath(rightDirectory);

        // フォルダ存在チェック
        if (!Directory.Exists(leftDirectory))
            throw new Exception($"Left directory does not exist: {leftDirectory}");
        if (!Directory.Exists(rightDirectory))
            throw new Exception($"Right directory does not exist: {rightDirectory}");

        // pairs.json の読み込み
        var pairs = DirectoryPairLoader.Load(_filePath);

        // 重複チェック
        if (pairs.Any(p => p.Name == name))
            throw new Exception($"Pair already exists: {name}");

        // 追加し、保存する
        var newPairs = new List<DirectoryPair>(pairs)
        {
            new(name, leftDirectory, rightDirectory, [])
        };

        DirectoryPairSaver.Save(_filePath, newPairs);
    }

    /// <summary>
    /// フォルダペアを削除する。
    /// </summary>
    /// <param name="name"></param>
    /// <exception cref="Exception"></exception>
    public static void Remove(string name)
    {
        var pairs = DirectoryPairLoader.Load(_filePath);

        var newPairs = pairs
            .Where(p => p.Name != name)
            .ToList();
        if (newPairs.Count == pairs.Count)
            throw new Exception($"Pair not found: {name}");

        DirectoryPairSaver.Save(_filePath, newPairs);
    }

    /// <summary>
    /// 各フォルダペアに対し、指定されたActionを実行する。
    /// </summary>
    /// <returns></returns>
    public static void ForEach(Action<string, string, string, IReadOnlySet<string>> action)
    {
        var pairs = DirectoryPairLoader.Load(_filePath);
        foreach (var pair in pairs)
        {
            action(pair.Name, pair.LeftDirectory, pair.RightDirectory, pair.IgnoreDirectoryPathSet);
        }
    }

    /// <summary>
    /// 指定された名前のフォルダペアを取得する。
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static (string LeftDirectory, string RightDirectory, IReadOnlySet<string> IgnoreDirectoryPathSet)
    Get(string name)
    {
        var pairs = DirectoryPairLoader.Load(_filePath);

        var pair = pairs.FirstOrDefault(p => p.Name == name)
            ?? throw new Exception($"Pair not found: {name}");

        return (pair.LeftDirectory, pair.RightDirectory, pair.IgnoreDirectoryPathSet);
    }

    /// <summary>
    /// ディレクトリペアに、無視するディレクトリを登録する。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ignoreDirectoryPath"></param>
    /// <exception cref="Exception"></exception>
    internal static void AddIgnoreDirectoryPath(string name, string ignoreDirectoryPath)
    {
        var pairs = DirectoryPairLoader.Load(_filePath);
        var pair = pairs.FirstOrDefault(p => p.Name == name)
            ?? throw new Exception($"Pair not found: {name}");
        pair.IgnoreDirectoryPathSet.Add(ignoreDirectoryPath);
        DirectoryPairSaver.Save(_filePath, pairs);
    }
}
