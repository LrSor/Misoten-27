using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class RotateObject : MiniGameObject
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

    private int m_rotatedCount = 0;

    // 疑似位置計算用
    private Vector3 m_velocity = Vector3.zero;
    private Vector3 m_position = Vector3.zero;

    // 前回の加速度
    private Vector3 m_prevAccel = Vector3.zero;

    // 円判定用
    private float m_circleAccumulatedAngle = 0f;
    private const float CIRCLE_THRESHOLD = 360f;

    // ノイズ除去用しきい値（小さすぎる差分は無視）
    private const float ACCEL_DIFF_THRESHOLD = 0.18f;

#if UNITY_EDITOR
    //デバッグ用に追加する変数はここに記載
    private int m_keyboardStep = 0;   // W→D→S→A の進行状況を管理
#endif

    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        m_rotatedCount = 0;
        m_velocity = Vector3.zero;
        m_position = Vector3.zero;
        m_prevAccel = Vector3.zero;
        m_circleAccumulatedAngle = 0f;

        transform.localEulerAngles = Vector3.zero;


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

        transform.localEulerAngles = Vector3.zero;
    }

    //オブジェクト更新関数
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
        // 現在の加速度を取得
        //------------------------------------------------------------
        Vector3 accel = m_joyconL.GetAccel();

        //------------------------------------------------------------
        // 前回との差分（Δaccel）を計算
        //------------------------------------------------------------
        Vector3 accelDiff = accel - m_prevAccel;

        //------------------------------------------------------------
        // 身体の前で円を描く場合は X-Z 平面の動きが重要
        // → Y 軸（上下方向）はノイズが多いので無視する
        //------------------------------------------------------------
        accelDiff.y = 0f;

        // ノイズ除去：差分が小さすぎる場合は無視
        if (accelDiff.magnitude < ACCEL_DIFF_THRESHOLD)
        {
            accelDiff = Vector3.zero;
        }

        //------------------------------------------------------------
        // Δaccel を積分して疑似位置を計算
        //------------------------------------------------------------
        m_velocity += accelDiff * Time.deltaTime;
        m_position += m_velocity * Time.deltaTime;

        //------------------------------------------------------------
        // 円判定：位置ベクトルの角度変化を積算
        //------------------------------------------------------------
        float angle = Mathf.Atan2(m_position.x, m_position.z) * Mathf.Rad2Deg / 10;
        float deltaAngle = Mathf.Abs(angle);

        m_circleAccumulatedAngle += deltaAngle;
        //m_circleAccumulatedAngle /= 10;

        //test
        //オブジェクトの回転
        transform.localEulerAngles = new Vector3(
                transform.localEulerAngles.x,
                transform.localEulerAngles.y,
                -m_circleAccumulatedAngle
            );

        //------------------------------------------------------------
        // 360°以上なら円を描いたと判定
        //------------------------------------------------------------
        if (m_circleAccumulatedAngle >= CIRCLE_THRESHOLD)
        {
            m_rotatedCount += 1;
            Debug.Log("円を描いた回数: " + m_rotatedCount);

            // リセット
            m_circleAccumulatedAngle = 0f;
            m_position = Vector3.zero;
            m_velocity = Vector3.zero;
        }

        //------------------------------------------------------------
        // 次フレームのために加速度を保存
        //------------------------------------------------------------
        m_prevAccel = accel;
    }

    public int GetRotatedCount()
    {
        return m_rotatedCount;
    }
}
