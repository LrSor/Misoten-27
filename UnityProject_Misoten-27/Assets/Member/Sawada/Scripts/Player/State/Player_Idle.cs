using UnityEngine;

public class Player_Idle : BaseState
{
    PlayerController m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        m_controller = GetComponent<PlayerController>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        if (m_controller.HasMoveInput)
        {
            m_stateMachine.ChangeState<Player_Walk>();
        }
    }

    public override void FixedUpdateState()
    {
    }
}
