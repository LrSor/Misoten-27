using System.Collections.Generic;
using TMPro;
using UnityEngine;

//==================================================
// リザルト画面
//
// ・プレイヤーごとのスコアを高い順に並べ、順位を付けて表示
//   （同点は同じ順位。例：1st, 1st, 3rd, 4th）
// ・スコアは 0 から数え上げて表示
// ・数え上げ中に決定を押すと、すぐに最後まで表示
// ・表示し終わった後に決定を押すとタイトルへ（自動で戻る設定も可）
//==================================================
public class GAME_RESULT : MonoBehaviour
{
    // 1人分の結果
    public struct RESULT_ENTRY
    {
        public int m_playerIndex;   // 0 = 1P
        public int m_score;         // 最終スコア
        public int m_place;         // 順位（1 = 1位）
    }

    [Header("シーン")]
    [SerializeField]
    private string m_titleSceneName = "Title";

    [Header("表示するプレイヤーの人数")]
    [Range(1, GAME_SCORE.MAX_PLAYERS)]
    [SerializeField]
    private int m_playerCount = GAME_SCORE.MAX_PLAYERS;

    [Header("演出")]
    [Tooltip("スコアを数え上げる時間（秒）。0 ですぐ表示")]
    [SerializeField]
    private float m_countUpTime = 1.5f;

    [Tooltip("シーン開始から入力を受け付けるまでの時間（ゲーム中の連打で飛ばされないように）")]
    [SerializeField]
    private float m_inputDelay = 1f;

    [Tooltip("表示し終わってから自動でタイトルに戻るまでの時間（秒）。0 で自動では戻らない")]
    [SerializeField]
    private float m_autoReturnTime = 0f;

    [Header("UI（任意。空欄でも動く）")]
    [Tooltip("順位表（「1st  1P  1200」のように改行で並べる）")]
    [SerializeField]
    private TMP_Text m_rankingText;

    [Tooltip("「Press Start でタイトルへ」の表示。数え上げが終わってから出る")]
    [SerializeField]
    private GameObject m_pressToReturnObject;

    // 順位順に並んだ結果
    private readonly List<RESULT_ENTRY> m_entries =
        new List<RESULT_ENTRY>();

    private float m_timer;
    private float m_finishedTimer;
    private float m_countProgress;
    private bool m_isCountFinished;

    // 順位順に並んだ結果（1位が先頭）
    public IReadOnlyList<RESULT_ENTRY> Entries
    {
        get { return m_entries; }
    }

    // 数え上げが終わったか
    public bool IsCountFinished
    {
        get { return m_isCountFinished; }
    }

    // 数え上げ中の表示スコア
    public int GetDisplayedScore(RESULT_ENTRY entry)
    {
        return Mathf.RoundToInt(entry.m_score * m_countProgress);
    }

    void Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildRanking();

        SetActiveSafe(m_pressToReturnObject, false);

        if (m_countUpTime <= 0f)
        {
            FinishCountUp();
        }
        else
        {
            UpdateRankingText();
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
    // 順位を決める
    //==================================================

    void BuildRanking()
    {
        m_entries.Clear();

        for (int i = 0; i < m_playerCount; i++)
        {
            RESULT_ENTRY entry = new RESULT_ENTRY();

            entry.m_playerIndex = i;
            entry.m_score = GAME_SCORE.GetPlayerScore(i);

            m_entries.Add(entry);
        }

        // スコアの高い順。同点ならプレイヤー番号の小さい順
        m_entries.Sort((a, b) =>
        {
            if (a.m_score != b.m_score)
            {
                return b.m_score.CompareTo(a.m_score);
            }

            return a.m_playerIndex.CompareTo(b.m_playerIndex);
        });

        // 順位を付ける（同点は同じ順位）
        for (int i = 0; i < m_entries.Count; i++)
        {
            RESULT_ENTRY entry = m_entries[i];

            bool isSameAsPrevious =
                i > 0 &&
                entry.m_score == m_entries[i - 1].m_score;

            entry.m_place =
                isSameAsPrevious
                    ? m_entries[i - 1].m_place
                    : i + 1;

            m_entries[i] = entry;
        }
    }

    // 1 → 1st, 2 → 2nd, 3 → 3rd, 4 → 4th
    public static string GetPlaceText(int place)
    {
        switch (place)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return place + "th";
        }
    }

    //==================================================
    // スコアの数え上げ
    //==================================================

    void UpdateCountUp()
    {
        float progress =
            Mathf.Clamp01(m_timer / m_countUpTime);

        // 最後に向かってゆっくりになる
        m_countProgress =
            1f - (1f - progress) * (1f - progress);

        UpdateRankingText();

        if (progress >= 1f)
        {
            FinishCountUp();
        }
    }

    void FinishCountUp()
    {
        m_isCountFinished = true;
        m_countProgress = 1f;

        UpdateRankingText();

        SetActiveSafe(m_pressToReturnObject, true);
    }

    void UpdateRankingText()
    {
        if (m_rankingText == null)
        {
            return;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int i = 0; i < m_entries.Count; i++)
        {
            RESULT_ENTRY entry = m_entries[i];

            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(GetPlaceText(entry.m_place));
            builder.Append("   ");
            builder.Append(entry.m_playerIndex + 1);
            builder.Append("P   ");
            builder.Append(GetDisplayedScore(entry));
        }

        m_rankingText.text = builder.ToString();
    }

    //==================================================
    // タイトルへ
    //==================================================

    public void ReturnToTitle()
    {
        GAME_SCORE.ResetScores();

        GAME_SCENE_LOADER.Load(m_titleSceneName);
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
