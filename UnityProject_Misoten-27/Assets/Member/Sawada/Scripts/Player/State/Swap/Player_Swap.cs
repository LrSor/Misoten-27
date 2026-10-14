using System.Collections.Generic;

public class Player_Swap : BaseState
{
    PlayerSwapController m_controller;
    StateMachine m_stateMachine;


    public override void InitState()
    {
        m_controller = GetComponent<PlayerSwapController>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        m_controller.Swap();
    }

    public override void FixedUpdateState()
    {
    }
}
