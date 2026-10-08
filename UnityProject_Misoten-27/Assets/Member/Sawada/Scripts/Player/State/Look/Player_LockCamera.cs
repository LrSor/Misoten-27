using UnityEngine;

public class Player_LockCamera : BaseState
{
    PlayerCameraLook m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        m_controller = GetComponent<PlayerCameraLook>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        if (true)
        {
            //m_stateMachine.ChangeState<>();
        }
    }

    public override void FixedUpdateState()
    {
    }
}
