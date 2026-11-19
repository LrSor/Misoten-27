using UnityEngine;
using UnityEngine.UI;

public sealed class MiniGameUIExplanationState : BaseState
{
    private const float CallScaleSeconds = 0.2f;
    private const float CallPulseSeconds = 0.4f;
    private const int CallPulseCount = 2;
    private const float CallFadeSeconds = 0.4f;
    private const float BubbleScaleSeconds = 0.1f;
    private const float BubbleHoldSeconds = 3f;
    public const float Duration = CallScaleSeconds + CallPulseSeconds * CallPulseCount +
        CallFadeSeconds + BubbleScaleSeconds + BubbleHoldSeconds + BubbleScaleSeconds;

    [SerializeField] private Image m_callIcon;
    [SerializeField] private RectTransform m_speechBubble;
    [SerializeField] private UISpriteSequencePlayer m_speechAnimation;
    private bool m_animationStarted;
    private float m_bubbleCloseStartedAt = -1f;
    private MiniGameUIBase m_ui;
    public float ExpectedDuration => Duration - BubbleHoldSeconds +
        (m_speechAnimation != null ? m_speechAnimation.DurationSeconds : BubbleHoldSeconds);

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        ResetPresentation();
        m_ui.EnterPhase(MiniGameUIBase.Phase.Explanation);
    }

    public override void UpdateState()
    {
        float elapsed = m_ui.PhaseElapsedSeconds;
        ApplyAnimation(elapsed);
        bool finished = m_speechAnimation != null
            ? m_bubbleCloseStartedAt >= 0f && elapsed - m_bubbleCloseStartedAt >= BubbleScaleSeconds
            : elapsed >= Duration;
        if (finished)
            GetComponent<StateMachine>().ChangeState<MiniGameUIPlayingState>();
    }

    public override void UnInitState()
    {
        // 中断・再開始時にも、透明度や拡大率を持ち越さない。
        ResetPresentation();
    }

    private void ApplyAnimation(float elapsed)
    {
        float pulseEnd = CallScaleSeconds + CallPulseSeconds * CallPulseCount;
        float fadeEnd = pulseEnd + CallFadeSeconds;
        float bubbleOpenEnd = fadeEnd + BubbleScaleSeconds;
        float bubbleCloseStart = m_speechAnimation != null ? float.PositiveInfinity : bubbleOpenEnd + BubbleHoldSeconds;

        if (m_speechAnimation != null && !m_animationStarted &&
            elapsed >= bubbleOpenEnd)
        {
            m_animationStarted = true;
            // 説明では1回だけ再生。未設定のコマで開始できない場合も閉じる。
            if (!m_speechAnimation.PlayOnce()) m_bubbleCloseStartedAt = elapsed;
        }
        if (m_speechAnimation != null && m_animationStarted)
        {
            if (m_speechAnimation.HasCompleted && m_bubbleCloseStartedAt < 0f)
                m_bubbleCloseStartedAt = elapsed;
            if (m_bubbleCloseStartedAt >= 0f) bubbleCloseStart = m_bubbleCloseStartedAt;
        }

        if (m_callIcon != null)
        {
            m_callIcon.rectTransform.localScale = Vector3.one * Mathf.Clamp01(elapsed / CallScaleSeconds);
            float alpha = 1f;
            if (elapsed >= fadeEnd) alpha = 0f;
            else if (elapsed >= pulseEnd) alpha = 1f - (elapsed - pulseEnd) / CallFadeSeconds;
            else if (elapsed >= CallScaleSeconds)
                alpha = Mathf.Lerp(1f, 0.5f,
                    Mathf.PingPong((elapsed - CallScaleSeconds) / (CallPulseSeconds * 0.5f), 1f));
            Color color = m_callIcon.color;
            color.a = alpha;
            m_callIcon.color = color;
        }

        if (m_speechBubble != null)
        {
            float scale = elapsed < bubbleCloseStart
                ? Mathf.Clamp01((elapsed - fadeEnd) / BubbleScaleSeconds)
                : 1f - Mathf.Clamp01((elapsed - bubbleCloseStart) / BubbleScaleSeconds);
            m_speechBubble.localScale = Vector3.one * scale;
        }
    }

    private void ResetPresentation()
    {
        m_animationStarted = false;
        m_bubbleCloseStartedAt = -1f;
        if (m_speechAnimation != null) m_speechAnimation.Stop();
        if (m_callIcon != null)
        {
            m_callIcon.rectTransform.localScale = Vector3.zero;
            Color color = m_callIcon.color;
            color.a = 1f;
            m_callIcon.color = color;
        }
        if (m_speechBubble != null) m_speechBubble.localScale = Vector3.zero;
    }
}
