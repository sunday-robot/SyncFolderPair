using System.Threading.Channels;
using SyncFolderPair.Core.Types;

namespace SyncFolderPair.Gui.Services;

public sealed class SyncService : IDisposable
{
    readonly Channel<CoreRequest> _requestChannel = Channel.CreateUnbounded<CoreRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });
    readonly CancellationTokenSource _shutdownCts = new();
    readonly Task _workerTask;
    CancellationTokenSource? _runningOperationCts;

    public event Action<CoreStateDelta>? StateUpdated;

    public SyncService()
    {
        _workerTask = Task.Run(ProcessRequestsAsync);
    }

    public void EnqueueRefresh() => _requestChannel.Writer.TryWrite(new RefreshPairsRequest());

    public void EnqueueSynchronize(string pairName, bool previewOnly) =>
        _requestChannel.Writer.TryWrite(new SynchronizeRequest(pairName, previewOnly));

    public void EnqueueCancel() => _requestChannel.Writer.TryWrite(new CancelCurrentRequest());

    async Task ProcessRequestsAsync()
    {
        try
        {
            await foreach (var request in _requestChannel.Reader.ReadAllAsync(_shutdownCts.Token))
            {
                switch (request)
                {
                    case RefreshPairsRequest:
                        ProcessRefreshPairs();
                        break;
                    case SynchronizeRequest syncRequest:
                        ProcessSynchronize(syncRequest.PairName, syncRequest.PreviewOnly);
                        break;
                    case CancelCurrentRequest:
                        ProcessCancelCurrent();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    void ProcessRefreshPairs()
    {
        var pairNames = Core.Core.EnumeratePairs().Select(x => x.Item1).OrderBy(x => x).ToArray();
        Publish(new CoreStateDelta(
            ReplacePairNames: pairNames,
            StatusMessage: $"ペア数: {pairNames.Length}",
            AddedLogs: ["[Info] ペア一覧を更新しました。"]));
    }

    void ProcessSynchronize(string pairName, bool previewOnly)
    {
        if (_runningOperationCts is not null)
        {
            Publish(new CoreStateDelta(AddedLogs: ["[Warn] 既に処理中のため、新しい依頼を受け付けませんでした。"]));
            return;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
        _runningOperationCts = cts;

        Publish(new CoreStateDelta(
            IsBusy: true,
            ClearLogs: true,
            StatusMessage: previewOnly ? "同期プレビューを実行中..." : "同期を実行中..."));

        var collectedLogs = new List<string>(capacity: 256);

        try
        {
            void OnEntryOperationStarted(Operation operation, bool isTargetLeft, string path)
            {
                cts.Token.ThrowIfCancellationRequested();
                var side = isTargetLeft ? "Left" : "Right";
                collectedLogs.Add($"[{operation}] {side}: {path}");
            }

            void OnErrorOccurred(string message)
            {
                cts.Token.ThrowIfCancellationRequested();
                collectedLogs.Add($"[Warn] {message}");
            }

            if (previewOnly)
                Core.Core.CheckSynchronize(pairName, OnEntryOperationStarted, OnErrorOccurred, cts.Token);
            else
                Core.Core.Synchronize(pairName, OnEntryOperationStarted, OnErrorOccurred, cts.Token);

            Publish(new CoreStateDelta(
                IsBusy: false,
                StatusMessage: previewOnly ? "同期プレビューが完了しました。" : "同期が完了しました。",
                AddedLogs: collectedLogs));
        }
        catch (OperationCanceledException)
        {
            collectedLogs.Add("[Info] キャンセルされました。");
            Publish(new CoreStateDelta(
                IsBusy: false,
                StatusMessage: "処理を中止しました。",
                AddedLogs: collectedLogs));
        }
        catch (Exception ex)
        {
            collectedLogs.Add($"[Error] {ex.Message}");
            Publish(new CoreStateDelta(
                IsBusy: false,
                StatusMessage: "エラーが発生しました。",
                AddedLogs: collectedLogs));
        }
        finally
        {
            _runningOperationCts?.Dispose();
            _runningOperationCts = null;
        }
    }

    void ProcessCancelCurrent()
    {
        if (_runningOperationCts is null)
            return;

        _runningOperationCts.Cancel();
    }

    void Publish(CoreStateDelta delta) => StateUpdated?.Invoke(delta);

    public void Dispose()
    {
        _requestChannel.Writer.TryComplete();
        _shutdownCts.Cancel();

        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation from shutdown.
        }

        _runningOperationCts?.Dispose();
        _shutdownCts.Dispose();
    }
}

abstract record CoreRequest;
sealed record RefreshPairsRequest : CoreRequest;
sealed record SynchronizeRequest(string PairName, bool PreviewOnly) : CoreRequest;
sealed record CancelCurrentRequest : CoreRequest;

public sealed record CoreStateDelta(
    IReadOnlyList<string>? ReplacePairNames = null,
    IReadOnlyList<string>? AddedLogs = null,
    string? StatusMessage = null,
    bool? IsBusy = null,
    bool ClearLogs = false);
