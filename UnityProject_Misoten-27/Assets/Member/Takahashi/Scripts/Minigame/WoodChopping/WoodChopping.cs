using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class WoodChopping : MiniGame
{
    protected override void Start()
    {
        //基底クラス内処理
        base.Start();


    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        m_remainingSeconds -= Time.deltaTime; // 変更 Sawada
    }

    public override void Activate()
    {
        base.Activate();

        m_stars = 2;                // 例 2つ星を設定 // 変更 Sawada
        m_remainingSeconds = 20f;   // 例 20秒を設定
    }
}
