using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class PointerObject : MiniGameObject
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joycon;
    //============================================================

    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;


    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う


        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
        //=====================================================
        JoyconManager joyconManager = GameObject.FindFirstObjectByType<JoyconManager>();
        if (joyconManager.GetIsKeyboardPlay())
        {
            return;
        }
        //=====================================================

        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joycon = m_joycons.First();
        //============================================================
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        m_joycon = null;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        //=====================================================
        bool hasJoycon = (m_joycon != null);
        if (!hasJoycon)
        {
            //デバッグ用キーボード操作を以下に記載

            return;
        }
        //=====================================================

        //ジョイコンを用いた操作を記載
        // Bボタンでセンター位置のリセット
        if (m_joycon.GetButtonDown(Joycon.Button.DPAD_DOWN))
        {
            m_joycon.Recenter();
        }

        gameObject.transform.rotation = m_joycon.GetVector();
    }
}
