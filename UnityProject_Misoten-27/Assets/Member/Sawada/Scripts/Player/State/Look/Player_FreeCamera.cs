using UnityEngine;

public class Player_FreeCamera : BaseState
{
    [SerializeField] float m_walkSpeed = 5.0f;
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
        if (!m_controller.HasMoveInput)
        {
            m_stateMachine.ChangeState<Player_Idle>();
        }
    }

    public override void FixedUpdateState()
    {
        // 視界の回転
        m_controller.Move(m_walkSpeed);
        m_controller.Rotate();
    }
}
