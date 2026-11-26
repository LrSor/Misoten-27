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

    // タイムアップ後、リザルトへ移るのを待ってほしいときに使う
    // （GAME_NETWORK_BRIDGE が「通信の順位集計が終わるまで待って」と使う）
    // true を返している間はリザルトへ移らない
    public static Func<bool> ResultHold;

    // カウントダウンを始めてよいか（false の間は始めない）
    // （GAME_SYNC が「ロビーで Host が開始を押すまで待って」と使う）
    public static Func<bool> StartGate;

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

    [Header("通信（澤田さんのネットワークシーン用）")]
    [Tooltip("プレイヤーが出てくる（Start Host / 接続が終わる）までカウントダウンを始めない")]
    [SerializeField]
    private bool m_waitForPlayer = false;

    [Tooltip("ロックのとき PlayerInput ごと無効にする（通信シーン用）。\n澤田さんの通信は PlayerInput が有効かどうかを見て入力を読むので、こちらの方が確実に止まる")]
    [SerializeField]
    private bool m_disablePlayerInput = false;

    [Tooltip("ResultHold で待つ最大の時間（通信が止まっても必ずリザルトへ進むように）")]
    [SerializeField]
    private float m_resultHoldMax = 10f;

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

    // プレイヤーが出てきたか（m_waitForPlayer 用）
    private bool m_hasPlayer;
    private float m_resultHoldTimer;

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
                   (GAME_SCENE_LOADER.IsTransitioning || GAME_INTRO.IsPlaying ||
                    IsWaitingForPlayer || IsWaitingForStart);
        }
    }

    // プレイヤーの出現を待っているか（通信の Start Host / 接続前）
    public bool IsWaitingForPlayer
    {
        get { return m_waitForPlayer && !m_hasPlayer; }
    }

    // ロビーで開始されるのを待っているか
    public bool IsWaitingForStart
    {
        get { return StartGate != null && !StartGate(); }
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
        ResultHold = null;
        StartGate = null;
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
        // 通信のプレイヤーは Start Host / 接続の後で生成されるので、
        // 出てきた瞬間にロックをかけ直す
        if (m_waitForPlayer && !m_hasPlayer)
        {
            m_hasPlayer = FindAnyObjectByType<PlayerScore>() != null;
        }

        // カウントダウン中に出てきた（有効になった）プレイヤーも止める
        if (m_waitForPlayer && m_lockDuringReady)
        {
            LockNewPlayerInputs();
        }

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

        if (m_stateTimer > 0f)
        {
            return;
        }

        // 順位の集計などを待つ（長すぎるときはあきらめて進む）
        if (ResultHold != null &&
            ResultHold() &&
            m_resultHoldTimer < m_resultHoldMax)
        {
            m_resultHoldTimer += Time.unscaledDeltaTime;
            return;
        }

        GAME_SCENE_LOADER.Load(m_resultSceneName);
    }

    // 他の理由（他のプレイヤーが先に終わった等）で、すぐにタイムアップにする
    public void ForceTimeUp()
    {
        if (m_state != GAME_STATE.PLAYING)
        {
            return;
        }

        m_remainingTime = 0f;
        UpdateTimerText();
        StartTimeUp();
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
            TryLockInput(playerInput);
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

    // ロック中に新しく出てきたプレイヤーの入力も止める
    void LockNewPlayerInputs()
    {
        if (!m_isInputLocked)
        {
            LockPlayerInputs();
            return;
        }

        PlayerInput[] playerInputs =
            FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);

        foreach (PlayerInput playerInput in playerInputs)
        {
            if (!m_lockedInputs.Contains(playerInput))
            {
                TryLockInput(playerInput);
            }
        }
    }

    // 1つの PlayerInput を止める（止めたら覚えておく）
    void TryLockInput(PlayerInput playerInput)
    {
        if (!playerInput.enabled)
        {
            return;
        }

        if (m_disablePlayerInput)
        {
            playerInput.enabled = false;
        }
        else if (playerInput.inputIsActive)
        {
            playerInput.DeactivateInput();
        }
        else
        {
            return;
        }

        m_lockedInputs.Add(playerInput);

        Debug.Log("GAME_MANAGER: 操作をロック → " + playerInput.name);
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
            if (playerInput == null)
            {
                continue;
            }

            if (m_disablePlayerInput)
            {
                playerInput.enabled = true;
            }
            else if (playerInput.enabled)
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
        m_resultHoldMax = Mathf.Max(0f, m_resultHoldMax);
    }
}
