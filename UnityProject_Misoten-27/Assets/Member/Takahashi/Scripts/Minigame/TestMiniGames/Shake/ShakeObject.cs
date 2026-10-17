using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShakeObject : MiniGameObject
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joyconL;
    //private Joycon m_joyconR;
    //============================================================

    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [SerializeField] private float accelThresholdDown = -1.2f;  // 下方向の加速度しきい値
    [SerializeField] private float accelThresholdUp = 1.2f;     // 上方向の加速度しきい値

    private bool m_isDownSwingFlag = false;   // 下振りを検知したか
    private int m_shakedCount = 0;              // スコア


    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        m_isDownSwingFlag = false;
        m_shakedCount = 0;

        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
#if UNITY_EDITOR
        JoyconManager joyconManager = GameObject.FindFirstObjectByType<JoyconManager>();
        if (joyconManager.GetIsKeyboardPlay())
        {
            return;
        }
#endif

        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joyconL = m_joycons.Find(c => c.isLeft);
        //m_joyconR = m_joycons.Find(c => !c.isLeft);
        //============================================================
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        //メンバ変数の初期化をここでも行う
        m_joyconL = null;
        m_isDownSwingFlag = false;
        m_shakedCount = 0;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joyconL != null);
        if (!hasJoycon)
        {
            //デバッグ用キーボード操作を以下に記載
            if (Keyboard.current.wKey.isPressed && m_isDownSwingFlag)
            {
                m_shakedCount++;
                m_isDownSwingFlag = false;
            }
            if (Keyboard.current.sKey.isPressed && !m_isDownSwingFlag)
            {
                m_isDownSwingFlag = true;
            }

            return;
        }
#endif

        //ジョイコンを用いた操作を記載
        Vector3 accel = m_joyconL.GetAccel();  // 加速度取得（縦持ち前提）

        // 下方向の振りを検知
        if (!m_isDownSwingFlag && accel.y < accelThresholdDown)
        {
            m_isDownSwingFlag = true;
        }

        // 上方向の振りを検知 → スコア加算
        if (m_isDownSwingFlag && accel.y > accelThresholdUp)
        {
            m_shakedCount++;
            m_isDownSwingFlag = false;
        }
    }

    public int GetShakedCount()
    {
        return m_shakedCount;
    }
}
