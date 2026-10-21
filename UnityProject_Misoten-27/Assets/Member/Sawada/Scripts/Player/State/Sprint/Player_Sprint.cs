using UnityEngine;

public class Player_Sprint : BaseState
{
    [SerializeField] float m_sprintMaxSpeed = 10.0f;
    [SerializeField] float m_sprintMinSpeed = 5.0f;
    [SerializeField] float m_sprintTime = 5.0f;
    float m_sprintTimer;
    PlayerMoveController m_controller;
    StateMachine m_stateMachine;

    private void Awake()
    {
        m_controller = GetComponent<PlayerMoveController>();
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void InitState()
    {
        m_sprintTimer = 0f;
        m_controller.SetRushFlag(true);
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
