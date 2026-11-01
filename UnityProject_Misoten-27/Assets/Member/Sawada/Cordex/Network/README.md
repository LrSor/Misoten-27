# Host + Client 3台 通信テスト

対象: Unity 6000.3.14f1 / Windows x64。
Scene は `Assets/Member/Sawada/Scenes/testMapPlayerMove.unity`。`Player_ver2.prefab` を生成元に使用（Prefab本体は変更しない）。
Display 1～5の分配・起動処理は追加していません。

## 表示方針（2026-10-31）

通常Playとの見た目の差を増やさないため、通信テスト用に追加した頭上のP番号・YOU表示と、参加者ごとのマテリアル色上書きを削除。必要な接続操作・参加人数・自分の番号は左上の通信用UIに集約する。
壁越しの見え方はユーザー指定により今回の対象外とし、既存のRenderer・Shader・Prefab設定は変更しない。
Windows版の再ビルドはエラー0件・警告2件、終了コード0。実行アプリのHost画面で頭上文字がなく、元Prefabの色と左上UIが表示されることを確認した。Unity EditorのPlay画面との直接比較、Client画面は未確認。
ビルド記録: `Builds/NetworkTest/Verification/unity-build-visual-parity.log`。

## Clientの滑らかさの調整（2026-10-31）

- Hostの状態時刻を、描画フレーム時刻から実際の物理位置に対応する `Time.fixedUnscaledTime` に変更。
- Clientの補間位置をUpdateで反映し、その後に既存PositionConstraintとカメラ追従を評価する順序に変更。Client自身のCinemachineBrainのみLateUpdateに固定し、物理更新と描画更新の混在を避ける。
- 補間の目標時刻も受信のないフレームで進め、通常の受信揺れでは等速再生を維持する。蓄積量がずれた場合のみ再生速度を±2%調整する。100msの補間待ちと通信約20Hz・物理50Hzは維持。ローカル予測は未実装。
- Windows再ビルド成功（エラー0・警告2）、終了コード0。Host＋Clientの自動移動試験で接続・移動・切断を確認し、Clientの177件すべてのXYZ位置・向き・時刻が同じtickのHost状態と一致。
- 起動直後を除く物理時刻の間隔が20msの整数倍であることを確認（最大誤差約5.6μs）。この自動試験は通信・時刻の確認であり、手動操作での滑らかさを確認済みとは扱わない。
- 記録: `Builds/NetworkTest/Verification/unity-build-client-smoothing.log`、`smooth-host.jsonl`、`smooth-client.jsonl`、`client-smoothing-result.json`。

## Sprint・装備選択の復旧検証（2026-10-31）

- Windowsビルド成功（エラー0・警告2）、終了コード0。Scene/PrefabのMissing Scriptチェック通過。
- HostとClientの短いSprint入力後、既存Player_Sprint Stateがそれぞれ約5秒（状態配信で5.00秒・5.04秒）継続し、Player_Walkへ復帰することを確認。
- どちらのプレイヤーも空の装備2枠の選択が0→1→0と切り替わり、装備内容は空のままであることを確認。Client受信202件の位置・向き・Sprint状態・装備状態・時刻がHost記録と一致。
- 自動入力による通信・State処理の検証。実際の長押しキーボード操作と、装備モデル表示は未確認（今回の初期装備はユーザー指定により空）。
- 記録: `Builds/NetworkTest/Verification/unity-build-gameplay.log`、`gameplay-host.jsonl`、`gameplay-client.jsonl`、`gameplay-result.json`。

## 実装

EditorのPlayでの生成高さはPrefab内PlayerBodyの保存済みTransformから取得する。
PrefabアセットのRigidbody.positionは未初期化の物理位置を返す場合があるため、生成位置の取得には使わない。
2026-10-31にEditorのHostで床下へ落下し続ける現象を再現し、修正後は生成元のY=1.72から接地後のワールドY=1.0（PlayerBodyローカルY=-0.72）で安定することを確認。コンパイルエラー0件。この高さ修正は装備取得版のWindowsビルドにも含まれる。

### E取得とPlayerDisplayの検証（2026-10-31）

- 最終Windowsビルド成功（エラー0・警告2）。対象Scene・生成元PrefabのMissing Scriptチェック通過。
- Host/Clientの実際のTrigger接触と自動E入力で、空のスロット0へ装備、矢印でスロット1へ変更して装備、スロット0へ復帰する流れを確認。
- 最終通常描画試験のClient受信744件すべての位置・装備・Sprint・選択スロットがHostと一致。実行時例外なし。
- 900×650のアプリ画面で、Hostの赤い装備とClientの緑の装備が、それぞれ自分のPlayerDisplayの2枠へ映ることを目視確認。非選択枠の既存front表示と矢印切り替えの反映も確認。
- 実キーボードによる手動E操作は未確認。入力自体は自動試験で置き換え、既存Swap Stateと物理接触判定を実行した。
- 記録: `Builds/NetworkTest/Verification/pickup-final-result.json`、`pickup-final-host.jsonl`、`pickup-final-client.jsonl`、各同名 `.log`。最終ビルドはUnity Editorの既存メニューから実行。

- .NET TCP、IPv4、既定ポート7777。追加パッケージなし。
- Hostは `0.0.0.0` に待ち受け、Host自身がP1、ClientがP2～P4。
- Hostは既存のFixed Timestep（0.02秒＝50Hz）でRigidbodyの水平速度を更新し、Unityの物理で重力・衝突を計算。全員のXYZ位置・向き・Host時刻を約20Hzで送信。
- Clientは自分の移動入力だけを送信。操作対象は接続ソケットから決定し、Client指定のIDや位置は採用しない。
- Playerは元Prefabのマテリアル・色を使用。テスト用の頭上文字や色分けは追加しない。参加人数と自分の番号は左上の通信用UIで確認する。
- TCPは4バイトのビッグエンディアン長 + UTF-8 JSON。プロトコル識別子 `SawadaNetworkTest/4`。旧版とは接続不可のため、HostとClientを同じソースから再ビルドする。
- 1メッセージ最大8192バイト、送受信キューは各128件を上限とする。
- 通信断はソケット終了または10秒タイムアウトで検知。未承認接続は5秒で終了。
- 入力更新が0.4秒止まるとHostはそのプレイヤーの移動を停止。
- 切断後はプレイヤーを削除し、空いた番号を再利用する。Host移行・自動再接続は行わない。

## 同一PCでの手動確認

1. `Builds/NetworkTest/NetworkTest.exe` を4つ起動する。exeだけでなく同じフォルダーのDLLと `NetworkTest_Data` も必要。
2. 最初のウィンドウでPortを `7777` にして **Start Host**。
3. 残り3つでHost IPv4を `127.0.0.1`、Portを `7777` にして **Connect Client**。
4. 全ウィンドウが `Players: 4/4`、自分の番号がP1～P4に分かれることを確認。
5. 操作したいウィンドウをクリックし、WASDまたはゲームパッド左スティックで移動。移動中の左Shiftで既存Sprint Stateへ入り、キーを離しても5秒間、視点方向へ自動移動しながら10→5へ減速する。左右矢印で装備スロットを切り替える。初期装備はユーザー指定により2枠とも空。
6. 自分の番号のプレイヤーだけが動き、他のウィンドウでも同じ位置・向きになることを確認。
7. Clientの **Disconnect / Stop Host** を押すかウィンドウを閉じると、全員の画面からそのプレイヤーが消える。
8. 空いたClientを再接続できる。5人目は `Room full` で拒否される。
9. Hostを停止するとClientは切断理由を表示する。接続先が不在なら `Connect failed`、待ち受け不能なら `Host failed` を表示する。

通常起動時は900×650のウィンドウ表示。バックグラウンドでも通信と描画を継続するが、
フォーカスのないウィンドウからの手動入力はゼロにする。
HostのIP欄はClient用であり、Hostは全IPv4インターフェイスで待ち受ける。
入力エラー、接続状態、人数、自分の番号は左上のパネルで確認できる。

## LAN内4台のPC

1. 最新のソースで再ビルドし、`Builds/NetworkTest/` の実行ファイルと依存ファイル一式（`NetworkTest_Data`、`UnityPlayer.dll`、`MonoBleedingEdge` 等を含む）を各PCにコピーする。exeだけでは起動しない。
2. 全PCを同じLANのWi-Fi / 有線ネットワークへ接続する。Hostの画面の **This PC's IPv4 candidates** からWi-Fi / EthernetのIPv4を確認する。複数候補がある場合は `ipconfig` で使用中のLANアダプターと照合する（VPN・仮想アダプターのIPは選ばない）。ネットワークを変更した場合は **Refresh this PC's IP addresses** で更新する。
3. Hostで同じPort（例7777）を指定して **Start Host**。
4. 他の3台ではHost IPv4にHostのLANアドレス（例 `192.168.1.20`）を入力して **Connect Client**。
5. ポートは全員で一致させる。別PCからは `127.0.0.1` を使わない。
6. 接続できなければ同一LANか、IP・ポートが正しいか、HostのWindows FirewallでこのアプリのTCP受信が許可されているか確認する。

Host IPv4欄の初期値は空欄。Hostを開始する場合はIP欄の入力は不要で、Clientだけが接続先を入力する。`0.0.0.0` は待受指定用であり、Clientの接続先には使用しない。
Host側Windowsのファイアウォール確認では、信頼できるLANのプライベートネットワークでアプリの受信を許可する。ファイアウォール全体を無効化する必要はない。
ゲストWi-FiやAPの端末間通信遮断が有効だと、同じSSIDでも接続できない場合がある。LAN内通信ではルーターのポート転送は不要。
Client側PowerShellで `Test-NetConnection <HostのLAN IPv4> -Port 7777` を実行し、`TcpTestSucceeded: True` ならHostのTCP待受へ到達できる（ゲーム同期の確認は別途必要）。

LANでの実機接続・Firewall設定は個別に確認が必要。本実装は信頼できるLAN向けの通信テストで、
認証、暗号化、インターネット向け中継、ゲーム状態全体の同期は実装していない。

### LAN接続設定の改善と検証（2026-11-01）

- TCPの全IPv4待受は既存実装を維持。接続先の初期値を空欄にし、このPCの稼働中インターフェイスのIPv4候補・更新ボタン・別PCでの接続案内を画面に追加。アドレス取得失敗時は `ipconfig` での確認を案内する。
- Clientの空欄・`0.0.0.0`・`255.255.255.255` を接続前に検証し、接続タイムアウト時はIP・ポート・LAN・Hostのファイアウォール確認を案内する。
- Windows x64ビルド成功（エラー0・警告2）、Unity終了コード0。既存ビルド処理による対象Scene・生成元PrefabのMissing Scriptチェック通過。
- 通常のWindows環境で同一PCのLAN IPv4 `192.168.10.102`、TCPポート17779を明示し、Host＋3 Clientを自動移動で検証。各Clientが受信した198件（合計594件）の4人分の状態JSONがHostの同じtickの記録と完全一致し、全プロセス終了コード0・実行時例外なし。
- 接続先の空欄・`0.0.0.0` で接続前の案内表示も確認。ビルドが自動変更した設定は開始時のバイト列へ復元し、追跡中のAssets・ProjectSettings・Packagesがビルド前のハッシュと一致することを確認した。
- 別PC間の実機接続、ファイアウォールの受信許可設定、接続パネルの画面表示・手動操作は未確認。同一PCのLANアドレス経由の成功は、別PCからの受信許可まで確認したことにはならない。
- 記録: `Builds/NetworkTest/Verification/unity-build-lan.log`、`Lan-20261101/result.json`、同フォルダー内のHost/Clientの `.jsonl`・`.log`、`empty-ip.log`、`wildcard-ip.log`。

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
- `--net-ip 192.168.1.20`、`--net-port 7777`: 接続設定。ClientはHostの実際のLAN IPv4を指定する。同一PC試験だけ `--net-ip 127.0.0.1` を明示する。
- `--net-test-motion`: 明示的に指定した場合だけ自動移動入力を使用。
- `--net-test-gameplay`: 自動移動に加え、1秒後にSprintを短く押して離し、7秒・9秒後に装備スロットを切り替える検証モード。`--net-test-motion`とは併用しない。
- `--net-test-pickup`: 検証時だけFieldArmorと重なる位置へ生成し、1秒・4秒後にE入力、3秒・6秒後に装備スロットを切り替える。他の自動入力モードと併用しない。通常起動の生成位置は変えない。
- `--net-trace <絶対パス>`: 受信/配信した状態をJSONLに記録。
- `--net-exit-after <秒数>`: 検証用の自動終了。
- Unity標準 `-logFile <絶対パス>`: プロセスごとのログを保存。

## 同期範囲

Sceneが参照する既存 `Player_ver2.prefab` を実行時に複製する。Prefab本体は変更しない。
自分の入力・カメラ関係を有効化する。Host上の全プレイヤーでは既存の移動StateMachine・PlayerMoveController・Idle/Walk/Sprintを使用し、所有者ごとの通信入力を渡す。Inventoryの直接キーボード読み取りは無効のまま、Hostが既存の左右スロット切り替えメソッドを呼ぶ。
Host上の全プレイヤーはPlayerBodyの動的Rigidbodyと既存Colliderを使用する。歩行5、Sprintは既存Prefabの設定（10→5、5秒）に従う。鉛直速度は維持して重力に任せ、従来のXZ±20制限と高さ固定を撤去した。
HostはRigidbody補間とContinuousDynamic衝突検出を使用し、生成時を除いてTransformへ位置を上書きしない。壁・床への衝突はマップの既存Colliderとレイヤー設定に従う。
Client上はKinematicかつCollider無効の表示用コピーとし、Hostから受信した状態を約100ms遅らせて位置・向きを補間する。TCPの受信が途絶えると最後の状態で止める。Clientで二重に物理計算しない。
Client自身にも通信遅延と補間待ちがある。Sprint中かどうかと装備スロット・装備内容をHostから同期する。選択の左右入力と短いShift入力は通信間隔の途中で失われないよう回数も送信する。Sprintに使う所有者の視点の水平向きをHostへ送り、カメラ自体の計算は各アプリ内で行う。
Hostでは既存PlayerSwapControllerとSwap Stateを使用し、FieldArmorのTriggerに重なってEを押すと現在のスロットへ装備する。装備候補はHostの接触判定で決める。参照元SawadaGameのSwap処理と同じくFieldArmor本体は残る。
装備IDはSceneのFieldArmorが参照するSOから共通の順序で作成し、各プレイヤーの装備内容を同期する。初期装備2枠は空のまま。取得後は既存のCharacterAmorControllerでモデルを反映し、左右矢印で切り替える。
操作中のプレイヤーだけ、既存PlayerDisplayのslot1/slot2・front1/front2をプレビューCameraへ実行時に接続する。各枠には別々の実行時RenderTextureを割り当て、Prefabで無効なItemPreview親も実行時に有効化する。Canvasは1920×1080を基準に画面の高さへ縮尺調整する。元Prefab・RenderTextureアセット・SawadaGame Sceneは変更しない。
ローカル予測・Host補正、ジャンプ、段差を自動で上る処理、落下時の復帰、得点の同期は未実装。

物理更新Hz・入力/状態の送信Hz・描画FPSは別の設定。50Hz/40Hzへの統一は必須ではない。現在の物理50Hz、通信約20Hz、目標描画60FPSを維持して、まず衝突と補間を確認する。ProjectSettingsは変更しない。

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
- 2026-10-30の変更でHostの物理移動とClientの補間を追加。以下は変更前の検証記録であり、今回の変更後の実行結果ではない。

### 切り替え後の検証結果（2026-10-29）

- Windows x64ビルド成功、エラー0件、警告1件。SceneとPlayer_ver2のMissing Script・参照チェック通過。
- Host＋Client 3つの位置・向き同期1,557件が一致。全員の移動・方向転換、定員、切断、再接続に合格。
- 自分の有効カメラとAudioListenerは各1、他参加者は各0。通常描画のHost P1 / Client P2で別々の追従視点を確認。
- 実キーボード／ゲームパッドの長押しとLAN接続は未確認。
- 記録: Builds/NetworkTest/Verification/Run-20261029-190135/result.txt と PlayerVer2Window/result.txt。
- Unityのビルドで既存keifont SDF.assetの動的Atlasがクリアされた。開始時のキャッシュは未復元。今後のビルドでは同ファイルのバイト列を退避・復元する。

## 物理移動版の検証（2026-10-30）

- Windows x64の最終ビルド成功、エラー0件・警告1件。Unityのバッチ処理も終了コード0。Scene/PrefabのMissing Scriptチェック通過。
- 既存ビルド処理を修正し、バッチ起動時に復元先Sceneが空でも例外を起こさず、Scene復元の成否にかかわらず退避した設定を復元するようにした。
- 同じ移動実装の実行ファイルでHost自動移動を12秒実施、終了コード0。186件の状態を記録し、重力でY=1.72から約1.0へ接地、Z=-3.003〜3.064の前後移動と方向転換を確認。
- 通常描画でもHost P1の生成、自分の追従カメラ、床上の表示を確認。手動の長押し操作による滑らかさ、段差、Clientとの接続は今回未確認。
- 最終ビルド記録: `Builds/NetworkTest/Verification/unity-build-final.log`、`build-result.txt`。
- 物理試験記録: `Builds/NetworkTest/Verification/physics-host.jsonl`、`physics-host.log`。
- Unity終了時の設定自動変更も含めて復元し、許可範囲外の差分が残っていないことを確認。
