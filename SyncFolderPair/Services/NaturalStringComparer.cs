using Win32Api;

namespace SyncFolderPair.Services
{
    public class NaturalStringComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
            => Win32.StrCmpLogicalW(x!, y!);
    }
}
