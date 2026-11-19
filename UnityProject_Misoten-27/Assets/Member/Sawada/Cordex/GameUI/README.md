# MiniGame UI Base

`BaseState` と `StateMachine` を利用する共通フロー。

1. `BeginMiniGame()` → 説明アニメーション（連番再生完了まで操作不可）
2. 自動でプレイ（操作可）
3. ミニゲームの終了条件から `FinishMiniGame()` → 終了表示（既定5秒、操作不可）
4. UI を閉じ、開始前の Player 操作状態へ復帰

時間には `Time.unscaledTime` を使う。プレイ時間・勝敗判定は継承先に実装する。

## 連番 Sprite の再生

GameUI Scene の `MiniGameUI_Base > Canvas > SpeechBubble > Image` に
`UISpriteSequencePlayer` を追加し、`TestRenban.png` の100コマを設定済み。
F で開始すると、吹き出しの拡大完了後から連番を1回再生し、再生が完了したら0.1秒で吹き出しを閉じてプレイへ移行する。

Inspector の設定：

- Sprite Sheet：Sprite Editor で分割済みの画像。指定するだけで末尾の連番順にコマを自動登録する（`sample_2` → `sample_10` の順）。コマ配列は内部保存し、Inspector には表示しない。
- Duration Seconds：「全コマで1ループする秒数」。初期値2秒。100コマを2秒なら1コマ約0.02秒。
- Loop：通常の `Play()` で繰り返すかを指定。説明 State は `PlayOnce()` で1回再生する。
- Play On Enable：有効になった時に再生。今回の Image はオフにし、説明 State から開始する。
- Use Unscaled Time：オンなら Time.timeScale に影響されない。初期値オン。

他の UI Image にもコンポーネントを追加して使用できる。
コマを1枚ずつ登録する必要はない。Sprite Sheet を外すと内部のコマ配列も空にする。
5コマ・6コマなど枚数が変わっても、指定秒数で全コマを再生する。
スクリプトからは `Play()` で先頭から開始、`PlayOnce()` で1回再生、`Stop()` で停止して先頭に戻す。
再生位置は `CurrentFrameIndex`、再生中かどうかは `IsPlaying`、最後まで再生したかは `HasCompleted` で参照する。
`Stop()` と再開時には `HasCompleted` も false に戻す。

説明 State は経過時間だけでは吹き出しを閉じず、`HasCompleted` を確認して閉じ始める。
Duration Seconds が5秒なら、5秒分の全コマを再生し終わるまで表示を継続する。
連番コンポーネントを指定しない場合のみ、従来の3秒表示にフォールバックする。
画像のコマ数や再生時間を変えるだけなら Animator / Animation Clip の作成は不要。

## 説明アニメーション

`MiniGameUIExplanationState` の Call Icon に Image、Speech Bubble に RectTransform を指定する。
GameUI Scene は既存の CallIcon / SpeechBubble に接続済み。配置・画像設定は維持する。
以下を順番に実行し、吹き出しを閉じ終わってからプレイへ移行する。

| 開始からの時間 | 動作 |
| --- | --- |
| 0～0.2秒 | CallIcon の Scale を 0→1 |
| 0.2～0.6秒 | CallIcon の α を 1→0.5→1（1回目） |
| 0.6～1.0秒 | CallIcon の α を 1→0.5→1（2回目） |
| 1.0～1.4秒 | CallIcon の α を 1→0 |
| 1.4～1.5秒 | SpeechBubble の Scale を 0→1 |
| 1.5秒～再生完了 | SpeechBubble 内の連番を指定秒数で1回再生 |
| 再生完了後0.1秒 | SpeechBubble の Scale を 1→0、閉じ終わったらプレイへ移行 |

点滅の「0.4秒」は1往復の時間。3回目は同じ0.4秒で透明にする。
Scale は XYZ を同じ値にする。経過時間から値を計算するため、低フレームレート時も各区間を累積遅延させない。
中断・終了時には両方の Scale を0、CallIcon の α を1に戻し、次回開始時も初期化する。
説明の残り時間は連番の指定秒数に合わせた見込み値。状態遷移は実際の再生完了を待つ。
既定の2秒再生なら説明全体は約3.6秒。終了表示は引き続き5秒。

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
説明中は CallIcon / SpeechBubble のアニメーション、プレイ中は関数経由で受信した Vector2・ボタン状態・押下回数を表示する。
ミニゲーム本体や本番用の UI デザインはまだ含まない。

実際の Player を接続するときは Player Controls にその Player 専用の操作コンポーネントを指定するか、
On Player Control Changed に既存の操作許可処理を接続する。イベントの true は操作可、false は操作不可。
入力送信元自身は停止対象にしない。
Player Controls は開始前の enabled を記録し、プレイ・終了・中断で復元する。
移動 StateMachine などの停止だけでは Rigidbody の慣性は停止しない。完全停止が必要な本番 Player では既存の停止処理も接続する。
GameUI Scene には本体の Player がないため、現在はテスト入力を State 側で制限する。

Unity の `Tools > Sawada > Check MiniGame UI Base` から、タイマー境界・入力の受け渡し・中断復帰・Scene 参照を保存せずに確認できる。
この検査は Edit Mode で実行する。実機 Joycon や Play Mode の見た目は別途確認する。

## 基底作成時の変更ファイル（プロジェクトルート相対）

以下は基底作成時点の報告。説明アニメーション追加後の検査結果は末尾に記載する。

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

## 説明アニメーション追加時の変更と確認

フェッチ・早送りプルで `8789f85` まで取り込み、配置済みの画像を接続した。

編集ファイル（プロジェクトルート相対）：

- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIExplanationState.cs`：CallIcon の拡大・点滅・消失、SpeechBubble の拡大・3秒待機・縮小と完了後のプレイ遷移。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIBase.cs`：経過時間を State と共有し、説明の残り時間を4.6秒の演出に合わせる。
- `Assets/Member/Sawada/Cordex/GameUI/Editor/MiniGameUIBaseChecks.cs`：各演出時刻の Scale・α、操作禁止、遷移、中断復帰、Scene の画像参照を検査。
- `Assets/Member/Sawada/Scripts/GameUI/TestUIController.cs`：説明中のテスト用 OnGUI を非表示にし、Scene の画像を表示。
- `Assets/Member/Sawada/Scenes/GameUI.unity`：説明 State に既存の CallIcon / SpeechBubble を接続し、従来の説明5秒設定を削除。配置・画像・GUID を維持。
- `Assets/Member/Sawada/Cordex/GameUI/README.md`：演出仕様と今回の報告を追記。

追加・削除・移動したファイルなし。既存 meta の変更なし。

確認結果：

- Unity の実 DLL を参照した関連10ソースの C# コンパイル：エラー0（Inspector 設定フィールドの CS0649 警告あり）。
- Unity 6000.3.14f1 のバッチで `MiniGameUIBaseChecks.Run`：PASS、終了コード0。
- 12時点の拡大率・透明度、閉じる途中までの操作禁止、演出完了後のプレイ遷移、入力制限、中断・再開、Player 操作状態の復帰を確認。
- GameUI Scene の Missing Script なし、State と両画像の参照を確認。
- 見た目の目視確認、Play Mode の実時間再生、実機 Joycon と実 Player の確認は未実施。

プロジェクト既存の Cinemachine サンプル asmref 警告は起動時に出るが、検査は通過した。
Unity 起動による `Assets/Member/Liu/Fonts/LiuQuestFont SDF.asset` の自動更新は、
起動前に変更がなかったことを確認したうえで元の内容に復元し、成果物から除外した。

## 連番アニメーション追加時の変更と確認

フェッチ・早送りプルで `3e79e67` まで取り込み、TestRenban と吹き出し内の Image を使用。

追加ファイル（プロジェクトルート相対）：

- `Assets/Member/Sawada/Cordex/GameUI/UISpriteSequencePlayer.cs`：分割済み画像からコマを数字順に登録し、Inspector 指定秒数で再生。通常ループ・1回再生・完了状態・停止を提供。
- `Assets/Member/Sawada/Cordex/GameUI/UISpriteSequencePlayer.cs.meta`：新規スクリプトの GUID。

編集ファイル（プロジェクトルート相対）：

- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIExplanationState.cs`：吹き出しが開いてから1回再生し、実際の再生完了後に0.1秒で閉じてプレイへ移行。
- `Assets/Member/Sawada/Cordex/GameUI/MiniGameUIBase.cs`：説明時間の見込みを連番の指定秒数に合わせる。
- `Assets/Member/Sawada/Cordex/GameUI/Editor/MiniGameUIBaseChecks.cs`：100コマの数字順・指定秒数・ループ・完了状態・5/6コマ共通再生と、経過時間だけでは閉じないことを検査。
- `Assets/Member/Sawada/Scenes/GameUI.unity`：既存の SpeechBubble/Image に再生コンポーネントを追加し、100コマ・2秒・1回再生・説明 State との参照を設定。
- `Assets/Member/Sawada/Cordex/GameUI/README.md`：Inspector 設定・仕様・本報告を追記。

削除・移動なし。追加画像、既存画像の切り取り設定・GUID、Animator Controller は変更していない。

Unity 6000.3.14f1 の `MiniGameUIBaseChecks.Run` は、再生完了待ちの最終仕様で PASS、終了コード0。
完了前は時間を20秒まで進めても閉じないこと、完了後は0.1秒で閉じてプレイへ移行することを確認。
Scene の Missing Script・連番コンポーネントへの参照、既存UI遷移と入力制限も通過した。
Play Mode での目視再生と実機入力は未確認。実行ログは `Logs/UISpriteSequenceCompletionChecks.log`。

Unity が自動更新した動的フォントは、内容一致を確認したバックアップを Logs 配下に保存してから起動前へ復元した。
Logs / Library / Temp は成果物に含めない。
