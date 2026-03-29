using Microsoft.VisualBasic;
using SyncFolderPair.Types;
using Win32Api;

namespace SyncFolderPair.Utils;

internal class EntryEnumerator
{
    static readonly Comparison<string> _fileNameComparison = Win32.StrCmpLogicalW;

}
