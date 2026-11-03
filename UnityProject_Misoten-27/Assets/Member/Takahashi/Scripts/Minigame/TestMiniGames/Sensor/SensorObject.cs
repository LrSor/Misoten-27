using System.Collections.Generic;
using System;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;

public class SensorObject : MiniGameObject
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

    // ジャイロ積分角度
    private Vector3 m_integratedAngle;

    [SerializeField] int m_testNumber = 0;


    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        m_currentEuler=Vector3.zero;
        m_integratedAngle=Vector3.zero;

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

    //オブジェクト終了関数
    public override void Deactivate()
    {
        m_joycon = null;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joycon != null);
        if (!hasJoycon)
        {
            //デバッグ用キーボード操作を以下に記載

            return;
        }
#endif

        switch(m_testNumber)
        {
            case 0:
                First();
                break;
            case 1:
                Second();
                break;
            case 2:
                Gyro(); 
                break;
            case 3:
                Laser(); 
                break;
            default:
                break;
        }
        
        
    }

    private void First()
    {
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

    private void Second()
    {
        Vector3 accel = m_joycon.GetAccel();

        //------------------------------------------------------------
        // 加速度 → 角度変換（X/Y/Z）
        //------------------------------------------------------------
        float targetX = Mathf.Lerp(90f, -90f, Mathf.InverseLerp(-1f, 1f, accel.x));
        float targetY = Mathf.Lerp(-90f, 90f, Mathf.InverseLerp(1f, -1f, accel.y));
        float targetZ = Mathf.Lerp(-90f, 90f, Mathf.InverseLerp(-1f, 1f, accel.z));

        //------------------------------------------------------------
        // 滑らかに補間
        //------------------------------------------------------------
        m_currentEuler.x = Mathf.LerpAngle(m_currentEuler.x, targetX, Time.deltaTime * m_rotateSmooth);
        //m_currentEuler.y = Mathf.LerpAngle(m_currentEuler.y, targetY, Time.deltaTime * m_rotateSmooth);
        m_currentEuler.z = Mathf.LerpAngle(m_currentEuler.z, targetZ, Time.deltaTime * m_rotateSmooth);

        //------------------------------------------------------------
        // オブジェクトに反映
        //------------------------------------------------------------
        transform.localEulerAngles = m_currentEuler;
    }

    private void Gyro()
    {
        //------------------------------------------------------------
        // ジャイロ角速度(rad/s)を取得
        //------------------------------------------------------------
        Vector3 gyro = m_joycon.GetGyro();

        //------------------------------------------------------------
        // 角速度を積分して角度に変換
        //------------------------------------------------------------
        Vector3 deltaAngle = gyro * Mathf.Rad2Deg * Time.deltaTime;

        // 積算角度に加算
        m_integratedAngle += deltaAngle;

        //------------------------------------------------------------
        // 滑らかに補間してオブジェクトに反映
        //------------------------------------------------------------
        Vector3 current = transform.localEulerAngles;
        current.x = Mathf.LerpAngle(current.x, m_integratedAngle.x, Time.deltaTime * m_rotateSmooth);
        current.y = Mathf.LerpAngle(current.y, m_integratedAngle.y, Time.deltaTime * m_rotateSmooth);
        current.z = Mathf.LerpAngle(current.z, m_integratedAngle.z, Time.deltaTime * m_rotateSmooth);

        transform.localEulerAngles = current;
    }

    private void Laser()
    {
        Vector3 accel = m_joycon.GetAccel();

        //------------------------------------------------------------
        // 持ち方に合わせた角度変換
        //------------------------------------------------------------
        // 左右の傾き（Yaw）
        float yaw = Mathf.Lerp(-90f, 90f, Mathf.InverseLerp(1f, -1f, accel.y));

        // 上下の傾き（Pitch）
        float pitch = Mathf.Lerp(-90f, 90f, Mathf.InverseLerp(1f, -1f, accel.x));

        //------------------------------------------------------------
        // 滑らかに補間
        //------------------------------------------------------------
        m_currentEuler.x = Mathf.LerpAngle(m_currentEuler.x, pitch, Time.deltaTime * m_rotateSmooth);
        m_currentEuler.y = Mathf.LerpAngle(m_currentEuler.y, yaw, Time.deltaTime * m_rotateSmooth);

        // Roll（Z軸）はレーザーポインターでは使わない

        transform.localEulerAngles = m_currentEuler;
    }

    private void GyroLaser()
    {
        //ジョイコンを用いた操作を記載(以下に回転処理を生成)
        Vector3 accel = m_joycon.GetAccel();
        Vector3 gyro = m_joycon.GetGyro();

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
