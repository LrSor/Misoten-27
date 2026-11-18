using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>説明 → プレイ → 終了表示を管理する、ミニゲーム UI の共通基底。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StateMachine), typeof(MiniGameUIClosedState), typeof(MiniGameUIExplanationState))]
[RequireComponent(typeof(MiniGameUIPlayingState), typeof(MiniGameUIResultState))]
public class MiniGameUIBase : MonoBehaviour
{
    public enum Phase { Closed, Explanation, Playing, Result }

    [Header("表示（管理コンポーネント自身ではなく、子の UI を指定）")]
    [SerializeField] private GameObject m_uiRoot;
    [SerializeField] private GameObject m_explanationUI;
    [SerializeField] private GameObject m_playingUI;
    [SerializeField] private GameObject m_resultUI;

    [Header("表示時間（実時間・秒）")]
    [SerializeField, Min(0f)] private float m_resultSeconds = 5f;

    [Header("説明・終了表示中に停止する Player の操作コンポーネント")]
    [Tooltip("Player 専用の入力・移動 StateMachine 等を指定。UI 自身は指定しない。")]
    [SerializeField] private Behaviour[] m_playerControls = new Behaviour[0];
    [SerializeField] private UnityEvent<bool> m_onPlayerControlChanged = new UnityEvent<bool>();

    private readonly Dictionary<Behaviour, bool> m_controlSnapshot = new Dictionary<Behaviour, bool>();
    private StateMachine m_stateMachine;
    private MiniGameUIPlayingState m_playingState;
    private float m_phaseStartedAt;

    public Phase CurrentPhase { get; private set; }
    public bool IsRunning => CurrentPhase != Phase.Closed;
    public bool CanControlPlayer => CurrentPhase == Phase.Playing;
    internal float PhaseElapsedSeconds => Mathf.Max(0f, Time.unscaledTime - m_phaseStartedAt);
    public float RemainingSeconds => Mathf.Max(0f,
        (CurrentPhase == Phase.Explanation ? MiniGameUIExplanationState.Duration :
         CurrentPhase == Phase.Result ? m_resultSeconds : 0f) - PhaseElapsedSeconds);

    protected virtual void Awake()
    {
        m_stateMachine = GetComponent<StateMachine>();
        m_playingState = GetComponent<MiniGameUIPlayingState>();
        UpdateVisibility();
    }

    /// <summary>入力取得スクリプトから呼ぶ。説明中・終了表示中の入力は破棄する。</summary>
    public void ReceiveVector2Input(Vector2 value)
    {
        if (CanReceiveInput) m_playingState.ReceiveVector2Input(value);
    }

    /// <summary>true = 押下、false = 解放。デバイスごとの取得処理は呼び出し側に置く。</summary>
    public void ReceiveButtonInput(bool isPressed)
    {
        if (CanReceiveInput) m_playingState.ReceiveButtonInput(isPressed);
    }

    private bool CanReceiveInput => isActiveAndEnabled && m_stateMachine.enabled &&
        m_stateMachine.IsInState<MiniGameUIPlayingState>();

    internal void UpdateMiniGameInput(Vector2 value, bool isPressed)
    {
        OnMiniGameInputUpdate(value, isPressed);
    }

    internal void NotifyMiniGameButton(bool isPressed)
    {
        OnMiniGameButtonChanged(isPressed);
    }

    /// <summary>プレイ中の State.UpdateState から毎フレーム呼ぶ。継承先で実装する。</summary>
    protected virtual void OnMiniGameInputUpdate(Vector2 value, bool isPressed) { }
    protected virtual void OnMiniGameButtonChanged(bool isPressed) { }

    public void BeginMiniGame()
    {
        if (!isActiveAndEnabled || !m_stateMachine.enabled ||
            !m_stateMachine.IsInState<MiniGameUIClosedState>()) return;
        m_controlSnapshot.Clear();
        foreach (Behaviour control in m_playerControls)
        {
            if (control == null || control.gameObject == gameObject || m_controlSnapshot.ContainsKey(control)) continue;
            m_controlSnapshot.Add(control, control.enabled);
        }
        m_stateMachine.ChangeState<MiniGameUIExplanationState>();
    }

    /// <summary>ミニゲーム本体が終了条件を満たしたときに呼ぶ。</summary>
    public void FinishMiniGame()
    {
        if (isActiveAndEnabled && CurrentPhase == Phase.Playing)
            m_stateMachine.ChangeState<MiniGameUIResultState>();
    }

    public void CancelMiniGame()
    {
        if (IsRunning) m_stateMachine.ChangeState<MiniGameUIClosedState>();
    }

    protected virtual void OnDisable()
    {
        // 中断時にも操作状態を元に戻し、次回は待機から再開する。
        CancelMiniGame();
        RestorePlayerControls();
    }

    internal void EnterPhase(Phase phase)
    {
        CurrentPhase = phase;
        m_phaseStartedAt = Time.unscaledTime;
        if (phase == Phase.Closed || phase == Phase.Playing) RestorePlayerControls();
        else
            foreach (Behaviour control in m_controlSnapshot.Keys)
                if (control != null) control.enabled = false;

        UpdateVisibility();
        // Closed は通常操作に戻す。CanControlPlayer はミニゲーム内操作の可否。
        m_onPlayerControlChanged.Invoke(phase == Phase.Closed || phase == Phase.Playing);
        if (phase == Phase.Closed) m_controlSnapshot.Clear();
        OnPhaseChanged(phase);
    }

    protected virtual void OnPhaseChanged(Phase phase) { }

    private void RestorePlayerControls()
    {
        foreach (KeyValuePair<Behaviour, bool> entry in m_controlSnapshot)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
    }

    private void UpdateVisibility()
    {
        SetUIActive(m_uiRoot, IsRunning);
        SetUIActive(m_explanationUI, CurrentPhase == Phase.Explanation);
        SetUIActive(m_playingUI, CurrentPhase == Phase.Playing);
        SetUIActive(m_resultUI, CurrentPhase == Phase.Result);
    }

    private void SetUIActive(GameObject target, bool active)
    {
        // 自身や親を非アクティブにすると、タイマーと再開入力も停止してしまう。
        if (target != null && target != gameObject && !transform.IsChildOf(target.transform))
            target.SetActive(active);
    }
}
