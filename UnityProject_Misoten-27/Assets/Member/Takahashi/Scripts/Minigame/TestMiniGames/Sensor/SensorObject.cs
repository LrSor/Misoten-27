using System.Collections.Generic;
using System;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;

public class SensorObject : MonoBehaviour
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

    [SerializeField] float m_rotateSmooth = 10f;   //角度補間の滑らかさ
    private Vector3 m_currentEuler;                //現在の角度キャッシュ

    [SerializeField] int m_testNumber = 0;


    //オブジェクト初期化関数
    void Start()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        m_currentEuler=Vector3.zero;

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

        m_joycon = m_joycons.First();
        //============================================================
    }

    //オブジェクト更新関数
    void Update()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joycon != null);
        if (!hasJoycon)
        {
            //デバッグ用キーボード操作を以下に記載

            return;
        }
#endif

        //ジョイコンを用いた操作を記載(以下に回転処理を生成)
        Vector3 accel = m_joycon.GetAccel();

        //------------------------------------------------------------
        // 加速度 → 角度変換
        //------------------------------------------------------------
        // X軸：1 → -90°, -1 → +90°
        float targetX = Mathf.Lerp(90f, -90f, Mathf.InverseLerp(-1f, 1f, accel.x));

        // Y軸：1 → +90°, -1 → -90°
        float targetY = Mathf.Lerp(-90f, 90f, Mathf.InverseLerp(1f, -1f, accel.y));

        //------------------------------------------------------------
        // 角度を滑らかに補間
        //------------------------------------------------------------
        m_currentEuler.x = Mathf.LerpAngle(m_currentEuler.x, targetX, Time.deltaTime * m_rotateSmooth);
        m_currentEuler.y = Mathf.LerpAngle(m_currentEuler.y, targetY, Time.deltaTime * m_rotateSmooth);

        //------------------------------------------------------------
        // オブジェクトに反映
        //------------------------------------------------------------
        transform.localEulerAngles = m_currentEuler;


    }
}
