using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

//ミニゲーム開始ゾーン
//
public class MiniGameZone : MonoBehaviour
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joyconL;
    //private Joycon m_joyconR;
    private Joycon.Button? m_pressedButtonL;
    //private Joycon.Button? m_pressedButtonR;
    //============================================================

    [Header("ミニゲーム管理用オブジェクト")]
    [SerializeField] MiniGameSystem m_system;
    private bool m_isPlaying = false;
    private GameObject m_playerObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_playerObject = null;

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

    // Update is called once per frame
    void Update()
    {
        if(m_isPlaying)
        {
            return;
        }

        // デバッグ用にエディタ上のみキーボード操作を受け付ける
#if UNITY_EDITOR
        bool hasJoycon = (m_joyconL != null);
        if(!hasJoycon )
        {
            if (m_playerObject && Keyboard.current.zKey.wasPressedThisFrame)
            {
                m_system.Activate(m_playerObject);
                m_isPlaying = true;
            }
            return;
        }
#endif

        //L2ボタン入力でゲーム開始
        if (m_playerObject && m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_2))
        {
            //ミニゲーム起動(引数でプレイヤーオブジェクトを渡す)
            m_system.Activate(m_playerObject);
            m_isPlaying = true;
        }
    }


    //プレイヤー衝突判定
    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player" && !m_isPlaying)
        {
            Debug.Log("ミニゲーム開始可能");

            //メンバ変数にプレイヤーオブジェクトを紐付け
            m_playerObject = collision.gameObject;
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            Debug.Log("ミニゲームゾーンから離脱");

            if(m_isPlaying)
            {
                m_isPlaying = false;
            }

            //メンバ変数に紐付けされたプレイヤーオブジェクトを解除
            m_playerObject = null;
        }
    }
}
