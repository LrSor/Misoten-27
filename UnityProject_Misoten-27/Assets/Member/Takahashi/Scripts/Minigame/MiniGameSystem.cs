using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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


    [Header("ミニゲーム本体のスクリプト")]
    [SerializeField] MiniGame m_minigame;
    [Header("プレイヤー本体のカメラ(仮実装)")]
    [SerializeField] Camera m_camera;
    [Header("ミニゲーム用カメラ(仮実装)")]
    [SerializeField] Camera m_miniGameCamera;
    [Header("遊び方説明用のイラスト")]
    [SerializeField] private Image m_howToPlay;
    [Header("ポーズ(ジョイコンの初期位置)を指定するイラスト")]
    [SerializeField] private Image m_pose;
    [Header("ゲーム開始カウントダウン用のテキスト")]
    [SerializeField] private Text m_count;
    [Header("ゲーム開始カウントダウンの秒数")]
    [SerializeField] private float m_countNum;
    [Header("スコア表示用のテキスト")]
    [SerializeField] private Text m_score;
    private float m_currentNum;
    private int m_scoreNum = 0;
    private bool m_isActive = false;
    private STATE_MINIGAME m_state;
    private GameObject m_playerObject = null;


    void Start()
    {
        m_miniGameCamera.gameObject.SetActive(false);
        m_howToPlay.gameObject.SetActive(false);
        m_pose.gameObject.SetActive(false);
        m_count.gameObject.SetActive(false);
        m_score.gameObject.SetActive(false);
        m_currentNum = m_countNum;
        m_state = STATE_MINIGAME.STATE_IDLE;

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
        if (!m_isActive)
        {
            return;
        }

        //デバッグ用にジョイコンが接続されていない場合の処理をここで受け付ける
#if UNITY_EDITOR
        bool hasJoycon = (m_joyconL != null);
        //ジョイコンが接続されている場合、キーボード操作処理を飛ばしジョイコン操作を行う
        if(!hasJoycon)
        {
            switch (m_state)
            {
                case STATE_MINIGAME.STATE_HOWTOPLAY:
                    if (Keyboard.current.zKey.wasPressedThisFrame)
                    {
                        //UI更新
                        m_howToPlay.gameObject.SetActive(false);
                        m_pose.gameObject.SetActive(true);

                        //ステート更新
                        m_state = STATE_MINIGAME.STATE_POSE;
                    }
                    break;

                case STATE_MINIGAME.STATE_POSE:
                    if (Keyboard.current.zKey.wasPressedThisFrame)
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
                        m_score.text = m_scoreNum.ToString("000");
                        m_state = STATE_MINIGAME.STATE_RESULT;
                    }
                    break;

                case STATE_MINIGAME.STATE_RESULT:
                    if (Keyboard.current.zKey.wasPressedThisFrame)
                    {
                        Deactive();
                    }
                    break;

                default:
                    break;
            }
            return;
        }
#endif

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
                //ミニゲーム終了フラグが立った場合、終了処理を行う
                if (m_minigame.GetIsFinish())
                {
                    //スコア取得
                    m_scoreNum = m_minigame.GetScore();
                    Debug.Log(m_scoreNum.ToString());
                    m_score.gameObject.SetActive(true);

                    //ミニゲーム終了処理
                    m_minigame.Deactivate();

                    //スコア表示
                    m_score.text = m_scoreNum.ToString("000");
                    m_state = STATE_MINIGAME.STATE_RESULT;
                }
                break;

            case STATE_MINIGAME.STATE_RESULT:
                if (m_joyconL.GetButtonDown(Joycon.Button.SHOULDER_1))
                {
                    Deactive();
                }
                break;

            default:
                break;
        }
    }


    public void Activate(GameObject player)
    {
        //メンバ変数へプレイヤーオブジェクトをを紐付け
        m_playerObject = player;
        //プレイヤー本体の動作を停止
        m_playerObject.SetActive(false);

        //起動中フラグを真に
        m_isActive = true;
        //ステート更新、操作説明へ変更
        m_state = STATE_MINIGAME.STATE_HOWTOPLAY;
        //操作説明用イラストの表示
        m_howToPlay.gameObject.SetActive(true);

        //仮実装
        //カメラをミニゲーム用に切り替え
        m_camera.gameObject.SetActive(false);
        m_miniGameCamera.gameObject.SetActive(true);
    }

    private void Deactive()
    {
        //スコアを譲渡
        m_playerObject.GetComponent<PlayerScore>().SetMiniGameScore(m_minigame, m_scoreNum);

        //プレイヤー本体の動作を再開
        m_playerObject.SetActive(true);
        //プレイヤーオブジェクトとの紐付けを解除
        m_playerObject = null;

        //スコアUIの非表示
        m_score.gameObject.SetActive(false);

        //仮実装
        //カメラをプレイヤー本体へ切り替え
        m_miniGameCamera.gameObject.SetActive(false);
        m_camera.gameObject.SetActive(true);

        //メンバ変数初期化
        m_currentNum = m_countNum;
        m_state = STATE_MINIGAME.STATE_IDLE;
    }

    public float GetScore()
    {
        return m_scoreNum;
    }
}
