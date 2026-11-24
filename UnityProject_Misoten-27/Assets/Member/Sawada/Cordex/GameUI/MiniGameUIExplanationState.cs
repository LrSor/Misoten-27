using UnityEngine;
using UnityEngine.UI;

// 既存 Scene の State 接続を保ち、開始前の3秒を担当する。
public sealed class MiniGameUIExplanationState : BaseState
{
    [SerializeField, HideInInspector] private Image m_callIcon;
    [SerializeField] private RectTransform m_speechBubble;
    private MiniGameUIBase m_ui;

    // 既存 Scene / Prefab の参照を保持したまま、リザルト側で再利用する。
    internal RectTransform SpeechBubble => m_speechBubble;

    private void Awake() => HideCallIcon();

    internal void HideCallIcon()
    {
        // 古い Prefab の参照だけ保持し、CallIcon は使用しない。
        if (m_callIcon != null) m_callIcon.gameObject.SetActive(false);
    }

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        HideCallIcon();
        if (m_speechBubble != null) m_speechBubble.gameObject.SetActive(false);
        m_ui.EnterPhase(MiniGameUIBase.Phase.Countdown);
    }

    public override void UpdateState()
    {
        if (!m_ui.IsExternallyDriven && m_ui.PhaseElapsedSeconds >= m_ui.CountdownSeconds)
            GetComponent<StateMachine>().ChangeState<MiniGameUIPlayingState>();
    }
}
