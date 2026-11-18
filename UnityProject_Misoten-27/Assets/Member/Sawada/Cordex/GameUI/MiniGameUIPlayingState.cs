using UnityEngine;

public sealed class MiniGameUIPlayingState : BaseState
{
    private MiniGameUIBase m_ui;
    public Vector2 Vector2Input { get; private set; }
    public bool IsButtonPressed { get; private set; }

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        ResetInput();
        m_ui.EnterPhase(MiniGameUIBase.Phase.Playing);
    }

    // デバイスは参照せず、外部の入力取得スクリプト → UI の関数経由で値を受け取る。
    internal void ReceiveVector2Input(Vector2 value)
    {
        Vector2Input = value;
    }

    internal void ReceiveButtonInput(bool isPressed)
    {
        if (IsButtonPressed == isPressed) return;
        IsButtonPressed = isPressed;
        m_ui.NotifyMiniGameButton(isPressed);
    }

    public override void UpdateState()
    {
        if (m_ui.isActiveAndEnabled)
            m_ui.UpdateMiniGameInput(Vector2Input, IsButtonPressed);
    }

    public override void UnInitState()
    {
        ResetInput();
    }

    private void ResetInput()
    {
        Vector2Input = Vector2.zero;
        IsButtonPressed = false;
    }
}
