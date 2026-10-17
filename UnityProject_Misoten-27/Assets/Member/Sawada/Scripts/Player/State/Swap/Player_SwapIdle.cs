using UnityEngine;

public class Player_SwapIdle : BaseState
{
    StateMachine m_stateMachine;

    [SerializeField] float m_swapIdleTime = 0.5f;
    float m_swapIdleTimer;


    private void Awake()
    {
        m_stateMachine = GetComponent<StateMachine>();
    }

    public override void InitState()
    {
        m_swapIdleTimer = 0f;
    }

    public override void UnInitState()
    {
    }

    public override void UpdateState()
    {
        m_swapIdleTimer += Time.deltaTime;
    }

    public override void FixedUpdateState()
    {
        if (m_swapIdleTime <= m_swapIdleTimer)
        {
            m_stateMachine.ChangeState<Player_Swap>();
        }
    }
}
