using UnityEngine;

//==================================================
// タイトル画面
//
// ・決定入力（Enter / Space / ゲームパッドの Start・決定）でゲームシーンへ
// ・UI の Button の OnClick に StartGame() を登録しても使える
// ・前回のゲームのスコアなどをここでリセットする
//==================================================
public class GAME_TITLE : MonoBehaviour
{
    [Header("シーン")]
    [SerializeField]
    private string m_gameSceneName = "Game";

    [Header("入力")]
    [Tooltip("シーンが始まってから入力を受け付けるまでの時間（リザルトからの押しっぱなしで即スタートしないように）")]
    [SerializeField]
    private float m_inputDelay = 0.5f;

    [Header("「Press Start」表示（任意）")]
    [Tooltip("点滅させるオブジェクト。空欄でも動く")]
    [SerializeField]
    private GameObject m_pressStartObject;

    [Tooltip("点滅の間隔（秒）。0 で点滅しない")]
    [SerializeField]
    private float m_blinkInterval = 0.5f;

    private float m_timer;

    void Start()
    {
        // 前のゲームの状態を元に戻す
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GAME_SCORE.ResetScores();
        GAME_SCORE.SetLocked(true);
    }

    void Update()
    {
        // トランジション中（画面がまだ見えていない間）は待つ
        // 入力受付までの時間も、トランジションが終わってから数える
        if (GAME_SCENE_LOADER.IsTransitioning)
        {
            m_timer = 0f;
            return;
        }

        m_timer += Time.unscaledDeltaTime;

        UpdateBlink();

        if (m_timer < m_inputDelay)
        {
            return;
        }

        if (GAME_INPUT.WasSubmitPressed())
        {
            StartGame();
        }
    }

    // UI の Button からも呼べるように public にしている
    public void StartGame()
    {
        if (GAME_SCENE_LOADER.IsTransitioning)
        {
            return;
        }

        GAME_SCENE_LOADER.Load(m_gameSceneName);
    }

    void UpdateBlink()
    {
        if (m_pressStartObject == null ||
            m_blinkInterval <= 0f)
        {
            return;
        }

        bool isVisible =
            Mathf.FloorToInt(m_timer / m_blinkInterval) % 2 == 0;

        if (m_pressStartObject.activeSelf != isVisible)
        {
            m_pressStartObject.SetActive(isVisible);
        }
    }
}
