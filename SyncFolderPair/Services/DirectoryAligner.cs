using SyncFolderPair.Types;
using SyncFolderPair.Utils;

namespace SyncFolderPair.Services;

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
    public static void Align(bool forceMode, string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var aligner = new Aligner(forceMode, leftDirectoryPath, rightDirectoryPath);
        aligner.Align(ignoreEntries);
    }

    /// <summary>
    /// 上のメソッドのファイルコピーなどを行わない版
    /// </summary>
    public static void CheckAlign(string leftDirectoryPath, string rightDirectoryPath, IgnoreEntries ignoreEntries)
    {
        var checker = new Checker(leftDirectoryPath, rightDirectoryPath);
        checker.Align(ignoreEntries);
    }
    #endregion 公開staticメソッド群

    #region 本来の抽象クラス定義
    readonly bool _forceMode = forceMode;
    readonly string _leftBasePath = leftBasePath;
    readonly string _rightBasePath = rightBasePath;

    void Align(IgnoreEntries ignoreEntries)
    {
        var entryPairs = EntryPairs.Enumerate(_leftBasePath, _rightBasePath, path => File.GetLastWriteTimeUtc(path), ignoreEntries);
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
                Console.WriteLine($"[!!!!] Left is directory, but right is file. {path}");
                break;
            case EntryPair.FileDir:
                Console.WriteLine($"[!!!!] Left is file, but right is directory. {path}");
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
                CopyFile(true, path);
                break;
            case EntryPair.NoneFile:
                CopyFile(false, path);
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
                        OverwriteFile(lt > rt, path);    // 新しいファイルで上書きする
                    else
                        PrintMessage("SKIP", lt > rt, path);
                }
                break;
        }
    }

    static void PrintMessage(string operation, bool leftToRight, string path)
    {
        if (leftToRight)
            Console.WriteLine($"[{operation,10}>] {path}");
        else
            Console.WriteLine($"[<{operation,-10}] {path}");
    }

    void CreateDirectory(bool isLeft, string path)
    {
        PrintMessage("MKDIR", !isLeft, path);
        var srcBase = isLeft ? _leftBasePath : _rightBasePath;
        var p = Path.Combine(srcBase, path);
        CreateDirectory(p);
    }

    protected abstract void CreateDirectory(string path);

    void CopyFile(bool leftToRight, string path)
    {
        PrintMessage("COPY", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        CopyFile(src, dest);
    }

    protected abstract void CopyFile(string src, string dest);

    void OverwriteFile(bool leftToRight, string path)
    {
        PrintMessage("OVERWRITE", leftToRight, path);
        var (src, dest) = GetSrcDest(leftToRight, path);
        OverwriteFile(src, dest);
    }

    protected abstract void OverwriteFile(string src, string dest);

    (string Src, string Dest) GetSrcDest(bool leftToRight, string path)
    {
        var (srcBase, destBase) = leftToRight ? (_leftBasePath, _rightBasePath) : (_rightBasePath, _leftBasePath);
        return (Path.Combine(srcBase, path), Path.Combine(destBase, path));
    }
    #endregion 本来の抽象クラス定義

    #region 派生クラス群
    class Aligner(bool forceMode, string leftBasePath, string rightBasePath) : DirectoryAligner(forceMode, leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) => Directory.CreateDirectory(path);
        protected override void CopyFile(string src, string dest) => File.Copy(src, dest, false);
        protected override void OverwriteFile(string src, string dest) => FileUtils.ReplaceFile(src, dest);
    }

    class Checker(string leftBasePath, string rightBasePath) : DirectoryAligner(false, leftBasePath, rightBasePath)
    {
        protected override void CreateDirectory(string path) { }
        protected override void CopyFile(string src, string dest) { }
        protected override void OverwriteFile(string src, string dest) { }
    }
    #endregion 派生クラス群
}
