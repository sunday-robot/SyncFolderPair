using SyncFolderPair.Core.Types;
using SyncFolderPair.Core.Utils;

namespace SyncFolderPair.Core.Services;

public abstract class DirectoryAligner(bool forceMode, string leftBasePath, string rightBasePath)
{
    #region 公開staticメソッド群
    /// <summary>
    /// 片方のディレクトリにしかないファイルを、もう片方のディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// 両方のディレクトリにあり、タイムスタンプが同じファイルについては何もしないし、その旨をユーザーに通知もしない。
    /// 両方のディレクトリにあり、タイムスタンプが異なる場合は、forceModeか否かで処理が異なる：
    /// forceModeの場合は、古い方のファイルをゴミ箱に移し、新しい方のファイルをもう型のウホディレクトリにコピーする。また、その旨をユーザーに通知する。
    /// forceModeではない場合は、コピーなどはしないが、その旨をユーザーに通知する。
    /// </summary>
    public static void Align(bool forceMode, string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries,
        Action<Operation, bool /* isTargetLeft */, string /* path */>? onEntryOperationStarted,
        Action<string /* message */>? errorOccurred)
    {
        var aligner = new Aligner(forceMode, leftDirectoryPath, rightDirectoryPath);
        aligner.EntryOperationStarted += onEntryOperationStarted;
        aligner.ErrorOccurred += errorOccurred;
        aligner.Align(ignoreEntries);
    }

    /// <summary>
    /// 上のメソッドのファイルコピーなどを行わない版
    /// </summary>
    public static void CheckAlign(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries,
        Action<Operation, bool /* isTargetLeft */, string /* path */>? onEntryOperationStarted,
        Action<string /* message */>? errorOccurred)
    {
        var checker = new Checker(leftDirectoryPath, rightDirectoryPath);
        checker.EntryOperationStarted += onEntryOperationStarted;
        checker.ErrorOccurred += errorOccurred;
        checker.Align(ignoreEntries);
    }
    #endregion 公開staticメソッド群

    #region 本来の抽象クラス定義
    public event Action<Operation, bool /* isTargetLeft */, string /* path */>? EntryOperationStarted;
    public event Action<string /* message */>? ErrorOccurred;

    readonly bool _forceMode = forceMode;
    readonly string _leftBasePath = leftBasePath;
    readonly string _rightBasePath = rightBasePath;

    void Align(IgnoreEntries ignoreEntries)
    {
        var entryPairs = EntryPairsEnumerator.Enumerate(path => File.GetLastWriteTimeUtc(path), _leftBasePath, _rightBasePath, ignoreEntries);
        AlignEntryPairs("", entryPairs);
    }

    void AlignEntryPairs(string path, IEnumerable<EntryPair> entryPairs)
    {
        foreach (var entryPair in entryPairs)
            AlignEntryPair(Path.Combine(path, entryPair.Name), entryPair);
    }

    void AlignEntryPair(string path, EntryPair entryPair)
    {
        switch (entryPair)
        {
            case EntryPair.DirFile:
                ErrorOccurred?.Invoke($"[!!!!] Left is directory, but right is file. {path}");
                break;
            case EntryPair.FileDir:
                ErrorOccurred?.Invoke($"[!!!!] Left is file, but right is directory. {path}");
                break;

            case EntryPair.DirNone x:
                CreateDirectory(false, path);
                AlignEntryPairs(path, x.Children);
                break;
            case EntryPair.NoneDir x:
                CreateDirectory(true, path);
                AlignEntryPairs(path, x.Children);
                break;

            case EntryPair.FileNone:
                CopyFile(false, path);
                break;
            case EntryPair.NoneFile:
                CopyFile(true, path);
                break;

            case EntryPair.DirDir x:
                AlignEntryPairs(path, x.Children);
                break;

            case EntryPair.FileFile x:
                var lt = (DateTime)x.Left!;
                var rt = (DateTime)x.Right!;
                if (lt != rt)
                {
                    if (_forceMode)
                        OverwriteFile(lt < rt, path);    // 新しいファイルで上書きする
                    else
                        EntryOperationStarted?.Invoke(Operation.Skip, lt < rt, path);
                }
                break;
        }
    }

    void CreateDirectory(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.CreateDirectory, isTargetLeft, path);
        var srcBase = isTargetLeft ? _leftBasePath : _rightBasePath;
        var p = Path.Combine(srcBase, path);
        CreateDirectory(p);
    }

    protected abstract void CreateDirectory(string path);

    void CopyFile(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.CopyFile, isTargetLeft, path);
        var (src, dest) = GetSrcDest(isTargetLeft, path);
        if (!CopyFile(src, dest))
            ErrorOccurred?.Invoke($"[Warning] Failed to copy file because the source file is being used by another process: {src}");
    }

    protected abstract bool CopyFile(string src, string dest);

    void OverwriteFile(bool isTargetLeft, string path)
    {
        EntryOperationStarted?.Invoke(Operation.OverwriteFile, isTargetLeft, path);
        var (src, dest) = GetSrcDest(isTargetLeft, path);
        if (!OverwriteFile(src, dest))
            ErrorOccurred?.Invoke($"[Warning] Failed to overwrite file because the source file is being used by another process: {src}");
    }

    protected abstract bool OverwriteFile(string src, string dest);

    (string Src, string Dest) GetSrcDest(bool isTargetLeft, string path)
    {
        var (srcBase, destBase) = isTargetLeft ? (_rightBasePath, _leftBasePath) : (_leftBasePath, _rightBasePath);
        return (Path.Combine(srcBase, path), Path.Combine(destBase, path));
    }
    #endregion 本来の抽象クラス定義

    #region 派生クラス群
    class Aligner(bool forceMode, string leftBasePath, string rightBasePath) : DirectoryAligner(forceMode, leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) => Directory.CreateDirectory(path);
        protected override bool CopyFile(string src, string dest) => FileUtils.SafeCopy(src, dest);
        protected override bool OverwriteFile(string src, string dest) => FileUtils.ReplaceFile(src, dest);
    }

    class Checker(string leftBasePath, string rightBasePath) : DirectoryAligner(false, leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) { }
        protected override bool CopyFile(string src, string dest) => true;
        protected override bool OverwriteFile(string src, string dest) => true;
    }
    #endregion 派生クラス群
}
