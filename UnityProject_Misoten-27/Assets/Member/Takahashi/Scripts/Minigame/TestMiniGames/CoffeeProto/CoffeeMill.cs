using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Linq;

public class CoffeeMill: MiniGameObject
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

    [Header("オブジェクトのデフォルト位置を指定するオブジェクト")]//
    [SerializeField] Transform m_defaultPositionObject;
    private Vector3 m_defaultPosition;

    [Header("移動範囲")]
    [SerializeField] float m_moveLange = 0;

    //オブジェクトが移動した後の位置を保持
    Vector3 m_movePosition;
    Vector3 m_prevPosition;
    
    //前回の加速度を保持
    Vector3 m_prevAccel;

    //一周するごとに1加算
    private int m_rotatedCount = 0;

    //============================================================
    // 追加メンバ変数（速度を保持）
    //============================================================
   private Vector2 m_velocity = Vector2.zero;   // Y,Z軸の速度
   [Header("移動速度補正")]
   [SerializeField] private float m_sensitivity = 0.05f;
   [Header("ジョイコン加速度の感度")]
   [SerializeField] private float m_accelSensitivity = 0.05f;
   
   [Header("テスト用Gyro感度")]
   [SerializeField] private float m_gyroSensitivity = 1.0f;
   [Header("テスト用Gyro移動距離(なるべく小さい値)")]
   [SerializeField] private float m_gyroMoveLange = 0.1f;

    // ジャイロ積分用
    private float m_accumulatedRotationZ = 0f;
    // 1回転とみなす閾値（360°）
    private const float ROTATION_THRESHOLD = 180f;

    [Header("非回転時減衰速度(回していない間に毎フレーム幾つ下がるか)")]
    [SerializeField] int m_attenuationSpeed = 0;




#if UNITY_EDITOR
    //デバッグ用に追加する変数はここに記載
    private int m_keyboardStep = 0;
#endif

    private void Start()
    {
        m_defaultPosition = m_defaultPositionObject.position;

        Debug.Log("defaultPos ->" + m_defaultPosition);
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う

        transform.position = m_defaultPosition;
        m_movePosition = Vector3.zero;
        m_prevPosition = Vector3.zero;
        
        m_prevAccel = Vector3.zero;

        m_rotatedCount = 0;
        m_velocity = Vector2.zero;

        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
#if UNITY_EDITOR
        JoyconManager joyconManager = GameObject.FindFirstObjectByType<JoyconManager>();
        if (joyconManager.GetIsKeyboardPlay())
        {
            m_keyboardStep = 0;
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

        transform.position = m_defaultPosition;
        m_movePosition = Vector3.zero;
        //
        m_prevAccel = Vector3.zero;

        m_rotatedCount = 0;
        m_velocity = Vector2.zero;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joycon != null);
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

        MoveOb();
        return;

        //横持ちをして前後左右に傾ける事でオブジェクトを移動させるテスト
        //MoveBySlope();
        MoveByAccelAndGyro();
        return;
        
        m_rotatedCount -= m_attenuationSpeed;
        if ((m_rotatedCount < 0))
        {
            m_rotatedCount = 0;
        }

        //投げた際の処理
        if (m_joycon.GetButtonUp(Joycon.Button.SHOULDER_1))
        {
            
        }



            //------------------------------------------------------------
            // ジャイロ値（角速度）を取得
            //------------------------------------------------------------
            // GetGyro() は「角速度(rad/s)」を返す。
            // 角速度を時間で積分すると「角度(rad)」になるため、
            // Time.deltaTime を掛けて角度に変換する。
            Vector3 gyro = m_joycon.GetGyro();

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
                m_accumulatedRotationZ * (360 / ROTATION_THRESHOLD),
                transform.localEulerAngles.z
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

    
    private void UseAccel()
    {
        //以下に処理を記載

        //キー入力で初期地へ移動
        if (m_joycon.GetButtonDown(Joycon.Button.SHOULDER_1))
        {
            transform.position = m_defaultPosition;
        }

        //------------------------------------------------------------
        // JoyCon の加速度取得（X軸は無視）
        //------------------------------------------------------------
        Vector3 accel = m_joycon.GetAccel();
        float accelY = accel.y;
        float accelZ = accel.z;

        //------------------------------------------------------------
        // 加速度 → 速度へ変換（疑似積分）
        //------------------------------------------------------------
        // 加速度の変化量を使うことでノイズを軽減
        float deltaY = accelY - m_prevAccel.y;
        float deltaZ = accelZ - m_prevAccel.z;

        //------------------------------------------------------------
        // 感度調整(指定未満の加速度ならば、移動を取り消す)
        //------------------------------------------------------------
        if (-m_accelSensitivity < accel.y && accel.y < m_accelSensitivity)
        {
            deltaY = 0;
            Debug.Log(deltaY + ":" + m_sensitivity + "未満なので無効");
        }
        if (-m_accelSensitivity < accel.z && accel.z < m_accelSensitivity)
        {
            deltaZ = 0;
        }

        //移動速度の補正
        m_velocity.x += deltaZ * m_sensitivity;
        m_velocity.y -= deltaY * m_sensitivity;

        // 速度の減衰（慣性を抑える）
        m_velocity *= 0.9f;

        //------------------------------------------------------------
        // 速度 → 位置へ変換
        //------------------------------------------------------------
        m_movePosition = Vector3.zero;
        m_movePosition.x += m_velocity.x;
        //m_movePosition.z += m_velocity.y;

        //------------------------------------------------------------
        // 実際のオブジェクト位置に反映
        //------------------------------------------------------------
        Vector3 setPosition = m_movePosition;
        setPosition += transform.position;
        Debug.Log(setPosition);

        //オブジェクトが移動範囲外に出る場合、範囲内へ納める
        Vector3 clampPosition = setPosition - m_defaultPosition;
        clampPosition.x = Math.Clamp(clampPosition.x, -m_moveLange, m_moveLange);
        clampPosition.z = Math.Clamp(clampPosition.z, -m_moveLange, m_moveLange);
        transform.position = m_defaultPosition + clampPosition;
        //
        //
        //Debug.Log(clampPosition);
        //------------------------------------------------------------
        // 前回の加速度を更新
        //------------------------------------------------------------
        m_prevAccel = accel;
    }

    private void MoveOb()
    {
        //キー入力で初期地へ移動
        if (m_joycon.GetButtonDown(Joycon.Button.SHOULDER_1))
        {
            transform.position = m_defaultPosition;
        }

        Vector3 accel = m_joycon.GetAccel();
        Vector3 gyro = m_joycon.GetGyro();
        Vector3 move = Vector3.zero;
        if(gyro.y > m_gyroSensitivity)
        {
            move.z = m_gyroMoveLange;
        }
        else if(gyro.y<-m_gyroSensitivity)
        {
            move.z = -m_gyroMoveLange;
        }
        

        if (gyro.z > m_gyroSensitivity)
        {
            move.x = m_gyroMoveLange;
        }
        else if (gyro.z < -m_gyroSensitivity)
        {
            move.x = -m_gyroMoveLange;
        }

        transform.position += move;
    }
   
    private void MoveBySlope()
    {
        transform.position = m_defaultPosition;
        //------------------------------------------------------------
        // JoyCon の加速度取得（X軸は無視）
        //------------------------------------------------------------
        Vector3 accel = m_joycon.GetAccel();

        //ジョイコンがRの場合、加速度YとZの数値を反転
        if (!m_joycon.isLeft)
        {
            accel.x = -accel.x;
            accel.y = -accel.y;
        }

        float accelX = accel.x;
        float accelY = accel.y;

        Vector3 movePosition = Vector3.zero;
        movePosition.x = accelX * 5;
        movePosition.y = accelY * 5;

        
        transform.position += movePosition;
    }

    private void MoveByAccelAndGyro()
    {
        //transform.position = m_defaultPosition;

        //位置情報補正
        Vector3 pos = transform.position;
        pos.y = m_defaultPosition.y;

        //------------------------------------------------------------
        // JoyCon の加速度取得（X軸は無視）
        //------------------------------------------------------------
        Vector3 accel = m_joycon.GetAccel();
        Vector3 gyro = m_joycon.GetGyro();

        //ジョイコンがRの場合、加速度YとZの数値を反転
        if (!m_joycon.isLeft)
        {
            accel.z = -accel.z;
            accel.y = -accel.y;

            gyro.y = -gyro.y;
            
        }

        float accelX = accel.x;
        float accelY = accel.y;

        Vector3 movePosition = Vector3.zero;
        movePosition.y = accelX * 5;

        //Gyro移動
        if (gyro.z > m_gyroSensitivity)
        {
            movePosition.x = m_gyroMoveLange;
        }
        else if (gyro.z < -m_gyroSensitivity)
        {
            movePosition.x = -m_gyroMoveLange;
        }

        pos += movePosition;
        transform.position = pos;
    }
}
