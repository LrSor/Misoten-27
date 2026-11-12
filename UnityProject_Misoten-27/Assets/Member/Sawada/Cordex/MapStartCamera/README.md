# MapStartCamera（Spline Dolly）

MapStartCamera.unity を開いて Play すると、3台のカメラが各Splineに沿って移動し、マップ中央を注視します。Unity標準コンポーネントのみを使用し、独自の実行時スクリプトはありません。

## 構成

- Cinemachine Spline Dolly：各カメラのPosition Control。NormalizedのPositionを0から1へ動かします。
- Spline Container：Orbit Center (0, 30, 0) 配下の Dolly Path 01/02/03 - Edit Spline。各カメラ専用の経路です。
- Cinemachine Hard Look At：Map LookAt (0, 0, 0) を注視します。
- AnimatorとTimelineのAnimation Track：Spline DollyのPositionをアニメーションします。
- Cinemachine BrainとCinemachine Track：ショットの切り替えとブレンド。
- Playable Director：開始時に自動再生し、19秒で最後の構図を保持します。

## 移動経路を編集する

1. Hierarchyで Dolly Path 01/02/03 - Edit Spline を選択します。
2. SceneビューのSpline編集ツールでノット（制御点）と接線ハンドルを動かします。
3. 高さや曲がり方もノットの位置で調整できます。経路全体を動かす場合は親の Orbit Center (0, 30, 0) を動かします。
4. Timelineの再生ヘッドを動かし、Gameビューで構図を確認してSceneを保存します。

初期の経路は以前の円周移動を引き継いだ、9ノットの開いた円弧です。経路をカメラの子にするとカメラの移動で経路も動くため、現在の親子関係を保ってください。

## 移動速度を編集する

Timelineの Dolly 01/02/03 - Spline Position のクリップをAnimationウィンドウで開き、Cinemachine Spline DollyのPosition（内部名 m_SplineSettings.Position）のキーを編集します。

- 既存の Orbit_01.anim、Orbit_02.anim、Orbit_03.anim を継続使用しています。ファイル名は残していますが、内容はSpline Positionのアニメーションです。
- 初期値は0秒で0、19秒で1。Normalizedの0が経路の始点、1が終点です。
- キーの時間とカーブで速度、停止、逆方向への移動を調整できます。
- Automatic DollyとDampingは無効です。Timelineが位置を制御します。
- 演出を延長する場合は、ショットと移動クリップの長さ、終端キーの時刻を合わせて変更してください。

## ショット・注視点・画角を編集する

1. MapStartCamera - Select for Timeline を選び、Window > Sequencing > Timeline を開きます。右上の鍵をロックすると選択を変えても表示を維持できます。
2. Camera Sequence - overlap to blend のショット本体を動かすと開始時刻、端を動かすと長さが変わります。
3. ショットを重ねるとブレンド、隙間なく並べるとカットです。空白は作らないでください。

初期設定は01が0–7秒、02が6–13秒、03が12–19秒。6–7秒と12–13秒でブレンドします。ブレンド中の出力位置は2台の補間位置であり、各Spline上に限定されません。

注視点は各カメラのLook At Target、画角はLens > Field Of Viewで編集します。経路から独立した注視設定です。

カメラを増やす場合はカメラと経路をそれぞれ複製し、Spline DollyのSplineを複製した経路へ割り当てます。Cinemachine Trackにショット、Animation Trackに複製したAnimatorと移動クリップを割り当てます。個別に速度を変える場合はアニメーションも複製してください。新規アセットは Assets/Member/Sawada/Cordex/ 配下に保存します。

プレイヤーカメラへの受け渡しは未実装です。

## 公式資料

- Spline Dolly：https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineSplineDolly.html
- Hard Look At：https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineHardLookAt.html
- Timeline：https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-timeline.html
