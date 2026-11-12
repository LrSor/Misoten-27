# MapStartCamera テスト（Unity 標準コンポーネント）

`MapStartCamera.unity` を開いて Play すると、回転中心 `(0, 30, 0)` の周りを3台のカメラが回転し、地上のマップ中央 `(0, 0, 0)` を注視します。独自 C# スクリプトは使用していません。

## 使用している標準機能

- Cinemachine Camera: 撮影カメラの設定。
- Cinemachine Orbital Follow (Position Control): Tracking Target を中心にした回転位置。
- Cinemachine Hard Look At (Rotation Control): Look At Target への注視。
- Animator と Timeline の Animation Track: Orbital Follow の Horizontal Axis > Value をアニメーション。
- Cinemachine Brain と Timeline の Cinemachine Track: カメラの選択とブレンド。
- Playable Director: シーン開始時に自動再生。Wrap Mode = Hold で終了構図を保持。

## 切り替えを編集する

1. Hierarchy の `MapStartCamera - Select for Timeline` を選択。
2. `Window > Sequencing > Timeline` を開く。
3. `Camera Sequence - overlap to blend` のショットを移動して切り替え時刻を変更。端をドラッグして長さを変更。
4. ショット同士を重ねるとブレンド、隙間なく並べるとカット。空白区間では優先度で選ばれたカメラに戻るため、連続した演出では隙間を作らないでください。
5. 再生ヘッドを動かすと Game ビューでプレビューできます。

初期設定は 01 が 0–7 秒、02 が 6–13 秒、03 が 12–19 秒。6–7 秒と 12–13 秒でブレンドし、19 秒で最後の構図を保持します。

## 回転と構図を編集する

- 回転中心: `Orbit Center (0, 30, 0)` の Transform。各カメラの Tracking Target に指定。
- 注視点: `Map LookAt (0, 0, 0)` の Transform。各カメラの Look At Target に指定。
- 距離: 各カメラの Orbital Follow > Radius。Sphere モードでは水平半径ではなく、中心からカメラまでの直線距離です。
- 仰角: Orbital Follow > Vertical Axis > Value。Radius と合わせて高さ・水平距離を調整。
- ズーム: Cinemachine Camera > Lens > Field Of View。

回転速度と開始角度は Timeline の `Orbit 01/02/03 - Horizontal Axis` の Animation Clip（`Orbit_01.anim` など）に記録しています。Animation ウィンドウで `Cinemachine Orbital Follow > Horizontal Axis.Value` のキーフレームを編集してください。値の変化量 ÷ 時間が回転速度で、逆回転は値を減らします。クリップを伸縮すると速度が変わります。

| カメラ | 水平半径 | 中心からの高さ | Sphere Radius | Vertical Axis | 水平軸キー（0秒 → 19秒） |
| --- | --- | --- | --- | --- | --- |
| 01 Wide Orbit | 125 | 65 | 約140.89 | 約27.47度 | 180 → 294度 |
| 02 Closer Orbit | 90 | 40 | 約98.49 | 約23.96度 | 220 → 372度 |
| 03 Reverse Orbit | 115 | 80 | 約140.09 | 約34.82度 | 380 → 285度 |

標準 Orbital Follow の水平軸は 0 度で -Z 側、180 度で +Z 側です。アニメーション中の水平軸は Animation Clip が制御するため、Inspector で開始値だけを変えてもアニメーションの値に戻ります。自動リセンタリングと入力制御は無効にしています。

演出時間を延長する場合は、Cinemachine のショットだけでなく3本の回転 Animation Clip も延長し、必要なキーフレームを追加してください。回転 Animation Track の終端以降は最後の角度を保持します。

カメラを増やす場合は既存カメラを複製し、Cinemachine Track にショットを追加して Virtual Camera を指定します。対応する Animation Track を作り、複製したカメラの Animator にバインドします。個別の動きが必要なら Animation Clip も複製してください。

## 公式資料

- Orbital Follow: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineOrbitalFollow.html
- Hard Look At: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineHardLookAt.html
- Timeline: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-timeline.html

今回の対象は独立したテスト Scene です。`testMapPlayerMove` 本体への組み込みと、演出後のプレイヤーカメラへの受け渡しは未実装です。既存 Scene、Prefab、Packages、ProjectSettings は変更していません。