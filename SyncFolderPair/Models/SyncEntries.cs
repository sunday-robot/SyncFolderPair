using SyncFolderPair.Services;
using SyncFolderPair.Types;

namespace SyncFolderPair.Models
{
    public static class SyncEntries
    {
        const string _directoryName = "syncentries";

        public static void Save(string pairName, List<SyncEntry> entries)
        {
            var filePath = Path.Combine(App.DataDirectory, _directoryName, $"{pairName}.tsv");
            SyncEntrySaver.Save(filePath, entries);
        }
    }
}
