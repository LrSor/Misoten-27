using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>入力・表示確認用。F 開始、Escape 終了。操作値は別スクリプトから受信する。</summary>
public class TestUIController : MiniGameUIBase
{
    private Vector2 m_vector2Input;
    private bool m_buttonPressed;
    private int m_buttonPressCount;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.fKey.wasPressedThisFrame) BeginMiniGame();
        if (keyboard.escapeKey.wasPressedThisFrame) FinishMiniGame();
    }

    protected override void OnMiniGameInputUpdate(Vector2 value, bool isPressed)
    {
        m_vector2Input = value;
        m_buttonPressed = isPressed;
    }

    protected override void OnMiniGameButtonChanged(bool isPressed)
    {
        m_buttonPressed = isPressed;
        if (isPressed) m_buttonPressCount++;
    }

    protected override void OnPhaseChanged(Phase phase)
    {
        m_vector2Input = Vector2.zero;
        m_buttonPressed = false;
        if (phase == Phase.Explanation) m_buttonPressCount = 0;
    }

    // 本番 UI は基底クラスの表示参照と OnPhaseChanged を使って実装する。
    private void OnGUI()
    {
        if (!IsRunning) return;
        float width = Mathf.Min(560f, Screen.width - 20f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, 30f, width, 340f);
        GUI.Box(panel, "MINI GAME / UI BASE TEST");
        string message = CurrentPhase == Phase.Explanation
            ? $"HOW TO PLAY   {Mathf.CeilToInt(RemainingSeconds)}\nSTICK / ARROW KEYS / MOUSE  ->  VECTOR2\nSL / SPACE / LEFT CLICK  ->  BUTTON\nPlease watch. Controls are locked."
            : CurrentPhase == Phase.Playing
                ? $"START!  Vector2: {m_vector2Input}\nButton: {(m_buttonPressed ? "HELD" : "RELEASED")}   Press count: {m_buttonPressCount}\nESC  ->  FINISH TEST"
                : $"FINISHED   {Mathf.CeilToInt(RemainingSeconds)}\nControls are locked. Closing soon.";
        GUI.Label(new Rect(panel.x + 20f, panel.y + 35f, width - 40f, 95f), message);
        Rect field = new Rect(panel.x + 20f, panel.y + 140f, width - 40f, 175f);
        GUI.Box(field, "");
        Vector2 vector = CurrentPhase == Phase.Explanation
            ? new Vector2(Mathf.Sin(Time.unscaledTime * 2f), 0f)
            : Vector2.ClampMagnitude(m_vector2Input, 1f);
        Vector2 position = new Vector2(0.5f + vector.x * 0.4f, 0.5f - vector.y * 0.4f);
        GUI.Box(new Rect(field.x + position.x * (field.width - 30f),
            field.y + position.y * (field.height - 30f), 30f, 30f), m_buttonPressed ? "ON" : "+");
    }
}
