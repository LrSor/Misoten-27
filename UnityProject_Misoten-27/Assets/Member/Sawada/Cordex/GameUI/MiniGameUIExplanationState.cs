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
    private MiniGameUIBase m_ui;

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
        if (elapsed >= Duration)
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
        float bubbleCloseStart = bubbleOpenEnd + BubbleHoldSeconds;

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
