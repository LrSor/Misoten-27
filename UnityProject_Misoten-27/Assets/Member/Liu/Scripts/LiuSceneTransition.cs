using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LiuSceneTransition : MonoBehaviour
{
    // 移動先のシーン名
    [SerializeField] private string targetSceneName = "LiuMiniGameScene";

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // キーボードが接続されていない場合は処理しない
        if (keyboard == null)
        {
            return;
        }

        // キーボード上段の1キーを押したらシーンを切り替える
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
