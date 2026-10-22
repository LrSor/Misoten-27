using TMPro;
using UnityEngine;

//==================================================
// リザルト画面
//
// ・総スコアを 0 から数え上げて表示 → ランク（S ～ C など）を表示
// ・数え上げ中に決定を押すと、すぐに最後まで表示
// ・表示し終わった後に決定を押すとタイトルへ（自動で戻る設定も可）
//==================================================
public class GAME_RESULT : MonoBehaviour
{
    // ランクの判定基準
    [System.Serializable]
    private class RANK
    {
        [Tooltip("表示する文字（S, A, B, C など）")]
        public string m_name = "C";

        [Tooltip("この点数以上ならこのランク")]
        public int m_minScore = 0;

        [Tooltip("ランクの文字色")]
        public Color m_color = Color.white;
    }

    [Header("ランク（点数の高い順に並べなくても自動で判定する）")]
    [SerializeField]
    private RANK[] m_ranks =
    {
        new RANK { m_name = "S", m_minScore = 3000, m_color = new Color(1f, 0.85f, 0.2f) },
        new RANK { m_name = "A", m_minScore = 2000, m_color = new Color(1f, 0.4f, 0.4f) },
        new RANK { m_name = "B", m_minScore = 1000, m_color = new Color(0.4f, 0.7f, 1f) },
        new RANK { m_name = "C", m_minScore = 0,    m_color = Color.white },
    };

    [Header("シーン")]
    [SerializeField]
    private string m_titleSceneName = "Title";

    [Header("演出")]
    [Tooltip("総スコアを数え上げる時間（秒）。0 ですぐ表示")]
    [SerializeField]
    private float m_countUpTime = 1.5f;

    [Tooltip("シーン開始から入力を受け付けるまでの時間（ゲーム中の連打で飛ばされないように）")]
    [SerializeField]
    private float m_inputDelay = 1f;

    [Tooltip("表示し終わってから自動でタイトルに戻るまでの時間（秒）。0 で自動では戻らない")]
    [SerializeField]
    private float m_autoReturnTime = 0f;

    [Header("UI（任意。空欄でも動く）")]
    [SerializeField]
    private TMP_Text m_totalScoreText;

    [SerializeField]
    private TMP_Text m_rankText;

    [Tooltip("プレイヤーごとのスコア（1P: 1200 のように改行で並べる）")]
    [SerializeField]
    private TMP_Text m_playerScoresText;

    [Tooltip("表示したいプレイヤーの人数")]
    [Range(1, GAME_SCORE.MAX_PLAYERS)]
    [SerializeField]
    private int m_playerCount = GAME_SCORE.MAX_PLAYERS;

    [Tooltip("「Press Start でタイトルへ」の表示。数え上げが終わってから出る")]
    [SerializeField]
    private GameObject m_pressToReturnObject;

    private int m_totalScore;
    private float m_timer;
    private float m_finishedTimer;
    private bool m_isCountFinished;

    void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        m_totalScore = GAME_SCORE.TotalScore;

        SetActiveSafe(m_rankText, false);
        SetActiveSafe(m_pressToReturnObject, false);

        ShowPlayerScores();

        if (m_countUpTime <= 0f)
        {
            FinishCountUp();
        }
        else
        {
            SetTotalScoreText(0);
        }
    }

    void Update()
    {
        m_timer += Time.unscaledDeltaTime;

        bool canInput =
            m_timer >= m_inputDelay;

        bool isSubmitPressed =
            canInput && GAME_INPUT.WasSubmitPressed();

        if (!m_isCountFinished)
        {
            UpdateCountUp();

            // 数え上げ中に押したら最後まで飛ばす
            if (isSubmitPressed)
            {
                FinishCountUp();
            }

            return;
        }

        m_finishedTimer += Time.unscaledDeltaTime;

        bool isAutoReturn =
            m_autoReturnTime > 0f &&
            m_finishedTimer >= m_autoReturnTime;

        if (isSubmitPressed ||
            isAutoReturn)
        {
            ReturnToTitle();
        }
    }

    //==================================================
    // 総スコアの数え上げ
    //==================================================

    void UpdateCountUp()
    {
        float progress =
            Mathf.Clamp01(m_timer / m_countUpTime);

        // 最後に向かってゆっくりになる
        float eased =
            1f - (1f - progress) * (1f - progress);

        SetTotalScoreText(
            Mathf.RoundToInt(m_totalScore * eased));

        if (progress >= 1f)
        {
            FinishCountUp();
        }
    }

    void FinishCountUp()
    {
        m_isCountFinished = true;

        SetTotalScoreText(m_totalScore);

        ShowRank();

        SetActiveSafe(m_pressToReturnObject, true);
    }

    void SetTotalScoreText(int score)
    {
        if (m_totalScoreText != null)
        {
            m_totalScoreText.text = score.ToString();
        }
    }

    //==================================================
    // ランク
    //==================================================

    void ShowRank()
    {
        if (m_rankText == null)
        {
            return;
        }

        RANK rank = GetRank(m_totalScore);

        if (rank == null)
        {
            return;
        }

        m_rankText.text = rank.m_name;
        m_rankText.color = rank.m_color;

        SetActiveSafe(m_rankText, true);
    }

    // 点数を満たすランクのうち、基準点が一番高いもの
    RANK GetRank(int score)
    {
        RANK best = null;
        RANK lowest = null;

        foreach (RANK rank in m_ranks)
        {
            if (rank == null)
            {
                continue;
            }

            if (lowest == null ||
                rank.m_minScore < lowest.m_minScore)
            {
                lowest = rank;
            }

            if (score >= rank.m_minScore &&
                (best == null || rank.m_minScore > best.m_minScore))
            {
                best = rank;
            }
        }

        // どのランクにも届かない（マイナス点など）場合は一番下のランク
        return best != null ? best : lowest;
    }

    //==================================================
    // プレイヤーごとのスコア
    //==================================================

    void ShowPlayerScores()
    {
        if (m_playerScoresText == null)
        {
            return;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int i = 0; i < m_playerCount; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(i + 1);
            builder.Append("P: ");
            builder.Append(GAME_SCORE.GetPlayerScore(i));
        }

        m_playerScoresText.text = builder.ToString();
    }

    //==================================================
    // タイトルへ
    //==================================================

    public void ReturnToTitle()
    {
        GAME_SCORE.ResetScores();

        GAME_SCENE_LOADER.Load(m_titleSceneName);
    }

    static void SetActiveSafe(Component target, bool isActive)
    {
        if (target != null)
        {
            target.gameObject.SetActive(isActive);
        }
    }

    static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_countUpTime = Mathf.Max(0f, m_countUpTime);
        m_inputDelay = Mathf.Max(0f, m_inputDelay);
        m_autoReturnTime = Mathf.Max(0f, m_autoReturnTime);
    }
}
