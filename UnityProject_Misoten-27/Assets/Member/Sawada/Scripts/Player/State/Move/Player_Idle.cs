using UnityEngine;

public class Player_Idle : BaseState
{
    PlayerMoveController m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        m_controller = GetComponent<PlayerMoveController>();
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
        // オフラインでもノックバック後の残速度を減速させる。
        m_controller.Decelerate();
        if (m_controller.HasNetworkInput)
        {
            m_controller.Rotate();
        }
    }
}
