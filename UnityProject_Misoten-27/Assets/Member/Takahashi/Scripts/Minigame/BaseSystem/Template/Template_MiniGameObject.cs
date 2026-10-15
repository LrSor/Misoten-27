using System.Collections.Generic;
using System;
using UnityEngine;

//ジョイコンを使用しないミニゲーム用オブジェクトのテンプレート
public class Template_MiniGameObject : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;


    //オブジェクト初期化関数
    public override void Activate()
    {
        
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {

    }
}
