using UnityEngine;
using UnityEngine.SceneManagement;

//==================================================
// シーン遷移をまとめる場所
//
// 劉さんの LIU_SCENE_CONTROLLER（トランジション演出）を使って遷移する
//   ・シーンに LIU_SCENE_CONTROLLER が置いてあれば、それを使う
//     （画像や時間の設定はそのオブジェクトのもの）
//   ・置いていなければ自動で作って使う（画像なし・初期設定の時間）
//
// ・遷移中に呼ばれても無視する（連打しても1回だけ）
// ・Build Settings に登録されていないシーンは分かりやすいエラーを出す
//
// 他のスクリプトは GAME_SCENE_LOADER.IsTransitioning を見れば
// 「トランジション中か（新しいシーンがまだ見えていないか）」が分かる
//==================================================
public static class GAME_SCENE_LOADER
{
    // トランジション中か（演出が終わるまで true）
    public static bool IsTransitioning
    {
        get { return LIU_SCENE_CONTROLLER.IsTransitioning; }
    }

    public static void Load(string sceneName)
    {
        // すでに遷移中なら何もしない
        if (IsTransitioning)
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

        LIU_SCENE_CONTROLLER controller =
            FindController();

        controller.LoadScene(sceneName);
    }

    // 今のシーンにあるトランジション用コントローラーを探す。無ければ作る
    static LIU_SCENE_CONTROLLER FindController()
    {
        Scene activeScene =
            SceneManager.GetActiveScene();

        LIU_SCENE_CONTROLLER[] controllers =
            Object.FindObjectsByType<LIU_SCENE_CONTROLLER>(FindObjectsSortMode.None);

        // 今のシーンに置いてあるものを優先する
        foreach (LIU_SCENE_CONTROLLER controller in controllers)
        {
            if (controller.gameObject.scene == activeScene)
            {
                return controller;
            }
        }

        Debug.Log(
            "シーン「" + activeScene.name +
            "」に LIU_SCENE_CONTROLLER が無いので、初期設定（画像なし）のものを自動で作りました");

        // 遷移中はこのオブジェクトごと次のシーンへ持ち越されるので、
        // 他のスクリプトを付けない専用のオブジェクトにする
        GameObject controllerObject =
            new GameObject("SceneController (自動作成)");

        return controllerObject.AddComponent<LIU_SCENE_CONTROLLER>();
    }
}
