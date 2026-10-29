# Host + Client 3台 通信テスト

対象: Unity 6000.3.14f1 / Windows x64。
Scene は `Assets/Member/Sawada/Scenes/testMapPlayerMove.unity`。`Player_ver2.prefab` を生成元に使用（Prefab本体は変更しない）。
Display 1～5の分配・起動処理は追加していません。

## 実装

- .NET TCP、IPv4、既定ポート7777。追加パッケージなし。
- Hostは `0.0.0.0` に待ち受け、Host自身がP1、ClientがP2～P4。
- Hostが50Hzで移動を計算し、全員の位置と向きを20Hzで送信。
- Clientは自分の移動入力だけを送信。操作対象は接続ソケットから決定し、Client指定のIDや位置は採用しない。
- P1水色、P2オレンジ、P3緑、P4紫。画面に番号と自分の識別を表示。
- TCPは4バイトのビッグエンディアン長 + UTF-8 JSON。プロトコル識別子 `SawadaNetworkTest/1`。
- 1メッセージ最大8192バイト、送受信キューは各128件を上限とする。
- 通信断はソケット終了または10秒タイムアウトで検知。未承認接続は5秒で終了。
- 入力更新が0.4秒止まるとHostはそのプレイヤーの移動を停止。
- 切断後はプレイヤーを削除し、空いた番号を再利用する。Host移行・自動再接続は行わない。

## 同一PCでの手動確認

1. `Builds/NetworkTest/NetworkTest.exe` を4つ起動する。exeだけでなく同じフォルダーのDLLと `NetworkTest_Data` も必要。
2. 最初のウィンドウでPortを `7777` にして **Start Host**。
3. 残り3つでHost IPv4を `127.0.0.1`、Portを `7777` にして **Connect Client**。
4. 全ウィンドウが `Players: 4/4`、自分の番号がP1～P4に分かれることを確認。
5. 操作したいウィンドウをクリックし、WASDまたはゲームパッド左スティックで移動、左Shiftで走る。
6. 自分の番号のプレイヤーだけが動き、他のウィンドウでも同じ位置・向きになることを確認。
7. Clientの **Disconnect / Stop Host** を押すかウィンドウを閉じると、全員の画面からそのプレイヤーが消える。
8. 空いたClientを再接続できる。5人目は `Room full` で拒否される。
9. Hostを停止するとClientは切断理由を表示する。接続先が不在なら `Connect failed`、待ち受け不能なら `Host failed` を表示する。

通常起動時は900×650のウィンドウ表示。バックグラウンドでも通信と描画を継続するが、
フォーカスのないウィンドウからの手動入力はゼロにする。
HostのIP欄はClient用であり、Hostは全IPv4インターフェイスで待ち受ける。
入力エラー、接続状態、人数、自分の番号は左上のパネルで確認できる。

## LAN内4台のPC

1. `Builds/NetworkTest/` の実行ファイルと依存ファイル一式を各PCにコピーする。
2. HostのPCのLAN内IPv4アドレスをWindowsのネットワーク情報で確認する。
3. Hostで同じPort（例7777）を指定して **Start Host**。
4. 他の3台ではHost IPv4にHostのLANアドレス（例 `192.168.1.20`）を入力して **Connect Client**。
5. ポートは全員で一致させる。別PCからは `127.0.0.1` を使わない。
6. 接続できなければ同一LANか、IP・ポートが正しいか、HostのWindows FirewallでこのアプリのTCP受信が許可されているか確認する。

LANでの実機接続・Firewall設定は個別に確認が必要。本実装は信頼できるLAN向けの通信テストで、
認証、暗号化、インターネット向け中継、ゲーム状態全体の同期は実装していない。

## ビルド

Unityメニュー **Sawada > Cordex > Build Windows Network Test** を実行する。
開いているSceneに未保存変更がある場合は先に保存する。
対象SceneだけをBuildPipelineに渡すため、EditorBuildSettingsは変更しない。
出力: `Builds/NetworkTest/NetworkTest.exe`。
ビルド結果: `Builds/NetworkTest/Verification/build-result.txt`。

Editorを閉じている場合はUnityの `-batchmode -quit -projectPath <project>` と
`-executeMethod Sawada.Cordex.Editor.NetworkTestBuild.Build` でも実行できる。

## 再現可能な自動確認

PowerShellで `Builds/NetworkTest/Verify-Network.ps1` を実行する（空いているTCPポート17777を使用）。
同じexeをHost 1つとClient 3つで動かし、自動入力で各プレイヤーを移動・回転させ、
各配信番号の全プレイヤーの位置と向きがHostの記録と完全一致するか照合する。
接続失敗、5人目の拒否、Client終了による削除、空いた番号への再接続も確認する。
この自動試験は `-batchmode -nographics` で実行するため、画面や手動操作の確認とは別扱い。
試験用ポートは `-Port 17778` のように変更できる。
記録は `Builds/NetworkTest/Verification/Run-日時/` のログ・JSONL・result.txtに出力。

検証用起動引数:

- `--net-mode host` または `--net-mode client`: 起動直後に選択した役割で接続。
- `--net-ip 127.0.0.1`、`--net-port 7777`: 接続設定。
- `--net-test-motion`: 明示的に指定した場合だけ自動移動入力を使用。
- `--net-trace <絶対パス>`: 受信/配信した状態をJSONLに記録。
- `--net-exit-after <秒数>`: 検証用の自動終了。
- Unity標準 `-logFile <絶対パス>`: プロセスごとのログを保存。

## 同期範囲

既存 `Player_cordex.prefab` のMeshFilter/MeshRendererとTransform階層を実行時に複製して外観を再利用する。
元Prefabは変更しない。既存の入力・カメラ・StateMachineは複製しない。
テスト専用の俯瞰カメラで全員を見る。移動はXZ平面、速度5（走行10）、各軸±20以内。
物理衝突、段差、ジャンプ、装備交換、得点、既存のSprint挙動は今回の同期対象に含まない。移動範囲は引き続きXZの-20～20、高さはPrefabの初期高さで固定。カメラの向きは各アプリ内だけで計算し、通信しない。
TCPの再送待ちや20Hz配信に伴う遅延・段階的な移動表示がある。予測・補間は実装していない。

## ファイル

- `NetworkTestSession.cs`: UI、入力、接続・人数管理、Host移動計算、状態同期、実行時外観生成。
- `TcpPeer.cs`: 非同期TCP送受信、フレーミング、切断とキュー上限。
- `../Editor/NetworkTestBuild.cs`: WindowsビルドとScene/PrefabのMissing Script・参照チェック。
- 本READMEおよび各追加ファイル・フォルダーの `.meta`。
- `../../Scenes/testMapPlayerMove.unity`: NetworkTestSessionを追加、既存単独プレイヤーをScene内だけで非アクティブ化。
- `Builds/NetworkTest/Verify-Network.ps1`: 実行ファイルを用いた自動確認。

## 切り替え前の2026-10-29 検証結果（CordexScene / Player_cordex）

- Windows x64ビルド成功、エラー0件、警告1件（既存スクリプトの未使用フィールド）。
- 同一PCの実行ファイルHost 1つ＋Client 3つで接続、移動・方向転換を確認。
- Clientの411/556/513件、再参加Clientの136件、計1,616件の受信状態が、同じtickのHostの位置・向きと完全一致。
- 接続先不在の失敗理由、5人目の拒否、切断後の削除、空いた番号への再参加を確認。
- 通常ウィンドウ4つで4/4表示、P1～P4の外観・色・自分の識別を確認。
- GUIのHost開始、切断、Client再接続を確認。
- 長押しによる実際のキーボード／ゲームパッド操作と、LAN内4台のPCでの接続は未確認。
  短い自動キー入力では移動を記録できず、手動入力を確認済みとは扱っていない。
- ビルドを阻害していた `Assets/Member/Takahashi/Scripts/Minigame/TestMiniGames/Spin/SpinObject.cs` は、
  ユーザーの明示許可により `m_keyboardStep = 0;` の1行を `#if UNITY_EDITOR` で囲む最小修正を実施。
- 詳細記録: `Builds/NetworkTest/Verification/Run-20261029-171540/result.txt` と `WindowTest/result.txt`。

## Player_ver2 / testMapPlayerMoveへの切り替え

- 自分のPlayerCamera、CinemachineCameraとThirdPersonFollow、Splineカメラ処理のみを有効化。PlayerCameraをDisplay 1に出力。
- 他参加者の入力、カメラ、AudioListenerと既存の単独プレイヤー操作を無効化。移動と向きは既存のTCP同期を継続。
- カメラはPrefabのCameraTargetとSceneのSplinesを参照。WASDと左スティックは自分のカメラの水平向きを基準に移動。
- 接続前・切断後はPlayerCameraの設定を複製した待機カメラを使用。
- 新しいSceneでも壁の当たり判定・段差対応は追加していない。マップ探索用の物理移動は別途対応が必要。

### 切り替え後の検証結果（2026-10-29）

- Windows x64ビルド成功、エラー0件、警告1件。SceneとPlayer_ver2のMissing Script・参照チェック通過。
- Host＋Client 3つの位置・向き同期1,557件が一致。全員の移動・方向転換、定員、切断、再接続に合格。
- 自分の有効カメラとAudioListenerは各1、他参加者は各0。通常描画のHost P1 / Client P2で別々の追従視点を確認。
- 実キーボード／ゲームパッドの長押しとLAN接続は未確認。
- 記録: Builds/NetworkTest/Verification/Run-20261029-190135/result.txt と PlayerVer2Window/result.txt。
- Unityのビルドで既存keifont SDF.assetの動的Atlasがクリアされた。開始時のキャッシュは未復元。今後のビルドでは同ファイルのバイト列を退避・復元する。
