using UnityEngine;

public class Player_Sprint : BaseState
{
    [SerializeField] float m_sprintMaxSpeed = 10.0f;
    [SerializeField] float m_sprintMinSpeed = 5.0f;
    [SerializeField] float m_sprintTime = 5.0f;
    float m_sprintTimer;
    PlayerController m_controller;
    StateMachine m_stateMachine;

    public override void InitState()
    {
        m_controller = GetComponent<PlayerController>();
        m_stateMachine = GetComponent<StateMachine>();

        m_sprintTimer = 0f;
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
    }

    public override void FixedUpdateState()
    {
        m_controller.AutoMove(m_sprintMaxSpeed, m_sprintMinSpeed, m_sprintTime, m_sprintTimer);
        m_controller.Rotate();

        m_sprintTimer += Time.fixedDeltaTime;

        if (m_sprintTimer >= m_sprintTime)
        {
            m_stateMachine.ChangeState<Player_Walk>();
        }
    }
}
