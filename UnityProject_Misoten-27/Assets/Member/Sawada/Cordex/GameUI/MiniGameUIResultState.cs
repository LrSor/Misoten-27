public sealed class MiniGameUIResultState : BaseState
{
    private MiniGameUIBase m_ui;

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        m_ui.EnterPhase(MiniGameUIBase.Phase.Result);
    }

    public override void UpdateState()
    {
        if (m_ui.RemainingSeconds <= 0f)
            GetComponent<StateMachine>().ChangeState<MiniGameUIClosedState>();
    }
}
