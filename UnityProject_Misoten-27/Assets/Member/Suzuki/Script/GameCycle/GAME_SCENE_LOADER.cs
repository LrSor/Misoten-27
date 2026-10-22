using UnityEngine;
using UnityEngine.SceneManagement;

//==================================================
// シーン遷移をまとめる場所
//
// ・連打しても1回しか遷移しない
// ・Build Settings に登録されていないシーンは分かりやすいエラーを出す
//
// あとで劉さんのトランジション演出に差し替えるときは、
// Load() の中身だけ変えれば全シーンに反映される
//==================================================
public static class GAME_SCENE_LOADER
{
    private static bool s_isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        s_isLoading = false;

        // 二重登録しないように一度外してから登録
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        s_isLoading = false;
    }

    public static void Load(string sceneName)
    {
        if (s_isLoading)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "シーン「" + sceneName +
                "」が Build Settings に登録されていません（シーン名のつづりも確認してください）");

            return;
        }

        s_isLoading = true;

        SceneManager.LoadScene(sceneName);
    }
}
