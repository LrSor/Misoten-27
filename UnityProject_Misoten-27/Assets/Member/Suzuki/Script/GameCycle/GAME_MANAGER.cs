using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

//==================================================
// ゲームシーンの進行役
//
//   READY   : 開始前のカウントダウン（3, 2, 1, START）
//             開始演出（GAME_INTRO）がある場合は、それが終わってから数え始める
//   PLAYING : 制限時間が減っていく。この間だけ加点される
//   TIME_UP : 0 秒で操作をロック → 少し待ってリザルトへ
//
// 他のスクリプトからは
//   GAME_MANAGER.Instance.IsPlaying     … プレイ中か
//   GAME_MANAGER.Instance.RemainingTime … 残り時間
//   GAME_MANAGER.GameStarted / TimeUp   … 開始・終了の通知
// を使える
//==================================================
public class GAME_MANAGER : MonoBehaviour
{
    public enum GAME_STATE
    {
        READY,
        PLAYING,
        TIME_UP
    }

    public static GAME_MANAGER Instance { get; private set; }

    // スクリプトから受け取るための通知
    public static event Action GameStarted;
    public static event Action TimeUp;

    [Header("時間（秒）")]
    [Tooltip("制限時間")]
    [SerializeField]
    private float m_timeLimit = 180f;

    [Tooltip("開始前のカウントダウン。0 ですぐに開始")]
    [SerializeField]
    private float m_readyTime = 3f;

    [Tooltip("タイムアップ表示を出してからリザルトに移るまで")]
    [SerializeField]
    private float m_timeUpWait = 2f;

    [Header("シーン")]
    [SerializeField]
    private string m_resultSceneName = "Result";

    [Header("操作ロック")]
    [Tooltip("カウントダウン中もプレイヤーを動けなくする")]
    [SerializeField]
    private bool m_lockDuringReady = true;

    [Tooltip("ロック中に止めるスクリプト。PlayerInput を使っていないプレイヤーや、ミニゲームの開始ゾーンなどを入れる")]
    [SerializeField]
    private Behaviour[] m_lockBehaviours;

    [Header("UI（任意。空欄でも動く）")]
    [Tooltip("残り時間（例 2:59）")]
    [SerializeField]
    private TMP_Text m_timerText;

    [Tooltip("カウントダウン（3, 2, 1, START）")]
    [SerializeField]
    private TMP_Text m_countdownText;

    [Tooltip("「TIME UP」の表示")]
    [SerializeField]
    private GameObject m_timeUpObject;

    [Header("イベント（Inspector から登録できる）")]
    [SerializeField]
    private UnityEvent m_onGameStart = new UnityEvent();

    [SerializeField]
    private UnityEvent m_onTimeUp = new UnityEvent();

    // 「START」の表示を残す時間
    const float START_TEXT_TIME = 1f;

    private GAME_STATE m_state;
    private float m_stateTimer;
    private float m_remainingTime;
    private float m_startTextTimer;

    // ロック中のプレイヤー入力（ロック前に有効だったものだけ覚えて戻す）
    private readonly List<PlayerInput> m_lockedInputs =
        new List<PlayerInput>();

    // ロック中に止めたスクリプト（ロック前に有効だったものだけ覚えて戻す）
    private readonly List<Behaviour> m_lockedBehaviours =
        new List<Behaviour>();

    private bool m_isInputLocked;
    private bool m_needApplyLock;

    public GAME_STATE State
    {
        get { return m_state; }
    }

    public bool IsPlaying
    {
        get { return m_state == GAME_STATE.PLAYING; }
    }

    public float RemainingTime
    {
        get { return m_remainingTime; }
    }

    // カウントダウンを始められずに待っているか
    // （シーン遷移中、または開始演出 GAME_INTRO の再生中）
    public bool IsWaitingForCountdown
    {
        get
        {
            return m_state == GAME_STATE.READY &&
                   (GAME_SCENE_LOADER.IsTransitioning || GAME_INTRO.IsPlaying);
        }
    }

    // カウントダウンの残り（READY 中のみ。それ以外は 0）
    public float ReadyRemaining
    {
        get { return m_state == GAME_STATE.READY ? Mathf.Max(m_stateTimer, 0f) : 0f; }
    }

    // ドメインリロード無しで再生した場合に前回の登録が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        GameStarted = null;
        TimeUp = null;
    }

    void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Debug.LogWarning(
                "GAME_MANAGER がシーンに2つあります: " + name);

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        Time.timeScale = 1f;

        GAME_SCORE.ResetScores();
        GAME_SCORE.SetLocked(true);

        m_remainingTime = m_timeLimit;

        SetActiveSafe(m_timeUpObject, false);
        SetActiveSafe(m_countdownText, false);

        UpdateTimerText();

        // トランジションが終わるまでは READY のまま待つ
        // （カウントダウンが 0 秒の設定でも、画面が見えてから開始する）
        m_state = GAME_STATE.READY;
        m_stateTimer = m_readyTime;

        // カウントダウンの表示は、トランジションと開始演出が終わってから出す（UpdateReady）

        // プレイヤーは他のスクリプトの Start で生成されることがあるので、
        // 全員の Start が終わった後（最初の Update）でロックする
        m_needApplyLock = m_lockDuringReady;
    }

    void Update()
    {
        if (m_needApplyLock)
        {
            m_needApplyLock = false;
            LockPlayerInputs();
        }

        switch (m_state)
        {
            case GAME_STATE.READY:
                UpdateReady();
                break;

            case GAME_STATE.PLAYING:
                UpdatePlaying();
                break;

            case GAME_STATE.TIME_UP:
                UpdateTimeUp();
                break;
        }

        UpdateStartText();
    }

    //==================================================
    // READY（カウントダウン）
    //==================================================

    void UpdateReady()
    {
        // トランジション中（画面がまだ見えていない間）や開始演出中は
        // カウントダウンを進めず、表示も出さない
        if (IsWaitingForCountdown)
        {
            SetActiveSafe(m_countdownText, false);
            return;
        }

        SetActiveSafe(m_countdownText, m_readyTime > 0f);

        m_stateTimer -= Time.deltaTime;

        UpdateCountdownText();

        if (m_stateTimer <= 0f)
        {
            StartGame();
        }
    }

    void UpdateCountdownText()
    {
        if (m_readyTime <= 0f)
        {
            return;
        }

        if (m_countdownText != null)
        {
            m_countdownText.text =
                Mathf.CeilToInt(Mathf.Max(m_stateTimer, 0.01f)).ToString();
        }
    }

    void StartGame()
    {
        m_state = GAME_STATE.PLAYING;

        // 1フレーム目でロックする予定だった場合は取り消す
        m_needApplyLock = false;

        UnlockPlayerInputs();

        GAME_SCORE.SetLocked(false);

        // 「START」を少しだけ表示
        if (m_countdownText != null &&
            m_readyTime > 0f)
        {
            m_countdownText.text = "START";
            m_startTextTimer = START_TEXT_TIME;
        }

        m_onGameStart.Invoke();

        if (GameStarted != null)
        {
            GameStarted();
        }
    }

    void UpdateStartText()
    {
        if (m_startTextTimer <= 0f)
        {
            return;
        }

        m_startTextTimer -= Time.deltaTime;

        if (m_startTextTimer <= 0f)
        {
            SetActiveSafe(m_countdownText, false);
        }
    }

    //==================================================
    // PLAYING（制限時間）
    //==================================================

    void UpdatePlaying()
    {
        m_remainingTime -= Time.deltaTime;

        if (m_remainingTime <= 0f)
        {
            m_remainingTime = 0f;

            UpdateTimerText();

            StartTimeUp();
            return;
        }

        UpdateTimerText();
    }

    void UpdateTimerText()
    {
        if (m_timerText == null)
        {
            return;
        }

        // 0.1 秒残っていても「0:01」と表示し、0 になった瞬間に終わるように切り上げる
        int seconds =
            Mathf.CeilToInt(m_remainingTime);

        m_timerText.text =
            (seconds / 60).ToString() + ":" + (seconds % 60).ToString("00");
    }

    //==================================================
    // TIME_UP（操作ロック → リザルトへ）
    //==================================================

    void StartTimeUp()
    {
        m_state = GAME_STATE.TIME_UP;
        m_stateTimer = m_timeUpWait;

        // これ以降は加点しない・操作できない
        GAME_SCORE.SetLocked(true);
        LockPlayerInputs();

        SetActiveSafe(m_countdownText, false);
        SetActiveSafe(m_timeUpObject, true);

        m_onTimeUp.Invoke();

        if (TimeUp != null)
        {
            TimeUp();
        }
    }

    void UpdateTimeUp()
    {
        m_stateTimer -= Time.unscaledDeltaTime;

        if (m_stateTimer <= 0f)
        {
            GAME_SCENE_LOADER.Load(m_resultSceneName);
        }
    }

    //==================================================
    // プレイヤーの操作ロック
    // PlayerInput を無効化するのではなく入力だけ止めるので、
    // コントローラーの割り当ては外れない
    //==================================================

    void LockPlayerInputs()
    {
        if (m_isInputLocked)
        {
            return;
        }

        m_isInputLocked = true;
        m_lockedInputs.Clear();

        PlayerInput[] playerInputs =
            FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);

        foreach (PlayerInput playerInput in playerInputs)
        {
            if (playerInput.enabled &&
                playerInput.inputIsActive)
            {
                playerInput.DeactivateInput();
                m_lockedInputs.Add(playerInput);
            }
        }

        // Inspector で指定したスクリプトを止める
        m_lockedBehaviours.Clear();

        if (m_lockBehaviours != null)
        {
            foreach (Behaviour behaviour in m_lockBehaviours)
            {
                if (behaviour != null &&
                    behaviour.enabled)
                {
                    behaviour.enabled = false;
                    m_lockedBehaviours.Add(behaviour);
                }
            }
        }
    }

    void UnlockPlayerInputs()
    {
        if (!m_isInputLocked)
        {
            return;
        }

        m_isInputLocked = false;

        foreach (PlayerInput playerInput in m_lockedInputs)
        {
            if (playerInput != null &&
                playerInput.enabled)
            {
                playerInput.ActivateInput();
            }
        }

        m_lockedInputs.Clear();

        foreach (Behaviour behaviour in m_lockedBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        m_lockedBehaviours.Clear();
    }

    //==================================================
    // デバッグ用（GAME_DEBUG_TOOL から使う）
    //==================================================

    public void SetRemainingTime(float seconds)
    {
        if (m_state != GAME_STATE.PLAYING)
        {
            return;
        }

        m_remainingTime =
            Mathf.Max(0f, seconds);
    }

    //==================================================
    // 補助
    //==================================================

    static void SetActiveSafe(Component target, bool isActive)
    {
        if (target != null)
        {
            target.gameObject.SetActive(isActive);
        }
    }

    static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_timeLimit = Mathf.Max(1f, m_timeLimit);
        m_readyTime = Mathf.Max(0f, m_readyTime);
        m_timeUpWait = Mathf.Max(0f, m_timeUpWait);
    }
}
