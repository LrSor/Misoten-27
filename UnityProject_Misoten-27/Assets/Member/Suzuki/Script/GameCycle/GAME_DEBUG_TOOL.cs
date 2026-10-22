using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// ゲームサイクル確認用のデバッグ機能（エディターと開発ビルドのみ）
//
//   F1 : 1P に 100 点
//   F2 : 2P に 100 点
//   F3 : 3P に 100 点
//   F4 : 4P に 100 点
//   F5 : 残り時間を 5 秒にする（すぐタイムアップを確認したいとき）
//
// 他のメンバーの数字キー操作とかぶらないように F キーを使っている
// Game シーンの空のオブジェクトなどに付ける。本番では外してよい
//==================================================
public class GAME_DEBUG_TOOL : MonoBehaviour
{
    [SerializeField]
    private int m_debugScore = 100;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f1Key.wasPressedThisFrame) AddDebugScore(0);
        if (keyboard.f2Key.wasPressedThisFrame) AddDebugScore(1);
        if (keyboard.f3Key.wasPressedThisFrame) AddDebugScore(2);
        if (keyboard.f4Key.wasPressedThisFrame) AddDebugScore(3);

        if (keyboard.f5Key.wasPressedThisFrame &&
            GAME_MANAGER.Instance != null)
        {
            GAME_MANAGER.Instance.SetRemainingTime(5f);
        }
    }

    void AddDebugScore(int playerIndex)
    {
        GAME_SCORE.AddScore(playerIndex, m_debugScore);

        Debug.Log(
            "【デバッグ】" + (playerIndex + 1) + "P に " + m_debugScore +
            " 点（合計 " + GAME_SCORE.TotalScore + "）");
    }
#endif
}
