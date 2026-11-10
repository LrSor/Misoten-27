# Netcode for Entities 試験運用

対象ブランチ: `Sawada_MapMoveCamera`

## 起動

- Unity 6000.3.14f1、Netcode for Entities 1.14.3、Entities 1.4.8。Unity本体は更新しない。
- `Assets/Member/Sawada/Scenes/testMapPlayerMove.unity` または `Assets/Member/Sawada/Scenes/CordexScene.unity` を再生する。
- 既存の `NetworkTestSession` の `Use Entities Trial` が有効な場合、独自TCP／Relayセッションの代わりに試験用セッションを起動する。
- LANではHost側で `Start Host`、Client側でHostのIPv4と同じUDPポートを入力して `Join Client`。同一PCの別プロセスでは `127.0.0.1`。
- LANで必要なのはUDPポート（初期値7777）の許可。以前のTCP試験とは異なる。
- Relayでは `Unity Relay` を選び、Hostが作成したルームコードをClientに入力する。Unity Servicesの設定とネット接続が必要。
- `Sawada > Cordex > Validate and Build Entities Trial` で検証とWindows Development Buildを実行する。出力先は既存と同じ `Builds/NetworkTest/NetworkTest.exe`。ビルド対象は `testMapPlayerMove.unity`。
- 移動はWASD／左スティック、SprintはShift／既存のSprintアクション。Joyconは既存のScene側ブリッジを使用する。

## 今回の移行範囲

- Hostと最大3 Client。HostにもローカルClient Worldを作成する。
- 移動入力は `ICommandData`、プレイヤー状態はOwner Predicted Ghost。入力履歴、再送用の冗長入力、スナップショット、巻き戻し／再計算はNetcodeが管理する。
- 移動計算は50Hz、スナップショットは25Hz。本人だけ予測し、他プレイヤーはNetcodeで補間する。
- 歩行・加速・減速・旋回・5秒間のSprintを共有する再計算可能な処理に分離。位置・速度・向き・Sprint状態／経過時間／消費した入力を同期する。
- 既存マップの静的ColliderにカプセルをCastし、床・壁に沿って移動する。従来のRigidbody運動をそのまま再実行する方式ではない。Unity Physicsパッケージは導入していない。
- 同じ物理Tickの再計算前後の差を表示オフセットとして減衰させる。4m以上の補正は即時反映。通常の移動に表示補間待ちを加えない。
- GameObjectは表示とカメラ・入力に使用し、移動用Rigidbodyはkinematic、Colliderとゲームプレイ用スクリプトは無効にする。
- 既存のNPC等のワールド状態は暫定的にNetcodeの信頼性付きRPCで転送する。移動用コマンド／スナップショットとは別の経路になる。
- Prefabアセットは作成・変更しない。既存Prefabを表示用に参照し、Ghost Prefabは実行時にEntityとして登録する。

## 未移行・制約

- 装備取得・交換・選択、強奪、Fキーのドア操作等は試験経路では無効。旧経路と混ぜて二重実行しない。
- 他プレイヤー・NPC・動く床など、動的物体との衝突や押し合いは未対応。`NetworkWorldObject` のColliderも移動予測から除外するため、閉じたネットワークドアは移動を遮らない。
- 段差の乗り越え、重なった状態からの押し出し、斜面の登坂制限は未実装。通常の床・壁で先に試験する。
- 静的マップの形状はHost／Clientで同じである必要がある。既存カタログ検証はSceneパスとオブジェクト構成を照合するもので、マップ全体の形状ハッシュではない。同じビルドを使用する。
- ワールド状態は既存の12件単位で分割し、さらに400文字ずつのRPCへ分割して信頼性付き通信の1パケット制限に収める。元のJSONは4093 UTF-8バイトまで。
- Relayは同一PCの別プロセスから実際のRelayサービスを経由する接続と移動を確認済み。異なるPC・回線での接続、Joycon実機、カメラの見た目は別途確認する。
- Windows実行終了時に `Persistent allocates 4 individual allocations` の警告が出る。通信を開始しない起動でも再現し、Nativeリーク検出のスタックはNetcode 1.14.3の `GhostEntityMapping` / `NetcodeUnmanaged.TryInitialize` を指している。パッケージ本体は変更していない。

## 自動試験用引数

`--net-mode host` / `--net-mode client` / `--net-mode relay-host` / `--net-mode relay-client`

`--net-ip 127.0.0.1 --net-port 7777`、Relay参加時は `--net-code CODE`。

- `--net-test-motion`: 2秒ごとに左右へ自動移動。
- `--net-test-gameplay`: 自動移動中にSprintを1回入力。装備試験は含まない。
- `--net-test-correction`: Hostが起動5秒後にプレイヤーの確定位置をX方向に3m変更する。
- `--net-trace ABSOLUTE_PATH`: Client／Serverの状態と本人の補正回数・表示オフセットをJSON Linesで記録する。
- `--net-exit-after SECONDS`: 指定秒後に終了する。
- Development Buildでは、Netcode標準の `--loadNetworkSimulatorJsonFile ABSOLUTE_PATH` で遅延・ジッター・欠落を設定できる。Editor内ではMultiplayer PlayMode ToolsのNetwork Simulatorを使用する。

生成された `Logs/`、`Builds/`、`Library/` は成果物に含めない。

## 2026-11-05 検証結果

- 最終Windows Development Build成功、ビルドエラー0件、警告6件。
- 両対象Sceneと参照PrefabのMissing Script・参照確認に合格。Scene／Prefabの実行時検証はビルド対象の `testMapPlayerMove.unity` を使用。
- 状態を保存した時点からの移動の再計算、Sprint終了、静的な床・壁との衝突、入力を離した際の停止に合格。
- 最終ビルドでHost＋3 Clientの同時接続・移動に合格。
- Netcodeのシミュレーターが有効であることをログで確認し、Clientの送受信に各100±20msの遅延と各5%の欠落を適用。Sprint、Hostが入れた3mの確定位置変更、表示オフセットの減衰を確認。試験対象の実行時例外は0件。
- 先行ビルドでは、切断したプレイヤーの削除と別プロセスからの再参加も確認。
- 最終ビルドでRelayの部屋作成・コード参加・2人の移動に合格。同一PCでの別プロセス試験。
- 結果は生成物の `Logs/EntitiesTrial-final-results.json`、詳細トレースは `Builds/NetworkTest/Verification/entities-verified-*.jsonl`／`.log` に保存。
- 補正時間や距離の閾値は試験用の初期値。実際の操作感・見た目の調整、異なるPCと回線での試験は継続する。

## FPS安定化（描画負荷の軽減）

- Entities試験の起動時は軽量描画を有効にする。HostとClientそれぞれのローカル描画だけに適用する。
- URPアセットの実行時コピーで描画スケールを最大0.8、影の距離を最大20m、カスケードを1、MSAAを1にする。プレイヤーカメラとロビーカメラのポストプロセスを無効にする。UIの解像度・マップ形状・衝突判定は変えない。
- 通信画面の `Lightweight graphics` をオフにすると標準画質へ戻る。Sceneを終了した際も元の設定を復元する。共有URPアセットやProjectSettingsは保存・変更しない。
- FPS、直近約1秒のフレーム時間P95、33.3msを超えたフレーム数を表示する。P95は95%のフレームが収まる時間。FPSは描画待ちも含む実際の更新頻度で、GPU単体の処理時間ではない。
- 比較試験は `--net-render-profile standard` / `--net-render-profile lightweight` を指定する。省略時はlightweight。
- `--net-performance-trace ABSOLUTE_PATH` で約1秒ごとのフレーム統計と実際の描画設定をJSON Linesへ記録する。描画なし実行は `rendering: false` と記録されるため、FPS性能の比較には使わない。
- 60FPS上限、VSync、入力タイミング、移動50Hz、スナップショット25Hz、補間時間、接続方式は変更していない。軽量化しても60FPSを保証するものではなく、ノートPC側で標準・軽量の両方を比較する。

## 旧経路へ戻す場合

停止中に対象Sceneの `Use Entities Trial` を無効にすると既存の独自通信経路を使用する。他のSceneの設定は変更していない。試験中に切り替えず、一度停止して再起動する。
