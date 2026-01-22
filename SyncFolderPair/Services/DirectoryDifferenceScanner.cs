using SyncFolderPair.Utils;
using System.Diagnostics;

namespace SyncFolderPair.Services;

public static class DirectoryDifferenceScanner
{
    static readonly NaturalStringComparer _fileNameComparer = new();

    public static void Scan(
        string leftDir,
        string rightDir,
        IReadOnlySet<string>? ignoreDirectoryPathSet,
        Func<string, bool> leftOnly,
        Func<string, DateTime, DateTime, bool> leftIsNewer,
        Func<string, DateTime, long, bool> same,
        Func<string, DateTime, DateTime, bool> rightIsNewer,
        Func<string, bool> rightOnly,
        Func<string, DateTime, long, long, bool> abnormal)
    {
        var leftIgnoreDirectoryPathSet = CreateIgnoreDirectoryAbsolutePathSet(leftDir, ignoreDirectoryPathSet);
        var rightIgnoreDirectoryPathSet = CreateIgnoreDirectoryAbsolutePathSet(rightDir, ignoreDirectoryPathSet);

        var leftEnum = EnumerateRelativePaths(leftDir, leftIgnoreDirectoryPathSet).GetEnumerator();
        var rightEnum = EnumerateRelativePaths(rightDir, rightIgnoreDirectoryPathSet).GetEnumerator();

        bool hasLeft = leftEnum.MoveNext();
        bool hasRight = rightEnum.MoveNext();

        while (hasLeft && hasRight)
        {
            int c = _fileNameComparer.Compare(leftEnum.Current, rightEnum.Current);
            if (c < 0)
            {
                if (!leftOnly(leftEnum.Current))
                    return;
                hasLeft = leftEnum.MoveNext();
            }
            else if (c == 0)
            {
                var relativePath = leftEnum.Current;
                switch (FileComparator.Compare(Path.Combine(leftDir, relativePath), Path.Combine(rightDir, relativePath)))
                {
                    case FileCompareResult.LeftIsNewer a:
                        if (!leftIsNewer(relativePath, a.Left, a.Right))
                            return;
                        break;
                    case FileCompareResult.Same a:
                        if (!same(relativePath, a.LastWriteTimeUtc, a.Length))
                            return;
                        break;
                    case FileCompareResult.RightIsNewer a:
                        if (!rightIsNewer(relativePath, a.Left, a.Right))
                            return;
                        break;
                    case FileCompareResult.InconsistentSize a:
                        if (!abnormal(relativePath, a.LeftLastWriteTimeUtc, a.Left, a.Right))
                            return;
                        break;
                    default:
                        throw new UnreachableException("Internal error");
                }

                hasLeft = leftEnum.MoveNext();
                hasRight = rightEnum.MoveNext();
            }
            else
            {
                if (!rightOnly(rightEnum.Current))
                    return;
                hasRight = rightEnum.MoveNext();
            }
        }

        while (hasLeft)
        {
            if (!leftOnly(leftEnum.Current))
                return;
            hasLeft = leftEnum.MoveNext();
        }

        while (hasRight)
        {
            if (!rightOnly(rightEnum.Current))
                return;
            hasRight = rightEnum.MoveNext();
        }
    }

    static HashSet<string> CreateIgnoreDirectoryAbsolutePathSet(string root, IReadOnlySet<string>? ignoreDirectoryPathSet)
    {
        var ignoreDirectoryAbsolutePathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ignoreDirectoryPathSet == null)
            return ignoreDirectoryAbsolutePathSet;

        foreach (var relativePath in ignoreDirectoryPathSet)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(root, relativePath));
            ignoreDirectoryAbsolutePathSet.Add(absolutePath);
        }
        return ignoreDirectoryAbsolutePathSet;
    }

    static IEnumerable<string> EnumerateRelativePaths(string root, IReadOnlySet<string> ignoreDirectoryPathSet)
    {
        var dirs = Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .OrderBy(x => x, _fileNameComparer);
        foreach (var d in dirs)
        {
            var full = Path.GetFullPath(Path.Combine(root, d!));
            if (ignoreDirectoryPathSet.Contains(full))
                continue;
            foreach (var child in EnumerateRelativePaths(full, ignoreDirectoryPathSet))
                yield return d + "/" + child;
        }

        var files = Directory.EnumerateFiles(root)
            .Select(Path.GetFileName)
            .OrderBy(x => x, _fileNameComparer);
        foreach (var f in files)
            yield return f!;
    }
}
