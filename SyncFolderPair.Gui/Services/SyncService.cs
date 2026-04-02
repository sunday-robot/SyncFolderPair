using SyncFolderPair.Core.Types;

namespace SyncFolderPair.Gui.Services;

public sealed class SyncService
{
    public IReadOnlyList<string> GetPairNames()
    {
        return [.. Core.Core.EnumeratePairs().Select(x => x.Item1).OrderBy(x => x)];
    }

    public Task SynchronizeAsync(string pairName, bool previewOnly,
        Action<Operation, bool, string> onEntryOperationStarted,
        Action<string> onErrorOccurred,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (previewOnly)
            {
                Core.Core.CheckSynchronize(pairName, onEntryOperationStarted, onErrorOccurred, cancellationToken);
            }
            else
            {
                Core.Core.Synchronize(pairName, onEntryOperationStarted, onErrorOccurred, cancellationToken);
            }
        }, cancellationToken);
    }
}
