using SyncFolderPair.Types;
using SyncFolderPair.Utils;
using System.Text.Json;

namespace SyncFolderPair.Models;

/// <summary>
/// ディレクトリペアのModel<br/>
/// </summary>
public static class DirectoryPairStorage
{
    const string _fileName = "directorypairs.json";

    /// <summary>
    /// 指定された名前のフォルダペアを取得する。
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static (string LeftDirectory, string RightDirectory, IgnoreEntries)
    Get(string name)
    {
        var pairs = Load(GetFilePath());

        var pair = pairs.FirstOrDefault(p => p.Name == name)
            ?? throw new Exception($"Pair not found: {name}");

        return (pair.LeftDirectory, pair.RightDirectory, pair.IgnoreDirectories);
    }

    /// <summary>
    /// フォルダペアを追加する。<br/>
    /// 無視ディレクトリ集合は空で追加する。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="leftDirectory"></param>
    /// <param name="rightDirectory"></param>
    /// <exception cref="Exception"></exception>
    public static void Set(string name, string leftDirectory, string rightDirectory)
    {
        var pairs = Load(GetFilePath());
        if (pairs.Any(p => p.Name == name))
            throw new Exception($"Pair already exists: {name}");
        var newPairs = new List<DirectoryPair>(pairs)
        {
            new(name, leftDirectory, rightDirectory)
        };
        JsonSaver.Save(GetFilePath(), newPairs);
    }

    /// <summary>
    /// フォルダペアを削除する。
    /// </summary>
    /// <param name="name"></param>
    /// <exception cref="Exception"></exception>
    public static void Delete(string name)
    {
        var pairs = Load(GetFilePath());

        var newPairs = pairs
            .Where(p => p.Name != name)
            .ToList();
        if (newPairs.Count == pairs.Count)
            throw new Exception($"Pair not found: {name}");

        JsonSaver.Save(GetFilePath(), newPairs);
    }

    /// <summary>
    /// フォルダペアを列挙する。
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<(string, string, string, IgnoreEntries)> Enumerate()
    {
        var pairs = Load(GetFilePath());
        foreach (var pair in pairs)
        {
            yield return (pair.Name, pair.LeftDirectory, pair.RightDirectory, pair.IgnoreDirectories);
        }
    }

    /// <summary>
    /// ディレクトリペアに、無視するディレクトリを登録する。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ignoreDirectoryPath"></param>
    /// <exception cref="Exception"></exception>
    internal static void AddIgnoreDirectoryPaths(string name, Span<string> ignoreDirectoryPaths)
    {
        var pairs = Load(GetFilePath());
        var pair = pairs.FirstOrDefault(p => p.Name == name)
            ?? throw new Exception($"Pair not found: {name}");
        foreach (var path in ignoreDirectoryPaths)
        {
            pair.IgnoreDirectories.Add(path);
        }
        JsonSaver.Save(GetFilePath(), pairs);
    }

    static string GetFilePath() => Path.Combine(App.DataDirectory, _fileName);

    static List<DirectoryPair> Load(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var json = File.ReadAllText(filePath);
        var list = JsonSerializer.Deserialize<List<DirectoryPair>>(json) ?? throw new Exception("pairs.json is invalid.");
        return list;
    }
}
