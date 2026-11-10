using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Linq;

public class CowboyRope : MiniGameObject
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

    // ジャイロ積分用
    private float m_accumulatedRotationZ = 0f;
    // 1回転とみなす閾値（360°）
    private const float ROTATION_THRESHOLD = 180f;

    //回して溜めたパワー
    private float m_power = 0;
    [Header("投げるのに必要なパワー")]
    [SerializeField] float m_throwPower = 10;
    [Header("毎フレーム下がるパワー")]
    [SerializeField] float m_powerDown = 1;

    private bool m_throwFlag = false;


    [Header("現在溜まっているパワーを表示するUI")]
    [SerializeField] Text m_powerText;
    [Header("回す速さが足りない事を伝えるUI")]
    [SerializeField] Image m_notEnoughSpeed;
    [Header("ボタン入力を促すUI")]
    [SerializeField] Image m_pleasePressButton;


#if UNITY_EDITOR
    //デバッグ用に追加する変数はここに記載
    private int m_keyboardStep = 0;
#endif

    private void Start()
    {
        m_powerText.gameObject.SetActive(false);
        m_notEnoughSpeed.gameObject.SetActive(false);
        m_pleasePressButton.gameObject.SetActive(false);
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        m_power = 0;
        m_accumulatedRotationZ = 0;
        m_throwFlag = false;

        m_powerText.gameObject.SetActive(true);
        m_powerText.text = m_power.ToString("Power: 00");
        m_notEnoughSpeed.gameObject.SetActive(false);
        m_pleasePressButton.gameObject.SetActive(false);

        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
#if UNITY_EDITOR
        m_keyboardStep = 0;

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

        m_power = 0;
        m_notEnoughSpeed.gameObject.SetActive(false);
        m_pleasePressButton.gameObject.SetActive(false);
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
                        m_power += 1;

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
        m_notEnoughSpeed.gameObject.SetActive(false);
        m_pleasePressButton.gameObject.SetActive(false);

        //投げた際は専用の関数で更新
        if(m_throwFlag)
        {
            Thrown();
            return;
        }


        //現在のパワーを表示
        m_powerText.text = m_power.ToString("Power: 00");
        Debug.Log("現在のpower -> " + m_power);

        //パワーダウン
        m_power -= m_powerDown;
        if ((m_power < 0))
        {
            m_power = 0;
        }

        //投げた際の処理
        if (m_joycon.GetButtonUp(Joycon.Button.SHOULDER_1) && m_power >= m_throwPower)
        {
            m_throwFlag = true;
            return;
        }

        //ボタンを押していない場合、ボタン入力を促す
        if(!m_joycon.GetButton(Joycon.Button.SHOULDER_1))
        {
            //UI表示
            m_pleasePressButton.gameObject.SetActive(true);
            return;
        }

        //パワーが足りない場合、もっと回すよう促す
        if(m_power < m_throwPower)
        {
            //UI表示
            m_notEnoughSpeed.gameObject.SetActive(true);
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
            m_power += 1;

            // カウント後は積算角度をリセット
            m_accumulatedRotationZ = 0f;
        }
    }

    private void Thrown()
    {

    }
}
