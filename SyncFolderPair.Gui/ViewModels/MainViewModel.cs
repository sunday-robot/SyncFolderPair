using System.Collections.ObjectModel;
using SyncFolderPair.Core.Types;
using SyncFolderPair.Gui.Services;

namespace SyncFolderPair.Gui.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    readonly SyncService _syncService = new();
    CancellationTokenSource? _cts;
    string? _selectedPairName;
    string _statusMessage = "待機中";
    bool _isBusy;

    public ObservableCollection<string> PairNames { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];

    public string? SelectedPairName
    {
        get => _selectedPairName;
        set
        {
            if (SetProperty(ref _selectedPairName, value))
                UpdateCommands();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                UpdateCommands();
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SyncCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand CancelCommand { get; }

    public MainViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshPairs, () => !IsBusy);
        SyncCommand = new RelayCommand(() => _ = RunSyncAsync(previewOnly: false), CanStartSync);
        PreviewCommand = new RelayCommand(() => _ = RunSyncAsync(previewOnly: true), CanStartSync);
        CancelCommand = new RelayCommand(CancelSync, () => IsBusy);

        RefreshPairs();
    }

    bool CanStartSync() => !IsBusy && !string.IsNullOrWhiteSpace(SelectedPairName);

    async Task RunSyncAsync(bool previewOnly)
    {
        if (SelectedPairName is null)
            return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        Logs.Clear();
        StatusMessage = previewOnly ? "同期プレビュー実行中..." : "同期実行中...";

        try
        {
            await _syncService.SynchronizeAsync(
                SelectedPairName,
                previewOnly,
                OnEntryOperationStarted,
                OnErrorOccurred,
                _cts.Token);

            StatusMessage = previewOnly ? "同期プレビューが完了しました。" : "同期が完了しました。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "処理を中止しました。";
            Logs.Add("[Info] キャンセルされました。");
        }
        catch (Exception ex)
        {
            StatusMessage = "エラーが発生しました。";
            Logs.Add($"[Error] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    void RefreshPairs()
    {
        PairNames.Clear();
        foreach (var pairName in _syncService.GetPairNames())
            PairNames.Add(pairName);

        if (PairNames.Count > 0)
            SelectedPairName ??= PairNames[0];

        StatusMessage = $"ペア数: {PairNames.Count}";
        Logs.Clear();
        Logs.Add("[Info] ペア一覧を更新しました。");
    }

    void CancelSync() => _cts?.Cancel();

    void OnEntryOperationStarted(Operation operation, bool isTargetLeft, string path)
    {
        _cts?.Token.ThrowIfCancellationRequested();
        var side = isTargetLeft ? "Left" : "Right";
        Logs.Add($"[{operation}] {side}: {path}");
    }

    void OnErrorOccurred(string message)
    {
        _cts?.Token.ThrowIfCancellationRequested();
        Logs.Add($"[Warn] {message}");
    }

    void UpdateCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SyncCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
