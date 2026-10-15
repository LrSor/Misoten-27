

public class Player_Swap : BaseState
{
    PlayerSwapController m_controller;
    StateMachine m_stateMachine;


    private void Awake()
    {
        m_controller = GetComponent<PlayerSwapController>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void InitState()
    {
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        if (m_controller.Swap())
        {
            m_stateMachine.ChangeState<Player_SwapIdle>();
        }
    }

    public override void FixedUpdateState()
    {
    }
}
