using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Linq;

public class MiniGameSystem : MonoBehaviour
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joycon;
    //============================================================

    enum STATE_MINIGAME
    {
        STATE_IDLE,
        STATE_HOWTOPLAY,
        STATE_POSE,
        STATE_COUNT,
        STATE_PLAYING,
        STATE_RESULT,
        STATE_DEACTIVE
    }


    [Header("ミニゲーム本体のスクリプト")]
    [SerializeField] MiniGame m_minigame;
    [Header("ミニゲーム用カメラ")]
    [SerializeField] Camera m_miniGameCamera;
    [Header("ミニゲーム用Canvas")]
    [SerializeField] Canvas m_miniGameCanvas;
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
    [Header("ゲームオーバー用イラスト")]
    [SerializeField] private Image m_gameOver;
    private float m_currentNum;
    private int m_scoreNum = 0;
    private bool m_isActiveFlag = false;
    private STATE_MINIGAME m_state;
    private GameObject m_playerObject = null;
    private Camera m_playerCamera = null;
    private Canvas m_playerCanvas = null;
    private bool m_pressedFlag = false;
    private int m_defaultTargetDisplay = 6;


    void Start()
    {
        m_miniGameCamera.gameObject.SetActive(false);
        m_howToPlay.gameObject.SetActive(false);
        m_pose.gameObject.SetActive(false);
        m_count.gameObject.SetActive(false);
        m_score.gameObject.SetActive(false);
        m_gameOver.gameObject.SetActive(false);
        m_currentNum = m_countNum;
        m_state = STATE_MINIGAME.STATE_DEACTIVE;
    }

    // Update is called once per frame
    void Update()
    {
        if (!m_isActiveFlag)
        {
            return;
        }

        //デバッグ用にジョイコンが接続されていない場合の処理をここで受け付ける
        //====================================================================================
        bool hasJoycon = (m_joycon != null);
        //ジョイコンが接続されている場合、キーボード操作処理を飛ばしジョイコン操作を行う
        if(!hasJoycon)
        {
            switch (m_state)
            {
                case STATE_MINIGAME.STATE_IDLE:
                    //ステート更新、操作説明へ変更
                    m_state = STATE_MINIGAME.STATE_HOWTOPLAY;
                    //操作説明用イラストの表示
                    m_howToPlay.gameObject.SetActive(true);
                    break;

                case STATE_MINIGAME.STATE_HOWTOPLAY:
                    if (Keyboard.current.zKey.wasPressedThisFrame && !m_pressedFlag)
                    {
                        //UI更新
                        m_howToPlay.gameObject.SetActive(false);
                        m_pose.gameObject.SetActive(true);

                        //ステート更新
                        m_state = STATE_MINIGAME.STATE_POSE;

                        m_pressedFlag = true;
                    }
                    break;

                case STATE_MINIGAME.STATE_POSE:
                    if (Keyboard.current.zKey.wasPressedThisFrame && !m_pressedFlag)
                    {
                        //UI更新
                        m_pose.gameObject.SetActive(false);
                        m_count.gameObject.SetActive(true);

                        //ステート更新
                        m_state = STATE_MINIGAME.STATE_COUNT;
                    }
                    else
                    {
                        m_pressedFlag = false;
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

    //===========================================================================================

        switch (m_state)
        {
            case STATE_MINIGAME.STATE_IDLE:
                //ステート更新、操作説明へ変更
                m_state = STATE_MINIGAME.STATE_HOWTOPLAY;
                //操作説明用イラストの表示
                m_howToPlay.gameObject.SetActive(true);
                break;

            case STATE_MINIGAME.STATE_HOWTOPLAY:
                if (m_joycon.GetButtonDown(Joycon.Button.SHOULDER_1) && !m_pressedFlag)
                {
                    //UI更新
                    m_howToPlay.gameObject.SetActive(false);
                    m_pose.gameObject.SetActive(true);

                    //ステート更新
                    m_state = STATE_MINIGAME.STATE_POSE;

                    m_pressedFlag = true;
                }
                break;

            case STATE_MINIGAME.STATE_POSE:
                if (m_joycon.GetButtonDown(Joycon.Button.SHOULDER_1) && !m_pressedFlag)
                {
                    //UI更新
                    m_pose.gameObject.SetActive(false);
                    m_count.gameObject.SetActive(true);

                    //ステート更新
                    m_state = STATE_MINIGAME.STATE_COUNT;
                }
                else
                {
                    m_pressedFlag = false;
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
                    if(m_minigame.GetIsGameClear())
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
                    else
                    {
                        //ミニゲーム終了処理
                        m_minigame.Deactivate();

                        //ゲームオーバー表示
                        m_gameOver.gameObject.SetActive(true);
                        m_state = STATE_MINIGAME.STATE_RESULT;
                    }
                }
                break;

            case STATE_MINIGAME.STATE_RESULT:
                if (m_joycon.GetButtonDown(Joycon.Button.SHOULDER_1))
                {
                    Deactive();
                }
                break;

            default:
                break;
        }
    }


    public void Activate(GameObject playerObject)
    {
        //メンバ変数へプレイヤーオブジェクトをを紐付け
        m_playerObject = playerObject;
        //プレイヤー本体の動作を停止
        m_playerObject.SetActive(false);

        //起動中フラグを真に
        m_isActiveFlag = true;
        //ミニゲーム実行ステータスを待機へ変更(同フレーム内でボタン入力が行われた際の対策)
        m_state = STATE_MINIGAME.STATE_IDLE;

        //カメラをミニゲーム用に切り替え
        //プレイヤーのカメラ,Canvasを取得
        m_playerCamera = m_playerObject.GetComponent<PlayerScore>().GetPlayerCamera();
        m_playerCanvas = m_playerObject.GetComponent<PlayerScore>().GetPlayerCanvas();
        //ミニゲーム用カメラ、Canvasのターゲットディスプレイをプレイヤーと同じものへ変更
        m_miniGameCamera.targetDisplay = m_playerCamera.targetDisplay;
        m_miniGameCanvas.targetDisplay = m_playerCanvas.targetDisplay;
        //プレイヤー本体のカメラを無効化し、ミニゲームのカメラを有効化
        m_playerCamera.gameObject.SetActive(false);
        m_miniGameCamera.gameObject.SetActive(true);
        m_playerCanvas.gameObject.SetActive(false);

        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
        JoyconManager joyconManager = GameObject.FindFirstObjectByType<JoyconManager>();
        if (joyconManager.GetIsKeyboardPlay())
        {
            return;
        }

        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joycon = m_joycons.First();
        //============================================================
    }

    private void Deactive()
    {
        //ジョイコンの入力を切る
        m_joycons = null;

        //スコアを譲渡
        m_playerObject.GetComponent<PlayerScore>().SetMiniGameScore(m_minigame, m_scoreNum);

        //プレイヤー本体の動作を再開
        m_playerObject.SetActive(true);
        //プレイヤーオブジェクトとの紐付けを解除
        m_playerObject = null;

        //スコアUIの非表示
        m_score.gameObject.SetActive(false);
        m_gameOver.gameObject.SetActive(false);

        //カメラをプレイヤー本体用へ切り替え
        //ミニゲーム用カメラ、Canvasのターゲットディスプレイをデフォルトへ変更
        m_miniGameCamera.targetDisplay = m_defaultTargetDisplay;
        m_miniGameCanvas.targetDisplay = m_defaultTargetDisplay;
        //ミニゲーム用カメラを無効化し、プレイヤー用カメラを有効化
        m_miniGameCamera.gameObject.SetActive(false);
        m_playerCamera.gameObject.SetActive(true);
        m_playerCanvas.gameObject.SetActive(true);

        //メンバ変数初期化
        m_currentNum = m_countNum;
        m_state = STATE_MINIGAME.STATE_DEACTIVE;
    }

    public float GetScore()
    {
        return m_scoreNum;
    }
}
