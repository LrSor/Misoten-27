using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleButton : MonoBehaviour
{
    // 表示 非表示
    public void ViewTrue(GameObject target)
    {
        target.SetActive(true);
    }
    public void ViewFalse(GameObject target)
    {
        target.SetActive(false);
    }

    // シーン読み込み
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // ゲーム終了
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
