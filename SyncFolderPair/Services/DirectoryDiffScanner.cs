namespace SyncFolderPair.Services
{
    public static class DirectoryDiffScanner
    {
        static readonly NaturalStringComparer _fileNameComparer = new();

        public static void Scan(
            string leftDir,
            string rightDir,
            Func<string, bool> leftOnly,
            Func<string, bool> leftIsNewer,
            Func<string, bool> rightIsNewer,
            Func<string, bool> rightOnly,
            Func<string, long, long, bool> abnormal)
        {
            var leftEnum = EnumerateRelativePaths(leftDir).GetEnumerator();
            var rightEnum = EnumerateRelativePaths(rightDir).GetEnumerator();

            bool hasLeft = leftEnum.MoveNext();
            bool hasRight = rightEnum.MoveNext();

            while (hasLeft && hasRight)
            {
                int c = _fileNameComparer.Compare(leftEnum.Current, rightEnum.Current);
                if (c == 0)
                {
                    var relativePath = leftEnum.Current;
                    try
                    {
                        switch (CompareUpdateTime(Path.Combine(leftDir, relativePath), Path.Combine(rightDir, relativePath)))
                        {
                            case 1:
                                if (!leftIsNewer(relativePath))
                                    return;
                                break;
                            case 0:
                                break; // 同一なので何もしない
                            default: // -1
                                if (!rightIsNewer(relativePath))
                                    return;
                                break;
                        }
                    }
                    catch (FileSizeMismatchException ex)
                    {
                        if (!abnormal(relativePath, ex.LeftSize, ex.RightSize))
                            return;
                    }

                    hasLeft = leftEnum.MoveNext();
                    hasRight = rightEnum.MoveNext();
                }
                else if (c < 0)
                {
                    if (!leftOnly(leftEnum.Current))
                        return;
                    hasLeft = leftEnum.MoveNext();
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

        static IEnumerable<string> EnumerateRelativePaths(string root)
        {
            var dirs = Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .OrderBy(x => x, _fileNameComparer);
            foreach (var d in dirs)
            {
                var full = Path.Combine(root, d!);
                foreach (var child in EnumerateRelativePaths(full))
                    yield return d + "/" + child;
            }

            var files = Directory.EnumerateFiles(root)
                .Select(Path.GetFileName)
                .OrderBy(x => x, _fileNameComparer);
            foreach (var f in files)
                yield return f!;
        }

        static int CompareUpdateTime(string leftPath, string rightPath)
        {
            var lt = File.GetLastWriteTimeUtc(leftPath);
            var rt = File.GetLastWriteTimeUtc(rightPath);

            if (lt == rt)
            {
                var leftSize = new FileInfo(leftPath).Length;
                var rightSize = new FileInfo(rightPath).Length;

                if (leftSize != rightSize)
                    throw new FileSizeMismatchException(leftSize, rightSize);

                return 0;
            }

            if (lt > rt)
                return 1;

            return -1;
        }
    }
}
