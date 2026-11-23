using UnityEngine;
using UnityEngine.UI;

// 既存 Scene の State 接続を保ち、開始前の3秒を担当する。
public sealed class MiniGameUIExplanationState : BaseState
{
    [SerializeField] private Image m_callIcon;
    [SerializeField] private RectTransform m_speechBubble;
    private MiniGameUIBase m_ui;

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        if (m_callIcon != null) m_callIcon.gameObject.SetActive(false);
        if (m_speechBubble != null) m_speechBubble.gameObject.SetActive(false);
        m_ui.EnterPhase(MiniGameUIBase.Phase.Countdown);
    }

    public override void UpdateState()
    {
        if (m_ui.PhaseElapsedSeconds >= m_ui.CountdownSeconds)
            GetComponent<StateMachine>().ChangeState<MiniGameUIPlayingState>();
    }
}
