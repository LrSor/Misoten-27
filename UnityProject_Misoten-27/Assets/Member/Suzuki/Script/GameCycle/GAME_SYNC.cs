using Sawada.Cordex;
using UnityEngine;

//==================================================
// ゲームの進行（ロビー → カウントダウン → プレイ中 → タイムアップ）を
// Host から全員に合わせるためのもの
//
// ・澤田さんの NetworkWorldObject（Host の値が Client に自動で送られる仕組み）を使っている
//   → 数字 1 つ（Value）だけを送る
// ・Host：ロビーの「ゲーム開始」で開始。GAME_MANAGER の状態を Value にして送る
// ・Client：届いた Value を見て
//     開始されたらカウントダウンを始める
//     残り時間が Host と 0.5 秒以上ずれたら Host に合わせる
//     Host がタイムアップしたら自分もタイムアップにする
//
// ※ 子オブジェクトを持たせないこと（Client では子のスクリプトが止められてしまう）
//==================================================
public sealed class GAME_SYNC : NetworkWorldObject
{
    public enum PHASE
    {
        LOBBY = 0,
        COUNTDOWN = 1,
        PLAYING = 2,
        TIME_UP = 3
    }

    // プレイ中の Value は PLAYING_BASE + 残り時間（0.1 秒単位）
    const int PLAYING_BASE = 2000000;

    // Host と残り時間がこれ以上ずれたら合わせる（秒）
    const float SYNC_THRESHOLD = 0.5f;

    public static GAME_SYNC Instance { get; private set; }

    private int m_receivedValue;
    private bool m_hostStarted;

    private System.Func<bool> m_gate;

    // Host か（澤田さんの通信で Host を始めたか）
    public bool IsHost
    {
        get { return World != null && World.IsHost; }
    }

    // ゲームが開始されたか（ロビーを抜けたか）
    public bool IsStarted
    {
        get { return CurrentPhase != PHASE.LOBBY; }
    }

    public PHASE CurrentPhase
    {
        get { return Decode(Value, out _); }
    }

    //==================================================
    // 送る値
    //==================================================

    protected override int Value
    {
        get { return IsHost ? EncodeHost() : m_receivedValue; }
        set { m_receivedValue = value; }
    }

    int EncodeHost()
    {
        if (!m_hostStarted)
        {
            return (int)PHASE.LOBBY;
        }

        GAME_MANAGER manager = GAME_MANAGER.Instance;

        if (manager == null)
        {
            return (int)PHASE.COUNTDOWN;
        }

        switch (manager.State)
        {
            case GAME_MANAGER.GAME_STATE.PLAYING:
                return PLAYING_BASE + Mathf.CeilToInt(manager.RemainingTime * 10f);

            case GAME_MANAGER.GAME_STATE.TIME_UP:
                return (int)PHASE.TIME_UP;

            default:
                return (int)PHASE.COUNTDOWN;
        }
    }

    static PHASE Decode(int value, out float remaining)
    {
        remaining = 0f;

        if (value >= PLAYING_BASE)
        {
            remaining = (value - PLAYING_BASE) / 10f;
            return PHASE.PLAYING;
        }

        if (value == (int)PHASE.COUNTDOWN) return PHASE.COUNTDOWN;
        if (value == (int)PHASE.TIME_UP) return PHASE.TIME_UP;

        return PHASE.LOBBY;
    }

    //==================================================
    // Host：ゲーム開始（ロビーの「ゲーム開始」ボタン）
    //==================================================

    public void HostStart()
    {
        if (IsHost)
        {
            m_hostStarted = true;
        }
    }

    //==================================================

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        // 開始されるまで GAME_MANAGER のカウントダウンを止めておく
        if (m_gate == null)
        {
            m_gate = () => IsStarted;
        }

        GAME_MANAGER.StartGate = m_gate;
    }

    void OnDisable()
    {
        if (GAME_MANAGER.StartGate == m_gate)
        {
            GAME_MANAGER.StartGate = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected override void Update()
    {
        base.Update();

        // Host が通信をやめたら、開始した状態も消す
        if (!IsHost)
        {
            m_hostStarted = false;
        }

        if (IsHost)
        {
            return;
        }

        GAME_MANAGER manager = GAME_MANAGER.Instance;

        if (manager == null)
        {
            return;
        }

        PHASE phase = Decode(m_receivedValue, out float hostRemaining);

        if (phase == PHASE.PLAYING &&
            manager.IsPlaying &&
            Mathf.Abs(manager.RemainingTime - hostRemaining) > SYNC_THRESHOLD)
        {
            manager.SetRemainingTime(hostRemaining);
        }

        if (phase == PHASE.TIME_UP)
        {
            manager.ForceTimeUp();
        }
    }
}
