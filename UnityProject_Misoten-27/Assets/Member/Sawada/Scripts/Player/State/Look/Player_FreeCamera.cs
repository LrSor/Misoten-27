using UnityEngine;

public class Player_FreeCamera : BaseState
{
    [SerializeField]PlayerCameraLook m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        //m_controller = GetComponent<PlayerCameraLook>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
    }

    public override void FixedUpdateState()
    {
        // 視界の回転
        m_controller.Look();
    }
}
