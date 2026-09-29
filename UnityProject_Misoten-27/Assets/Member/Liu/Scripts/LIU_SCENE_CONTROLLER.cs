//using System.Collections;
//using UnityEngine;
//using UnityEngine.SceneManagement;
//using UnityEngine.UI;

//public class LIU_SCENE_CONTROLLER : MonoBehaviour
//{
//    private static bool g_transitionFlag;
//    private bool m_transitionFlag;

//    [Header("トランジション設定")]

//    [SerializeField, InspectorName("表示する画像")]
//    private Sprite m_transitionImage;

//    [SerializeField, Min(0.1f), InspectorName("片道の移動時間（秒）")]
//    private float m_moveDuration = 1.2f;

//    public void LoadGame()
//    {
//        StartTransition("LiuGameScene");
//    }

//    public void LoadResult()
//    {
//        StartTransition("LiuResultScene");
//    }

//    public void LoadTitle()
//    {
//        StartTransition("LiuTitleScene");
//    }

//    private void StartTransition(string sceneName)
//    {
//        // 遷移中の連続クリックを防ぐ。
//        if (g_transitionFlag)
//        {
//            return;
//        }

//        if (!Application.CanStreamedLevelBeLoaded(sceneName))
//        {
//            Debug.LogError(
//                "シーンがビルドプロファイルに登録されていません：" + sceneName
//            );
//            return;
//        }

//        g_transitionFlag = true;
//        m_transitionFlag = true;

//        // 演出が終了するまで、このオブジェクトを保持する。
//        transform.SetParent(null);
//        DontDestroyOnLoad(gameObject);

//        StartCoroutine(PlayTransition(sceneName));
//    }

//    private IEnumerator PlayTransition(string sceneName)
//    {
//        // 他の UI より手前に表示するキャンバスを生成する。
//        GameObject canvasObject = new GameObject(
//            "TransitionCanvas",
//            typeof(RectTransform),
//            typeof(Canvas),
//            typeof(GraphicRaycaster)
//        );

//        canvasObject.transform.SetParent(transform, false);

//        Canvas canvas = canvasObject.GetComponent<Canvas>();
//        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//        canvas.sortingOrder = 32767;

//        // 画面を覆う黒いパネルを生成する。
//        GameObject panelObject = new GameObject(
//            "TransitionPanel",
//            typeof(RectTransform),
//            typeof(Image)
//        );

//        panelObject.transform.SetParent(canvasObject.transform, false);

//        Image panelImage = panelObject.GetComponent<Image>();
//        panelImage.color = Color.black;

//        RectTransform panel = panelObject.GetComponent<RectTransform>();
//        panel.anchorMin = Vector2.zero;
//        panel.anchorMax = Vector2.one;
//        panel.offsetMin = Vector2.zero;
//        panel.offsetMax = Vector2.zero;

//        // 黒いパネルの上に画像を表示する。
//        if (m_transitionImage != null)
//        {
//            GameObject imageObject = new GameObject(
//                "TransitionImage",
//                typeof(RectTransform),
//                typeof(Image)
//            );

//            imageObject.transform.SetParent(panel, false);

//            Image transitionImage = imageObject.GetComponent<Image>();
//            transitionImage.sprite = m_transitionImage;
//            transitionImage.color = Color.white;
//            transitionImage.preserveAspect = false;
//            transitionImage.raycastTarget = false;

//            RectTransform imageRect =
//                imageObject.GetComponent<RectTransform>();

//            imageRect.anchorMin = Vector2.zero;
//            imageRect.anchorMax = Vector2.one;
//            imageRect.offsetMin = Vector2.zero;
//            imageRect.offsetMax = Vector2.zero;

//            // パネルの外側にはみ出した画像を非表示にする。
//            panelObject.AddComponent<RectMask2D>();

//            // 縦横比を維持したまま、画像を画面全体に広げる。
//            AspectRatioFitter imageFitter =
//                imageObject.AddComponent<AspectRatioFitter>();

//            imageFitter.aspectRatio =
//                m_transitionImage.rect.width / m_transitionImage.rect.height;

//            imageFitter.aspectMode =
//                AspectRatioFitter.AspectMode.EnvelopeParent;


//    }

//        // パネルの移動中に次のシーンを先読みする。
//        AsyncOperation sceneOperation =
//            SceneManager.LoadSceneAsync(sceneName);

//        // 画面が完全に隠れるまではシーンを切り替えない。
//        sceneOperation.allowSceneActivation = false;

//        // 左からパネルを移動させ、画面全体を覆う。
//        yield return SlidePanel(panel, -Screen.width, 0f);

//        // 先読みしたシーンに切り替える。
//        sceneOperation.allowSceneActivation = true;
//        yield return sceneOperation;

//        // パネルを右へ移動させ、次の画面を表示する。
//        yield return SlidePanel(panel, 0f, Screen.width);

//        // 次のシーンでは、そのシーンのコントローラーを使用する。
//        Destroy(gameObject);
//    }

//    private IEnumerator SlidePanel(
//        RectTransform panel, float startX, float endX)
//    {
//        // パネルの移動にかかる時間（秒）。
//        float duration = Mathf.Max(0.1f, m_moveDuration);
//        float elapsedTime = 0f;

//        panel.anchoredPosition = new Vector2(startX, 0f);

//        while (elapsedTime < duration)
//        {
//            elapsedTime += Time.unscaledDeltaTime;

//            float progress = Mathf.Clamp01(elapsedTime / duration);
//            float positionX = Mathf.Lerp(startX, endX, progress);

//            panel.anchoredPosition = new Vector2(positionX, 0f);
//            yield return null;
//        }

//        panel.anchoredPosition = new Vector2(endX, 0f);
//    }

//    private void OnDestroy()
//    {
//        // 遷移を担当したオブジェクトの破棄時にフラグを解除する。
//        if (m_transitionFlag)
//        {
//            g_transitionFlag = false;
//        }
//    }
//}
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LIU_SCENE_CONTROLLER : MonoBehaviour
{
    private static bool g_transitionFlag;
    private bool m_transitionFlag;
    private Texture2D m_previousFrame;

    [Header("トランジション設定")]
    [SerializeField, InspectorName("表示する画像")]
    private Sprite m_transitionImage;

    [SerializeField, Min(0.1f), InspectorName("片道の移動時間（秒）")]
    private float m_moveDuration = 2.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTransition()
    {
        g_transitionFlag = false;
    }

    public void LoadGame()
    {
        StartTransition("LiuGameScene");
    }

    public void LoadResult()
    {
        StartTransition("LiuResultScene");
    }

    public void LoadTitle()
    {
        StartTransition("LiuTitleScene");
    }

    private void StartTransition(string sceneName)
    {
        if (g_transitionFlag)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("シーンがビルドプロファイルに登録されていません：" + sceneName);
            return;
        }

        g_transitionFlag = true;
        m_transitionFlag = true;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(PlayTransition(sceneName));
    }

    private IEnumerator PlayTransition(string sceneName)
    {
        // 描画完了後、現在の画面を画像として保持する。
        yield return new WaitForEndOfFrame();
        Texture2D capturedFrame = ScreenCapture.CaptureScreenshotAsTexture();

        // キャプチャ画像を sRGB として扱い、色の白浮きを防ぐ。
        m_previousFrame = new Texture2D(
            capturedFrame.width,
            capturedFrame.height,
            TextureFormat.RGBA32,
            false,
            false
        );

        m_previousFrame.SetPixels32(capturedFrame.GetPixels32());
        m_previousFrame.Apply(false, true);
        Destroy(capturedFrame);

        GameObject canvasObject = new GameObject(
            "TransitionCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        // 読み込み中も旧画面を表示し、背後の UI 操作を遮断する。
        GameObject snapshotObject = new GameObject(
            "PreviousScreen", typeof(RectTransform), typeof(RawImage));
        snapshotObject.transform.SetParent(canvasObject.transform, false);
        StretchToParent(snapshotObject.GetComponent<RectTransform>());

        RawImage snapshot = snapshotObject.GetComponent<RawImage>();
        snapshot.texture = m_previousFrame;
        snapshot.raycastTarget = true;

        GameObject panelObject = new GameObject(
            "TransitionPanel", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D));
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelObject.GetComponent<Image>().color = Color.black;

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        StretchToParent(panel);
        panel.anchoredPosition = new Vector2(-Screen.width, 0f);

        if (m_transitionImage != null)
        {
            GameObject imageObject = new GameObject(
                "TransitionImage", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(panel, false);

            Image transitionImage = imageObject.GetComponent<Image>();
            transitionImage.sprite = m_transitionImage;
            transitionImage.color = Color.white;
            transitionImage.preserveAspect = false;
            transitionImage.raycastTarget = false;
            StretchToParent(imageObject.GetComponent<RectTransform>());

            // 縦横比を維持し、余白が出ないように拡大する。
            AspectRatioFitter imageFitter =
                imageObject.AddComponent<AspectRatioFitter>();
            imageFitter.aspectRatio =
                m_transitionImage.rect.width / m_transitionImage.rect.height;
            imageFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        // アニメーションの開始前にシーンの読み込みと切り替えを完了する。
        // 新しいシーンは旧画面の画像の背後にあるため、まだ見えない。
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;
        Canvas.ForceUpdateCanvases();

        // 左端から右端まで、一つのループで連続して移動する。
        float duration = Mathf.Max(0.1f, m_moveDuration) * 2f;
        double startTime = Time.unscaledTimeAsDouble;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        while (true)
        {
            float elapsedTime = (float)(Time.unscaledTimeAsDouble - startTime);
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float width = canvasRect.rect.width;
            float positionX = Mathf.Lerp(-width, width, progress);
            panel.anchoredPosition = new Vector2(positionX, 0f);

            if (progress >= 0.5f)
            {
                // 全面が覆われる位置を通過したら旧画面だけを透明にする。
                // 透明化後もクリックの遮断は継続する。
                snapshot.color = Color.clear;
            }

            if (progress >= 1f)
            {
                break;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (m_previousFrame != null)
        {
            Destroy(m_previousFrame);
        }

        if (m_transitionFlag)
        {
            g_transitionFlag = false;
        }
    }
}
