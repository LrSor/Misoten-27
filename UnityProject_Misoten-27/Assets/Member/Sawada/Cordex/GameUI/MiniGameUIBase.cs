using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>説明 → プレイ → 終了表示を管理する、ミニゲーム UI の共通基底。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StateMachine), typeof(MiniGameUIClosedState), typeof(MiniGameUIExplanationState))]
[RequireComponent(typeof(MiniGameUIPlayingState), typeof(MiniGameUIResultState))]
public class MiniGameUIBase : MonoBehaviour
{
    public enum Phase { Closed, Explanation, Playing, Result, Countdown }

    [Header("表示（管理コンポーネント自身ではなく、子の UI を指定）")]
    [SerializeField] private GameObject m_uiRoot;
    [SerializeField] private GameObject m_explanationUI;
    [SerializeField] private GameObject m_playingUI;
    [SerializeField] private GameObject m_resultUI;

    [Header("表示時間（実時間・秒）")]
    [SerializeField, Min(0f)] private float m_resultSeconds = 5f;

    [Header("READY（Scale 8 で画面にフィット）")]
    [SerializeField, Min(0f)] private float m_readyFromScale = 12f;
    [SerializeField, Min(0f)] private float m_readyToScale = 8f;
    [SerializeField, Range(0f, 1f)] private float m_readyFromAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float m_readyToAlpha = 1f;
    [SerializeField, Min(0f)] private float m_readyZoomSeconds = 1f;
    [SerializeField, Min(0f)] private float m_readyHoldSeconds = 1f;

    [Header("START（Scale 8 で画面にフィット）")]
    [SerializeField, Min(0f)] private float m_startFromScale = 8f;
    [SerializeField, Min(0f)] private float m_startToScale = 12f;
    [SerializeField, Min(0f)] private float m_startZoomSeconds = 1f;

    [Header("結果の星")]
    [Tooltip("画面の短い辺に対する星1個のサイズの割合")]
    [SerializeField, Range(0f, 1f)] private float m_starSizeRatio = 0.2f;
    [SerializeField, Min(0f)] private float m_starIntervalSeconds = 0.25f;
    [SerializeField] private float m_lastStarFromAngle = -30f;
    [SerializeField] private float m_lastStarToAngle = 0f;
    [SerializeField, Min(0f)] private float m_lastStarRotateSeconds = 0.1f;

    [Header("待機に戻った後の星の回収")]
    [SerializeField] private StarCollectionEffect m_starCollectionEffect;
    [SerializeField, Min(0f)] private float m_starCollectDelaySeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float m_starFlySeconds = 0.5f;
    [Tooltip("画面の短い辺に対する弧の高さの割合")]
    [SerializeField, Min(0f)] private float m_starArcHeightRatio = 0.2f;

    [Header("説明・終了表示中に停止する Player の操作コンポーネント")]
    [Tooltip("Player 専用の入力・移動 StateMachine 等を指定。UI 自身は指定しない。")]
    [SerializeField] private Behaviour[] m_playerControls = new Behaviour[0];
    [SerializeField] private UnityEvent<bool> m_onPlayerControlChanged = new UnityEvent<bool>();

    private readonly Dictionary<Behaviour, bool> m_controlSnapshot = new Dictionary<Behaviour, bool>();
    private StateMachine m_stateMachine;
    private MiniGameUIPlayingState m_playingState;
    [SerializeField, Min(1f)] private float m_playSeconds = 10f;
    [SerializeField] private TMP_Text m_timeLabel;
    public int ResultStars { get; private set; }
    private float m_phaseStartedAt;
    private Texture2D m_ready, m_start, m_star;
    private bool m_externalControl, m_externalClear, m_externalHasTimer;
    private float m_externalRemaining, m_externalDuration, m_externalSampleAt;
    public bool IsExternallyDriven => m_externalControl;
    public float ResultDisplaySeconds => Mathf.Max(0f, m_resultSeconds);
    private float ExternalElapsed => Mathf.Max(0f, Time.time - m_externalSampleAt);
    private float ReadySeconds => m_readyZoomSeconds + m_readyHoldSeconds;
    // 短い結果表示でも、すべての星の出現・回転を表示時間内に収める。
    private float ResultAnimationDuration => m_externalControl ? m_externalDuration : m_resultSeconds;
    private float ResultAnimationSpeed => ResultAnimationDuration > 0f && ResultStars > 0
        ? Mathf.Max(1f, ((ResultStars - 1) * m_starIntervalSeconds + m_lastStarRotateSeconds) / ResultAnimationDuration)
        : 1f;
    internal float CountdownSeconds => ReadySeconds + m_startZoomSeconds;
    public bool ShowingReady => CurrentPhase == Phase.Countdown && PhaseElapsedSeconds < ReadySeconds;
    public bool ShowingStart => CurrentPhase == Phase.Countdown && !ShowingReady;

    public Phase CurrentPhase { get; private set; }
    public bool IsRunning => CurrentPhase != Phase.Closed;
    public bool CanControlPlayer => CurrentPhase == Phase.Playing;
    internal float PhaseElapsedSeconds => m_externalControl
        ? Mathf.Max(0f, m_externalDuration - m_externalRemaining + ExternalElapsed)
            * (CurrentPhase == Phase.Countdown && m_externalDuration > 0f ? CountdownSeconds / m_externalDuration : 1f)
        : Mathf.Max(0f, Time.unscaledTime - m_phaseStartedAt);
    public float RemainingSeconds => m_externalControl ? m_externalRemaining : Mathf.Max(0f,
        (CurrentPhase == Phase.Playing ? m_playSeconds :
         CurrentPhase == Phase.Countdown ? CountdownSeconds :
         CurrentPhase == Phase.Result ? m_resultSeconds : 0f) - PhaseElapsedSeconds);

    /// <summary>The external authority owns timing, input and player controls; this component only presents them.</summary>
    public void SetExternalControl(bool enabled)
    {
        if (m_externalControl == enabled) return;
        CancelMiniGame();
        RestorePlayerControls();
        m_controlSnapshot.Clear();
        m_externalControl = enabled;
    }

    public void BindStarCollectionEffect(StarCollectionEffect effect) => m_starCollectionEffect = effect;

    public void ApplyExternalPhase(Phase phase, float remaining, float duration, int stars = 0, bool clear = false, bool hasTimer = true)
    {
        if (!m_externalControl || !isActiveAndEnabled || m_stateMachine == null || !m_stateMachine.enabled) return;
        m_externalRemaining = Mathf.Max(0f, remaining);
        m_externalDuration = Mathf.Max(0f, duration);
        m_externalSampleAt = Time.time;
        m_externalClear = clear;
        m_externalHasTimer = hasTimer;
        if (phase == Phase.Result) ResultStars = Mathf.Clamp(stars, 0, 3);
        if (phase != CurrentPhase)
        {
            switch (phase)
            {
                case Phase.Countdown: m_stateMachine.ChangeState<MiniGameUIExplanationState>(); break;
                case Phase.Playing: m_stateMachine.ChangeState<MiniGameUIPlayingState>(); break;
                case Phase.Result: m_stateMachine.ChangeState<MiniGameUIResultState>(); break;
                case Phase.Closed: m_stateMachine.ChangeState<MiniGameUIClosedState>(); break;
            }
        }
        UpdateTimer();
    }

    /// <summary>Only a confirmed normal completion may start star collection. Disconnect/cancel never does.</summary>
    public void CloseExternalGame(bool completed)
    {
        if (!m_externalControl) return;
        if (completed && CurrentPhase == Phase.Result) CompleteResult();
        else CancelMiniGame();
        m_externalRemaining = m_externalDuration = 0f;
    }

    protected virtual void Awake()
    {
        m_stateMachine = GetComponent<StateMachine>();
        m_playingState = GetComponent<MiniGameUIPlayingState>();
        // 既存の GameUI では右上の Image の子がタイマー。
        if (m_timeLabel == null && m_uiRoot != null)
            m_timeLabel = m_uiRoot.transform.Find("Image/Text (TMP)")?.GetComponent<TMP_Text>();
        m_star = Resources.Load<Texture2D>("SawadaQuest/Star");
        m_ready = Resources.Load<Texture2D>("SawadaQuest/READY");
        m_start = Resources.Load<Texture2D>("SawadaQuest/START");
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

    private bool CanReceiveInput => !m_externalControl && isActiveAndEnabled && m_stateMachine.enabled &&
        m_stateMachine.IsInState<MiniGameUIPlayingState>();

    internal void UpdateMiniGameInput(Vector2 value, bool isPressed)
    {
        UpdateTimer();
        if (m_externalControl) return;
        if (RemainingSeconds <= 0f) FinishMiniGame(0);
        else OnMiniGameInputUpdate(value, isPressed);
    }

    internal void NotifyMiniGameButton(bool isPressed)
    {
        OnMiniGameButtonChanged(isPressed);
    }

    /// <summary>プレイ中の State.UpdateState から毎フレーム呼ぶ。継承先で実装する。</summary>
    protected virtual void OnMiniGameInputUpdate(Vector2 value, bool isPressed) { }
    protected virtual void OnMiniGameButtonChanged(bool isPressed) { }

    protected virtual void OnGUI()
    {
        if (CurrentPhase == Phase.Result)
        {
            Rect label = new Rect(0, Screen.height * 0.4f - 40, Screen.width, 40);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
            GUI.Label(label, (m_externalControl ? m_externalClear : ResultStars > 0) ? "CLEAR!" : "TIME UP", style);
            for (int i = 0; i < ResultStars && m_star != null; i++)
            {
                float age = PhaseElapsedSeconds * ResultAnimationSpeed - i * m_starIntervalSeconds;
                if (age < 0f) break;
                Rect rect = GetResultStarRect(i);
                Matrix4x4 previousMatrix = GUI.matrix;
                float rotation = Mathf.Lerp(m_lastStarFromAngle, m_lastStarToAngle,
                    AnimationProgress(age, m_lastStarRotateSeconds));
                GUIUtility.RotateAroundPivot(rotation, rect.center);
                GUI.DrawTexture(rect, m_star, ScaleMode.ScaleToFit);
                GUI.matrix = previousMatrix;
            }
            return;
        }
        if (CurrentPhase != Phase.Countdown) return;
        Texture2D banner = ShowingReady ? m_ready : m_start;
        if (banner == null) return;
        // Scale 8 を従来の画面フィットサイズに合わせる。
        const float fitScale = 8f;
        float elapsed = PhaseElapsedSeconds;
        float progress = ShowingReady ? AnimationProgress(elapsed, m_readyZoomSeconds)
                                      : AnimationProgress(elapsed - ReadySeconds, m_startZoomSeconds);
        float scale = ShowingReady ? Mathf.Lerp(m_readyFromScale, m_readyToScale, progress)
                                   : Mathf.Lerp(m_startFromScale, m_startToScale, progress);
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        Vector2 extent = screen * (scale / fitScale);
        Color previousColor = GUI.color;
        Color tint = previousColor;
        if (ShowingReady) tint.a *= Mathf.Lerp(m_readyFromAlpha, m_readyToAlpha, progress);
        GUI.color = tint;
        GUI.DrawTexture(new Rect((screen - extent) / 2f, extent), banner, ScaleMode.ScaleToFit);
        GUI.color = previousColor;
    }

    private static float AnimationProgress(float elapsed, float duration)
    {
        return duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
    }

    private Rect GetResultStarRect(int index)
    {
        float size = Mathf.Min(Screen.width, Screen.height) * m_starSizeRatio;
        return new Rect((Screen.width - size * ResultStars) / 2f + index * size,
            Screen.height * 0.4f, size, size);
    }

    // 通常の結果表示終了だけが回収演出を開始する。キャンセルとは分離する。
    internal void CompleteResult()
    {
        if (CurrentPhase != Phase.Result) return;
        if (ResultStars > 0 && m_star != null)
        {
            if (m_starCollectionEffect == null)
            {
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    m_starCollectionEffect = root.GetComponentInChildren<StarCollectionEffect>(true);
                    if (m_starCollectionEffect != null) break;
                }
            }
            if (m_starCollectionEffect != null)
            {
                var stars = new Rect[ResultStars];
                for (int i = 0; i < stars.Length; i++) stars[i] = GetResultStarRect(i);
                m_starCollectionEffect.Play(m_star, stars, m_lastStarToAngle,
                    m_starCollectDelaySeconds, m_starFlySeconds, m_starArcHeightRatio);
            }
            else Debug.LogWarning("PlayerDisplay/Star の StarCollectionEffect が未設定です。", this);
        }
        m_stateMachine.ChangeState<MiniGameUIClosedState>();
    }

    public void BeginMiniGame()
    {
        if (m_externalControl || !isActiveAndEnabled || !m_stateMachine.enabled ||
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
    public void FinishMiniGame() => FinishMiniGame(2);

    public void FinishMiniGame(int stars)
    {
        if (m_externalControl || !isActiveAndEnabled || CurrentPhase != Phase.Playing) return;
        ResultStars = RemainingSeconds > 0f ? Mathf.Clamp(stars, 0, 3) : 0;
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
        if (phase == Phase.Closed || phase == Phase.Countdown) ResultStars = 0;
        CurrentPhase = phase;
        m_phaseStartedAt = Time.unscaledTime;
        if (!m_externalControl && (phase == Phase.Closed || phase == Phase.Playing)) RestorePlayerControls();
        else if (!m_externalControl)
            foreach (Behaviour control in m_controlSnapshot.Keys)
                if (control != null) control.enabled = false;

        UpdateVisibility();
        // Closed は通常操作に戻す。CanControlPlayer はミニゲーム内操作の可否。
        if (!m_externalControl) m_onPlayerControlChanged.Invoke(phase == Phase.Closed || phase == Phase.Playing);
        if (phase == Phase.Closed) m_controlSnapshot.Clear();
        if (m_timeLabel != null)
        {
            m_timeLabel.enabled = phase == Phase.Playing;
            if (phase == Phase.Playing) UpdateTimer();
        }
        OnPhaseChanged(phase);
    }

    private void UpdateTimer()
    {
        if (m_timeLabel == null) return;
        if (m_externalControl && !m_externalHasTimer) m_timeLabel.SetText("--");
        else m_timeLabel.SetText("{0}", Mathf.CeilToInt(RemainingSeconds));
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
