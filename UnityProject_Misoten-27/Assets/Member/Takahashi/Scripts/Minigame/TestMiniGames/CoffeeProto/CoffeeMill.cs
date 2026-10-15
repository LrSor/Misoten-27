using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class CoffeeMill: MiniGameObject
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

    [Header("オブジェクトのデフォルト位置")]//
    [SerializeField] Transform m_defaultPosition;
    //オブジェクトが移動した後の位置を保持
    Vector3 m_movePosition;

    //前回の加速度を保持
    Vector3 m_prevAccel;

    //一周するごとに1加算
    private int m_rotatedCount = 0;

    //============================================================
    // 追加メンバ変数（速度を保持）
    //============================================================
    private Vector2 m_velocity = Vector2.zero;   // Y,Z軸の速度
    private float m_sensitivity = 0.05f;         // 加速度→速度変換の係数（調整可）

#if UNITY_EDITOR
    //デバッグ用に追加する変数はここに記載
    
#endif

    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う

        transform.position = m_defaultPosition.position;
        m_movePosition = Vector3.zero;

        m_prevAccel = Vector3.zero;

        m_rotatedCount = 0;
        m_velocity = Vector2.zero;

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
        m_joyconL = null;

        transform.position = m_defaultPosition.position;
        m_movePosition = Vector3.zero;

        m_prevAccel = Vector3.zero;

        m_rotatedCount = 0;
        m_velocity = Vector2.zero;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
#if UNITY_EDITOR
        //bool hasJoycon = (m_joyconL != null);
        ////ジョイコンが接続されている場合、キーボード操作処理を飛ばしジョイコン操作を行う
        //if (!hasJoycon)
        //{
        //    //デバッグ用のキーボード操作は全てここに記載
        //    //============================================================
        //    // デバッグ用キーボード操作
        //    // W → D → S → A の順に押されたら 1 回転とみなす
        //    //============================================================

        //    // 現在のステップに応じて必要なキーを判定
        //    switch (m_keyboardStep)
        //    {
        //        case 0:
        //            if (Keyboard.current.dKey.isPressed)
        //            {
        //                m_keyboardStep = 1;
        //            }
        //            break;

        //        case 1:
        //            if (Keyboard.current.sKey.isPressed)
        //            {
        //                m_keyboardStep = 2;
        //            }
        //            break;

        //        case 2:
        //            if (Keyboard.current.aKey.isPressed)
        //            {
        //                m_keyboardStep = 3;
        //            }
        //            break;

        //        case 3:
        //            if (Keyboard.current.wKey.isPressed)
        //            {
        //                m_rotatedCount += 1;

        //                // ステップをリセット
        //                m_keyboardStep = 0;
        //            }
        //            break;
        //    }

        //    // オブジェクトの回転演出（任意）
        //    // キーボード操作時は積算角度を使わないため簡易的に回転
        //    transform.localEulerAngles = new Vector3(
        //        transform.localEulerAngles.x,
        //        transform.localEulerAngles.y,
        //        m_keyboardStep * -90f // W→D→S→A の進行に応じて 0,90,180,270
        //    );
        //    return;
        //}

#endif
        //以下に処理を記載
        //------------------------------------------------------------
        // JoyCon の加速度取得（X軸は無視）
        //------------------------------------------------------------
        Vector3 accel = m_joyconL.GetAccel();
        float accelY = accel.y;
        float accelZ = accel.z;

        //------------------------------------------------------------
        // 加速度 → 速度へ変換（疑似積分）
        //------------------------------------------------------------
        // 加速度の変化量を使うことでノイズを軽減
        float deltaY = accelY - m_prevAccel.y;
        float deltaZ = accelZ - m_prevAccel.z;

        m_velocity.x += deltaY * m_sensitivity;   // 左右(Y)
        m_velocity.y += deltaZ * m_sensitivity;   // 前後(Z)

        // 速度の減衰（慣性を抑える）
        m_velocity *= 0.9f;

        //------------------------------------------------------------
        // 速度 → 位置へ変換
        //------------------------------------------------------------
        m_movePosition.x += m_velocity.x;
        m_movePosition.z += m_velocity.y;

        //------------------------------------------------------------
        // 移動範囲を ±5 に制限
        //------------------------------------------------------------
        m_movePosition.x = Mathf.Clamp(m_movePosition.x, -5f, 5f);
        m_movePosition.z = Mathf.Clamp(m_movePosition.z, -5f, 5f);

        //------------------------------------------------------------
        // 実際のオブジェクト位置に反映
        //------------------------------------------------------------
        transform.position = m_defaultPosition.position + m_movePosition;

        //------------------------------------------------------------
        // 前回の加速度を更新
        //------------------------------------------------------------
        m_prevAccel = accel;

    }

    public int GetRotatedCount()
    {
        return m_rotatedCount;
    }
}
