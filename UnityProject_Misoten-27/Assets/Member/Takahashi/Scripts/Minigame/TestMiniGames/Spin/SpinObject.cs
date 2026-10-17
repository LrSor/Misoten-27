using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpinObject : MiniGameObject
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

    [SerializeField] MiniGame m_miniGame;

    private int m_rotatedCount = 0;
    // ジャイロ積分用
    private float m_accumulatedRotationZ = 0f;
    // 1回転とみなす閾値（360°）
    private const float ROTATION_THRESHOLD = 180f;

#if UNITY_EDITOR
    //デバッグ用に追加する変数はここに記載
    private int m_keyboardStep = 0;   // W→D→S→A の進行状況を管理
#endif

    public override void Activate()
    {
        m_rotatedCount = 0;
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

    public override void Deactivate()
    {
        m_joyconL = null;

        m_rotatedCount = 0;
        m_accumulatedRotationZ = 0f;
        transform.localEulerAngles = Vector3.zero;
        m_keyboardStep = 0;
    }

    public override void UpdateObject()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joyconL != null);
        //ジョイコンが接続されている場合、キーボード操作処理を飛ばしジョイコン操作を行う
        if (!hasJoycon)
        {
            //デバッグ用のキーボード操作は全てここに記載
            //============================================================
            // デバッグ用キーボード操作
            // W → D → S → A の順に押されたら 1 回転とみなす
            //============================================================

            // 現在のステップに応じて必要なキーを判定
            switch (m_keyboardStep)
            {
                case 0:
                    if (Keyboard.current.dKey.isPressed)
                    {
                        m_keyboardStep = 1;
                    }
                    break;

                case 1:
                    if (Keyboard.current.sKey.isPressed)
                    {
                        m_keyboardStep = 2;
                    }
                    break;

                case 2:
                    if (Keyboard.current.aKey.isPressed)
                    {
                        m_keyboardStep = 3;
                    }
                    break;

                case 3:
                    if (Keyboard.current.wKey.isPressed)
                    {
                        m_rotatedCount += 1;

                        // ステップをリセット
                        m_keyboardStep = 0;
                    }
                    break;
            }

            // オブジェクトの回転演出（任意）
            // キーボード操作時は積算角度を使わないため簡易的に回転
            transform.localEulerAngles = new Vector3(
                transform.localEulerAngles.x,
                transform.localEulerAngles.y,
                m_keyboardStep * -90f // W→D→S→A の進行に応じて 0,90,180,270
            );
            return;
        }

#endif

        //------------------------------------------------------------
        // ジャイロ値（角速度）を取得
        //------------------------------------------------------------
        // GetGyro() は「角速度(rad/s)」を返す。
        // 角速度を時間で積分すると「角度(rad)」になるため、
        // Time.deltaTime を掛けて角度に変換する。
        Vector3 gyro = m_joyconL.GetGyro();

        //------------------------------------------------------------
        // Z軸の角速度を積分して回転角度を推定
        //------------------------------------------------------------
        // rad → degree に変換するため Mathf.Rad2Deg を掛ける。
        float deltaAngleZ = gyro.z * Mathf.Rad2Deg * Time.deltaTime;

        // 積算角度に加算
        m_accumulatedRotationZ += Mathf.Abs(deltaAngleZ);

        //オブジェクトの回転
        transform.localEulerAngles = new Vector3(
                transform.localEulerAngles.x,
                transform.localEulerAngles.y,
                m_accumulatedRotationZ * (360/ROTATION_THRESHOLD)
            );

        //------------------------------------------------------------
        // 360°以上回転したら 1 回転と判定
        //------------------------------------------------------------
        if (m_accumulatedRotationZ >= ROTATION_THRESHOLD)
        {
            m_rotatedCount += 1;

            // カウント後は積算角度をリセット
            m_accumulatedRotationZ = 0f;
        }
    }

    public int GetRotatedCount()
    {
        return m_rotatedCount;
    }
}
