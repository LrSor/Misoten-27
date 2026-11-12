using UnityEngine;

public class Player_FreeCamera : BaseState
{
    TestPlayerCameraLookController m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        m_controller = GetComponent<TestPlayerCameraLookController>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        // 視界の回転
        m_controller.Look();
    }

    public override void FixedUpdateState()
    {
    }
}
