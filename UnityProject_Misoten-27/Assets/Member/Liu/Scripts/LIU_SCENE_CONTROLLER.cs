using UnityEngine;
using UnityEngine.SceneManagement;

public class LIU_SCENE_CONTROLLER : MonoBehaviour
{
    public void LoadGame()
    {
        SceneManager.LoadScene("LiuGameScene");
    }

    public void LoadResult()
    {
        SceneManager.LoadScene("LiuResultScene");
    }

    public void LoadTitle()
    {
        SceneManager.LoadScene("LiuTitleScene");
    }
}
