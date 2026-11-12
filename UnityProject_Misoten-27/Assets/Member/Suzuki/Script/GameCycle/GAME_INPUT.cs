using UnityEngine.InputSystem;

//==================================================
// タイトル・リザルトで使う「決定」入力
//
// キーボード：Enter / Space
// ゲームパッド：Start / 決定ボタン（下ボタン）
// どのゲームパッドからでも受け付ける
//==================================================
public static class GAME_INPUT
{
    public static bool WasSubmitPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame))
        {
            return true;
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.startButton.wasPressedThisFrame ||
                gamepad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }
}
