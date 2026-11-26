using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

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

    [Header("デバッグ用:キーボード操作時の移動速度")]
    [SerializeField] float m_keyboardSpeed = 0.1f;


    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う


        if (IsNetworkControlled) { m_joycon = null; return; } // 変更 Sawada

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

        //ポインターの初期化
        m_joycon.Recenter();
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
        bool hasJoycon = (IsNetworkControlled || m_joycon != null); // 変更 Sawada
        if (!hasJoycon)
        {
            //デバッグ用キーボード操作を以下に記載

            Vector3 pos = Vector3.zero;


            if (Keyboard.current.wKey.isPressed)
            {
                pos.z -= m_keyboardSpeed;
            }
            if (Keyboard.current.sKey.isPressed)
            {
                pos.z += m_keyboardSpeed;
            }
            if (Keyboard.current.aKey.isPressed)
            {
                pos.x -= m_keyboardSpeed;
            }
            if (Keyboard.current.dKey.isPressed)
            {
                pos.x += m_keyboardSpeed;
            }

            gameObject.transform.position += pos;

            return;
        }
        //=====================================================

        //ジョイコンを用いた操作を記載
        // Bボタンでセンター位置のリセット
        if (!IsNetworkControlled && m_joycon.GetButtonDown(Joycon.Button.DPAD_DOWN)) // 変更 Sawada
        {
            m_joycon.Recenter();
        }

        gameObject.transform.rotation = (IsNetworkControlled ? NetworkInput.m_orientation : m_joycon.GetVector()); // 変更 Sawada
    }
}
