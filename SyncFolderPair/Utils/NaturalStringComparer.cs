using Win32Api;

namespace SyncFolderPair.Utils;

/// <summary>
/// ファイル名の並べ替えを、Explorerの並び順に合わせるための比較クラス
/// </summary>
public class NaturalStringComparer : IComparer<string?>
{
    public int Compare(string? x, string? y)
        => Win32.StrCmpLogicalW(x!, y!);
}
