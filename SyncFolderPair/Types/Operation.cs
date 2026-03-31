namespace SyncFolderPair.Types;

/// <summary>
/// 処理進捗通知用のもの
/// </summary>
public enum Operation
{
    CreateDirectory,
    DeleteDirectory,
    DeleteFile,
    CopyFile,
    OverwriteFile,
    Skip,
};
