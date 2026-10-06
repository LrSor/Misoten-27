using System.Collections.Generic;
using System;
using UnityEngine;

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

    [SerializeField] MiniGameSystem m_system;
    private bool m_isCollisionPlayer = false;
    private bool m_isPlaying = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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

        if(m_isCollisionPlayer && m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_2))
        {
            m_system.Activate();
            m_isPlaying = true;


        }
    }


    //プレイヤー衝突判定(改修必須)
    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player")
        {
            m_isCollisionPlayer = true;
            Debug.Log("ミニゲーム開始可能");
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            m_isCollisionPlayer = false;
            Debug.Log("ミニゲームゾーンから離脱");

            if(m_isPlaying)
            {
                m_isPlaying = false;
            }
        }
    }
}
