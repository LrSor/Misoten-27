# MiniGame UI Base

`BaseState` と `StateMachine` を利用する共通フロー。

1. `BeginMiniGame()` → 説明（既定5秒、操作不可）
2. 自動でプレイ（操作可）
3. ミニゲームの終了条件から `FinishMiniGame()` → 終了表示（既定5秒、操作不可）
4. UI を閉じ、開始前の Player 操作状態へ復帰

時間には `Time.unscaledTime` を使う。プレイ時間・勝敗判定は継承先に実装する。

## 入力の渡し方

入力取得スクリプトから次を呼ぶ。State はデバイスを直接参照しない。

```csharp
miniGameUI.ReceiveVector2Input(vector2Value);
miniGameUI.ReceiveButtonInput(true);  // 押した時
miniGameUI.ReceiveButtonInput(false); // 離した時
```

入力取得 → UI の受信関数 → `MiniGameUIPlayingState` の受信関数という経路。
待機・説明・終了表示中と UI 無効時は入力を破棄する。
State は Vector2 の最新値とボタンの押下状態を保持し、プレイ終了時にリセットする。
Vector2 はクランプせず、そのまま渡す。スティック値・マウス delta・座標などの意味付けは呼び出し側と継承先で決める。
Vector2 は毎フレーム（無入力時もゼロ）送り、ボタンは押下・解放時に送る。
説明中から押し続けたボタンは、プレイ中に押し直すまで押下として扱わない。

`MiniGameUIBase` を継承し、以下を必要に応じて override する。

- `OnPhaseChanged(Phase)`：説明・プレイ・結果などの表示や初期化
- `OnMiniGameInputUpdate(Vector2, bool)`：プレイ中の State から毎フレーム呼ぶ処理
- `OnMiniGameButtonChanged(bool)`：プレイ中のボタン変化（同じ値の連続通知は除外）

`MiniGameUIInputForwarder` は入力を取得する別コンポーネントの実装例。
専用の InputAction を所有し、Inspector の binding を変更できる。
既定値は Vector2 が Gamepad 左スティック・矢印キー・マウス delta、ボタンが Gamepad 南ボタン・左ショルダー・Space・左クリック。
既存 `JoyconInputBridge` の仮想 Gamepad からも受け取れるが、Joycon の接続・割り当て自体は既存の仕組みで行う。
この接続例は全デバイスを対象とする。複数 Player の本番入力には、各 Player 用の入力スクリプトから受信関数を呼ぶ。
同一 UI に複数の送信元を付ける場合は、入力取得側で値を統合する。

## 配置とテスト

`MiniGameUI_Base` の管理 GameObject は有効なままにし、UI Root には子の Canvas を指定する。
StateMachine の Initial State は `MiniGameUIClosedState`。同じ GameObject に4つの MiniGameUI State を置く。
基底コンポーネントを新規追加すると RequireComponent によりこれらも追加される。
継承先でも Unity の `Awake` / `OnDisable` を override する場合は `base` を呼ぶ。

GameUI Scene の TestUIController は F で開始、Escape でテストを終了する。
説明中は入力例のアニメーション、プレイ中は関数経由で受信した Vector2・ボタン状態・押下回数を表示する。
ミニゲーム本体や本番用の UI デザインはまだ含まない。

実際の Player を接続するときは Player Controls にその Player 専用の操作コンポーネントを指定するか、
On Player Control Changed に既存の操作許可処理を接続する。イベントの true は操作可、false は操作不可。
入力送信元自身は停止対象にしない。
Player Controls は開始前の enabled を記録し、プレイ・終了・中断で復元する。
移動 StateMachine などの停止だけでは Rigidbody の慣性は停止しない。完全停止が必要な本番 Player では既存の停止処理も接続する。
GameUI Scene には本体の Player がないため、現在はテスト入力を State 側で制限する。

Unity の `Tools > Sawada > Check MiniGame UI Base` から、タイマー境界・入力の受け渡し・中断復帰・Scene 参照を保存せずに確認できる。
この検査は Edit Mode で実行する。実機 Joycon や Play Mode の見た目は別途確認する。

## 今回の変更ファイル（プロジェクトルート相対）

編集：

- `Assets/Member/Sawada/Scenes/GameUI.unity`：今回の編集許可に基づき State・入力送信元・UI Root を接続。既存の白い Image は非表示。
- `Assets/Member/Sawada/Scripts/GameUI/TestUIController.cs`：基底を継承したテスト表示と F 開始・Escape 終了。

追加：

- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIBase.cs`：共通フロー、入力受信関数、操作制限、継承用フック。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIClosedState.cs`：待機・UI を閉じる処理。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIExplanationState.cs`：5秒の説明。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIPlayingState.cs`：プレイ中の入力保持と処理呼び出し。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIResultState.cs`：5秒の終了表示。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIInputForwarder.cs`：別スクリプトから関数経由で入力を渡す接続例。
- `Assets/Member/Sawada/Cordex/GameUI/Editor/MiniGameUIBaseChecks.cs`：遷移・入力・復帰・Scene の検査。
- `Assets/Member/Sawada/Cordex/GameUI/README.md`：設定・使用方法と本報告。

追加したアセット・フォルダーに対応するメタファイル（既存 GUID の変更なし）：

- `Assets/Member/Sawada/Cordex/GameUI.meta`
- `Assets/Member/Sawada/Cordex/GameUI/Editor.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIBase.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIClosedState.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIExplanationState.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIPlayingState.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIResultState.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIInputForwarder.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/Editor/MiniGameUIBaseChecks.cs.meta`
- `Assets/Member/Sawada/Cordex/GameUI/README.md.meta`

削除・移動なし。フェッチ・早送りプル済み。既存 BaseState / StateMachine の変更なし。

実施結果：Unity の実 DLL を参照して関連10ソースを C# コンパイルし、エラー0。
Inspector 設定フィールドに対する CS0649 警告あり。
Scene の追加 fileID の重複・コンポーネント接続・追加スクリプト GUID の対応・新規 meta の有無、
変更範囲と `git diff --check` は検査を通過。

未実施：Unity バッチ起動がライセンス初期化付近で終了コード1となり、Editor 検査・Play Mode・
実機 Joycon・Unity 内での Missing Script 検査・実 Player の動作は未確認。
生成したコンパイル出力・起動ログは Temp / Logs 配下に置き、成果物に含めない。
