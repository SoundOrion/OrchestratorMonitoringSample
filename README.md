# OrchestratorMonitoringSample

Azure Durable Functions × 外部API進捗 × Blazor可視化
**ジョブ進捗監視アーキテクチャ・デモサンプル**

## ソリューション構成

| 起動順 | プロジェクト名                      | 主な役割                                                       |
| :-: | :--------------------------- | :--------------------------------------------------------- |
|  1  | **SimpleProgressApi**        | 疑似バッチAPI<br> `/start`でバッチ開始、`/progress`で進捗返却               |
|  2  | **StartOrchestratorAppBody** | Durable Functions本体<br> Orchestrator/Activityで業務API監視/状態管理 |
|  3  | **StartOrchestratorApp**     | ジョブ監視Orchestrator起動・受付API<br> バリデーション/認証/受付                |
|  4  | **BlazorProgressUI**         | 進捗可視化UI（Blazor Server）<br> ジョブ開始・進捗表示・キャンセル機能               |


## 全体フロー

```
BlazorProgressUI（進捗UI）
    │
    ├─[POST]→ StartOrchestratorApp（受付API・start-job-monitor）
    │                │
    │                ├─[Orchestrator起動]→ StartOrchestratorAppBody
    │                │                       │
    │                │                       ├─[業務API起動/進捗確認]→ SimpleProgressApi
    │                │                       └─[進捗・状態監視]
    │                └─[StatusQueryGetUriなどをUIへ返却]
    │
    └─[GET]→ StatusQueryGetUriでポーリング（進捗/状態取得）
```

## 各プロジェクト説明

### 1. **BlazorProgressUI**

* ユーザー向け進捗モニター（Blazor Server/Web Apps想定）
* ジョブ開始ボタン → `StartOrchestratorApp`のAPIへPOST
* 受信した `StatusQueryGetUri` でDurable Functionsの状態/進捗を定期取得＆表示

### 2. **StartOrchestratorApp**

* Durable Orchestrator（`StartOrchestratorAppBody`）の起動・受付API
* バリデーション・認証・監査・前処理など本番運用を意識した構成
* Orchestratorのインスタンス管理URI（StatusQueryGetUri等）をクライアントへ返却

### 3. **StartOrchestratorAppBody**

* Durable Functions本体
* Orchestrator/Activity関数
* 外部業務API（SimpleProgressApi等）を呼び出し、進捗監視・状態管理
* `customStatus` を使って進捗パーセントや任意情報を動的返却

### 4. **SimpleProgressApi**

* 疑似長時間バッチAPI
* `/start`でジョブ開始、`/progress`で進捗％返却
* 業務API部分は本番ではContainer Appsや外部Web APIにも容易に差し替え可能

## 使い方

## 1. Azurite（ローカル Azure Storage エミュレーター）の起動

Durable Functionsの状態管理には **Azure Storage** が必須です。
**ローカル開発時は「Azurite」を必ず先に起動してください。**

### 【起動成功ログ例】

Azure Functions／Durable Functions で正常に利用可能な状態になると
**コンソール上に下記のようなメッセージが表示されます：**

```txt
Azurite Blob service is starting at http://127.0.0.1:10000
Azurite Blob service is successfully listening at http://127.0.0.1:10000
Azurite Queue service is starting at http://127.0.0.1:10001
Azurite Queue service is successfully listening at http://127.0.0.1:10001
Azurite Table service is starting at http://127.0.0.1:10002
Azurite Table service is successfully listening at http://127.0.0.1:10002
```

> **この表示が出ていれば、Azuriteが正常に稼働中です！**

> **⚠️注意**
> Azuriteを起動しないままFunctionsをデバッグ実行すると「127.0.0.1:10000に接続できません」等のエラーになります。
> **ソリューション起動前に必ずAzuriteを立ち上げてください。**

### 2. **プロジェクト起動順**

1. **SimpleProgressApi**（疑似バッチAPI）
2. **StartOrchestratorAppBody**（Durable Functions本体）
3. **StartOrchestratorApp**（受付API）
4. **BlazorProgressUI**（UI）

Visual Studioの「複数スタートアッププロジェクト」で同時起動も可能！

### 3. **実行例**

* UIで「ジョブ開始」ボタン → 受付API経由でOrchestratorを起動
* Orchestratorは疑似バッチAPIの進捗を定期取得、`customStatus`で進捗％を管理
* UIが `StatusQueryGetUri` をポーリングし、進捗バーや状態をリアルタイム表示
* UIからキャンセルボタンで `TerminatePostUri` 呼び出し、API経由でジョブを中断可能

## 詳細・補足

* **StatusQueryGetUri** … Durable Functions標準の状態監視エンドポイント。`customStatus`もここで返却
* **外部API部はContainer Apps/Web API等へ差し替え拡張可能**
* **StartOrchestratorApp**でバリデーションや認証/監査、前処理・A/Bテスト等も柔軟に設計可

## 発展・応用例

* **監査・通知・進捗イベント拡張：**
  オーケストレーション完了・異常・中断時に
  **外部サービス連携・通知（Teams/Slack/メール等）や独自ログ出力も容易**に追加可能。

* **クラウドネイティブ実装の参考例：**

  * 「受付API」と「Durable Functions本体」の疎結合構成により、
    **バリデーション／認証／監視／監査などの共通基盤ロジックの再利用が容易**です。
  * **Container Apps や 他FaaS/API群との連携・差し替えにも柔軟に対応**できます。

> **このリポジトリは、モダンなクラウドアプリケーションにおける
> 長時間バッチや進捗監視・中断制御・可視化UIのアーキテクチャ実例として
> 設計・実装の参考にご活用いただけます！**


## トラブル時

* **Azurite起動必須**（起動していないとFunctionsがStorage接続エラーで失敗）
* `local.settings.json` の `AzureWebJobsStorage` がAzurite向きになっているか要確認

## まとめ

* **進捗可視化UI**、**受付API**、**監視Orchestrator本体**、**ダミー業務API**を疎結合で構成
* 業務バッチや外部APIの状態をDurable Functionsで可視化・監視
* 本番構成でも拡張・分離が容易なアーキテクチャ例


ご質問・ドキュメント追加要望はお気軽にどうぞ！
