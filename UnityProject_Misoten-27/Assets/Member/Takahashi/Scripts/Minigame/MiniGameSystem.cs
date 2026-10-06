using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameSystem : MonoBehaviour
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

    enum STATE_MINIGAME
    {
        STATE_HOWTOPLAY,
        STATE_POSE,
        STATE_COUNT,
        STATE_PLAYING,
        STATE_RESULT,
        STATE_IDLE
    }

    [SerializeField] MiniGame m_minigame;
    [SerializeField] Camera m_camera;
    [SerializeField] Camera m_miniGameCamera;
    [SerializeField] private Image m_howToPlay;
    [SerializeField] private Image m_pose;
    [SerializeField] private Text m_count;
    [SerializeField] private Text m_score;
    [SerializeField] private float m_countNum;
    private float m_currentNum;
    private float m_scoreNum = 0;
    private bool m_isActive = false;
    private STATE_MINIGAME m_state;


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

        m_miniGameCamera.gameObject.SetActive(false);
        m_howToPlay.gameObject.SetActive(false);
        m_pose.gameObject.SetActive(false);
        m_count.gameObject.SetActive(false);
        m_score.gameObject.SetActive(false);
        m_currentNum = m_countNum;
        m_state = STATE_MINIGAME.STATE_IDLE;
    }

    // Update is called once per frame
    void Update()
    {
        //============================================================
        // JoyCon入力(コメントアウト)
        //============================================================
        {//m_pressedButtonL = null;
         ////m_pressedButtonR = null;
         //
         //if (m_joycons == null || m_joycons.Count <= 0) return;
         //
         //foreach (var button in m_buttons)
         //{
         //    if (m_joyconL.GetButton(button))
         //    {
         //        m_pressedButtonL = button;
         //    }
         //    //if (m_joyconR.GetButton(button))
         //    //{
         //    //    m_pressedButtonR = button;
         //    //}
         //}
        }
        //============================================================
        if (!m_isActive)
        {
            return;
        }

        switch (m_state)
        {
            case STATE_MINIGAME.STATE_HOWTOPLAY:
                if (m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_1))
                {
                    //UI更新
                    m_howToPlay.gameObject.SetActive(false);
                    m_pose.gameObject.SetActive(true);

                    //ステート更新
                    m_state = STATE_MINIGAME.STATE_POSE;
                }
                break;

            case STATE_MINIGAME.STATE_POSE:
                if (m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_1))
                {
                    //UI更新
                    m_pose.gameObject.SetActive(false);
                    m_count.gameObject.SetActive(true);

                    //ステート更新
                    m_state = STATE_MINIGAME.STATE_COUNT;
                }
                break;

            case STATE_MINIGAME.STATE_COUNT:
                //カウントダウン
                m_currentNum -= Time.deltaTime;
                m_count.text = m_currentNum.ToString("0");

                if (m_currentNum <= 0)
                {
                    //カウントダウンUI非表示
                    m_count.gameObject.SetActive(false);

                    //ステート更新、ミニゲーム開始
                    m_minigame.Activate();
                    m_state = STATE_MINIGAME.STATE_PLAYING;
                }
                break;

            case STATE_MINIGAME.STATE_PLAYING:
                if (m_minigame.GetIsFinish())
                {
                    //スコア取得
                    m_scoreNum = m_minigame.GetScore();
                    m_score.gameObject.SetActive(true);

                    //ミニゲーム終了処理
                    m_minigame.Deactivate();

                    //スコア表示
                    m_score.text = m_scoreNum.ToString("0");
                    m_state = STATE_MINIGAME.STATE_RESULT;
                }
                break;

            case STATE_MINIGAME.STATE_RESULT:
                if (m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_1))
                {
                    //スコアUIの非表示
                    m_score.gameObject.SetActive(false);

                    //カメラの変更
                    m_miniGameCamera.gameObject.SetActive(false);
                    m_camera.gameObject.SetActive(true);

                    //メンバ変数初期化
                    m_currentNum = m_countNum;
                    m_state = STATE_MINIGAME.STATE_IDLE;
                }
                break;

            default:
                break;
        }
    }


    public void Activate()
    {
        m_isActive = true;
        m_state = STATE_MINIGAME.STATE_HOWTOPLAY;
        m_howToPlay.gameObject.SetActive(true);
        m_camera.gameObject.SetActive(false);
        m_miniGameCamera.gameObject.SetActive(true);
    }
}
