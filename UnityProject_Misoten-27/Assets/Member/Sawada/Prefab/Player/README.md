# リギング済み衣装の自動接続

`AutoRigSlot` はCategory設定を持たず、子孫のSkinnedMeshRendererを共通Rigの同名の骨へ接続します。上着・ズボン・靴・髪は同じ処理です。骨のないMeshRendererの固定・位置補正・左右推定は行いません。

## Player_ver3の構成

```text
PlayerBody/Model
├─ Sawada_Base_HeadBodyRig
│  └─ Common_Rig
└─ Wardrobe                         AutoRigWardrobe
   ├─ TemporaryOutfit               AutoRigSlot
   └─ Outfit                        AutoRigSlot
```

`TemporaryOutfit` と `Outfit` のどちらでも、配下の衣装を同じように接続します。名前は整理用で、表示は各GameObjectのActiveで管理します。既存のTops・Pants・Shoes・Hairグループと配置済みの衣装も維持しています。既存グループのAutoRigSlotもCategoryなしで動作します。

WardrobeをModel直下へ置く場合は、AutoRigWardrobeのCommon Rigに素体のCommon_Rig、または骨を含む素体モデルのrootを指定してください。素体との座標・スケールの対応も維持します。Player_ver3の現在の参照・Transformは変更していません。

## 使い方

1. AutoRigSlotのあるEmptyの子として、衣装のFBXまたはモデルPrefab全体を配置します。衣装自身のRigも含めます。
2. 共通Rigと同じ骨名・階層・レスト姿勢・単位でスキニング済みのモデルを使います。Import SettingsはGeneric、Optimize Game Objectsは無効にします。
3. 編集中・Play中とも自動接続します。深い階層への追加も最大約0.2秒で検出します。必要ならコンポーネントのメニューからSynchronize attachments nowを実行できます。
4. TemporaryOutfitからOutfitへ移動した場合も接続し直します。カテゴリや左右の指定は不要です。複数の衣装がActiveなら同時に表示します。
5. 外へ移動した場合、またはAutoRigSlotを無効にした場合は、衣装自身の元の骨参照とAnimatorの状態へ戻します。
6. 永続化する場合は対象PrefabまたはSceneを保存します。元の衣装FBX・Prefabへ変更をApplyする必要はありません。

## 壁越しのPlayer表示

- Layer8はPlayer、Layer9はCharacter、Layer10はItemPreviewです。
- 共通Rigへ接続できた衣装のSkinnedMeshRendererのGameObjectは、自動でPlayerレイヤーになります。取り外し・Slot無効化時には、接続前のレイヤーへ戻します。Collider・Camera・骨のレイヤーは変更しません。
- CharacterBehindのDrawCharacterBehindとCharacterはPlayerのみを対象にし、Opaque Layer MaskではPlayerを除外します。両FeatureはAfterRenderingOpaquesの順序（青色→通常）を維持します。
- Player_ver3とPlayer_cordexのプレビューCameraはItemPreviewのみを描画し、Renderer 0（PC_Renderer）を使用します。PlayerInventoryUIが生成するプレビューの描画オブジェクトもItemPreviewへ設定します。
- 半透明マテリアルは現在のOpaque用Render Objectsによる青色表示の対象外です。

## 処理と制約

- Rendererのbones/rootBoneを共通Rigへ割り当て、衣装側のAnimatorを無効化します。メッシュ・マテリアル・ウェイト・Transformは変更しません。
- 保存済み衣装の元の骨参照・Animator状態は引き続き保持します。
- 骨対応配列をキャッシュし、接続済みのRendererへの繰り返しの配列取得・再設定を省きます。階層検出用のListも再利用します。
- 骨が見つからない場合は、そのRendererを元の状態に保ち、Consoleへ警告します。異なる骨構成・レスト姿勢のモデルを自動リターゲットする機能ではありません。
- 衣装が画面外でも骨の差し替え後に正しく更新されるよう、接続中のupdateWhenOffscreenは既存どおり有効にし、取り外すと元の設定へ戻します。
- ネットワーク生成時も自動接続処理は有効です。衣装の種類・Active状態の端末間共有は、別途ゲーム側で管理します。

## Player_ver3のアニメーション

表示モデルは`Assets/Member/Sawada/FBX/Player/Sawada_Base_20261116.fbx`のHead・Body・Rigに統一しています。Player_ver3・Player_cordex・CordexSceneはこのFBXを参照します。FBXのGeneric設定は維持し、モデル上のAnimatorには`Assets/Member/Sawada/Avatar/PlayerVer3Humanoid.asset`と`TripoLocomotion.controller`を使用します。Root Motionは無効です。移動・カメラ・当たり判定・インベントリ・Swapなどの既存設定は維持しています。
