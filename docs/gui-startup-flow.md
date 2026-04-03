# GUIアプリ起動時の処理フロー

## エントリーポイント
1. `SyncFolderPair.Gui.Program.Main()` が起動し、`Application` を生成する。
2. `MainViewModel` を生成（`using` 管理）する。
3. `MainWindow` に ViewModel を注入して生成する。
4. `Application.Run(window)` で UI メッセージループを開始する。

## ViewModel 初期化
1. `MainViewModel` のコンストラクタで `SyncService` の `StateUpdated` イベントを購読する。
2. `RefreshCommand` / `SyncCommand` / `PreviewCommand` / `CancelCommand` を初期化する。
3. 起動直後に `_syncService.EnqueueRefresh()` を実行し、ペア一覧読み込み要求を非同期キューへ投入する。

## サービス層（非同期ワーカー）
1. `SyncService` のコンストラクタで `Task.Run(ProcessRequestsAsync)` を開始し、専用ワーカーを起動する。
2. `ProcessRequestsAsync()` は `Channel<CoreRequest>` を読み続け、要求種別ごとに処理を分岐する。
3. `RefreshPairsRequest` 受信時は `ProcessRefreshPairs()` を実行し、`Core.Core.EnumeratePairs()` でペア名一覧を取得する。
4. 取得結果を `CoreStateDelta` として `StateUpdated` イベントで UI 側に通知する。

## Core層 / 永続化
1. `Core.Core.EnumeratePairs()` は `DirectoryPairStorage.Enumerate()` を呼び出す。
2. `DirectoryPairStorage` は `%AppData%/SyncFolderPair/directorypairs.json`（`App.DataDirectory` + ファイル名）を読み込む。
3. ファイルが存在しない場合は空配列として扱う。

## UI反映
1. `MainViewModel.OnCoreStateUpdated()` が `StateUpdated` を受け取り、必要なら Dispatcher 経由で UI スレッドに戻す。
2. `ApplyDelta()` が `PairNames` / `Logs` / `StatusMessage` / `IsBusy` を更新する。
3. `PairNames` が取得でき、未選択なら先頭ペアを `SelectedPairName` に自動設定する。
4. `IsBusy` / `SelectedPairName` 変化時は `UpdateCommands()` で各 `RelayCommand` の実行可否を再評価する。

## 補足
- 実同期（`Synchronize`）やプレビュー（`CheckSynchronize`）は起動時には走らず、ユーザー操作後にキュー投入される。
- アプリ終了時は `MainViewModel.Dispose()` で `SyncService.Dispose()` が呼ばれ、チャネル完了・キャンセル通知でワーカーを停止する。
