using System.Collections.ObjectModel;
using System.Windows;
using SyncFolderPair.Gui.Services;

namespace SyncFolderPair.Gui.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    readonly SyncService _syncService = new();
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
        _syncService.StateUpdated += OnCoreStateUpdated;

        RefreshCommand = new RelayCommand(() => _syncService.EnqueueRefresh(), () => !IsBusy);
        SyncCommand = new RelayCommand(() => EnqueueSync(previewOnly: false), CanStartSync);
        PreviewCommand = new RelayCommand(() => EnqueueSync(previewOnly: true), CanStartSync);
        CancelCommand = new RelayCommand(() => _syncService.EnqueueCancel(), () => IsBusy);

        _syncService.EnqueueRefresh();
    }

    bool CanStartSync() => !IsBusy && !string.IsNullOrWhiteSpace(SelectedPairName);

    void EnqueueSync(bool previewOnly)
    {
        if (SelectedPairName is null)
            return;

        _syncService.EnqueueSynchronize(SelectedPairName, previewOnly);
    }

    void OnCoreStateUpdated(CoreStateDelta delta)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            ApplyDelta(delta);
            return;
        }

        Application.Current.Dispatcher.Invoke(() => ApplyDelta(delta));
    }

    void ApplyDelta(CoreStateDelta delta)
    {
        if (delta.ReplacePairNames is not null)
        {
            PairNames.Clear();
            foreach (var pairName in delta.ReplacePairNames)
                PairNames.Add(pairName);

            if (PairNames.Count > 0 && string.IsNullOrWhiteSpace(SelectedPairName))
                SelectedPairName = PairNames[0];
        }

        if (delta.ClearLogs)
            Logs.Clear();

        if (delta.AddedLogs is not null)
        {
            foreach (var log in delta.AddedLogs)
                Logs.Add(log);
        }

        if (delta.StatusMessage is not null)
            StatusMessage = delta.StatusMessage;

        if (delta.IsBusy is not null)
            IsBusy = delta.IsBusy.Value;
    }

    void UpdateCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SyncCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _syncService.StateUpdated -= OnCoreStateUpdated;
        _syncService.Dispose();
    }
}
