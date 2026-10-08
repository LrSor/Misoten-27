using UnityEngine;

public class Player_Walk : BaseState
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
    }

    public override void FixedUpdateState()
    {
        // 移動と回転
        m_controller.Move(m_walkSpeed);
        m_controller.Rotate();

        // 入力がない場合はIdleに遷移
        if (!m_controller.HasMoveInput)
        {
            m_stateMachine.ChangeState<Player_Idle>();
        }

        // タックル状態へ遷移    仮pcテスト用
        if (m_controller.PressSprint)
        {
            m_stateMachine.ChangeState<Player_Sprint>();
        }
    }
}
