using SyncFolderPair.Services;
using SyncFolderPair.Types;
using System.Text.RegularExpressions;

namespace SyncFolderPair.Models
{
    public static class DirectoryPairs
    {
        static readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "directorypairs.json");
        static readonly Regex _nameRegex = new("^[A-Za-z0-9_\\-\\(\\)\\.,]+$"); // 英数字、アンダースコア、ハイフン、丸括弧、ドット、カンマのみ許可

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
                new(name, leftDirectory, rightDirectory)
            };

            DirectoryPairSaver.Save(_filePath, newPairs);
        }

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

        public static IReadOnlyList<DirectoryPair> Get()
        {
            return DirectoryPairLoader.Load(_filePath);
        }

        public static DirectoryPair Get(string name)
        {
            var pairs = DirectoryPairLoader.Load(_filePath);
            var pair = pairs.FirstOrDefault(p => p.Name == name)
                ?? throw new Exception($"Pair not found: {name}");
            return pair;
        }
    }
}
