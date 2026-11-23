using UnityEngine;
using UnityEngine.InputSystem;

public class TestUIController : MiniGameUIBase
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            BeginMiniGame();
    }

    protected override void OnMiniGameButtonChanged(bool isPressed)
    {
        if (isPressed) FinishMiniGame(2);
    }

    protected override void OnGUI()
    {
        base.OnGUI();
        if (!IsExternallyDriven && CanControlPlayer)
            GUI.Label(new Rect(20, Screen.height - 60, 320, 40), "SPACE / SL / CLICK  -  CLEAR");
    }
}
