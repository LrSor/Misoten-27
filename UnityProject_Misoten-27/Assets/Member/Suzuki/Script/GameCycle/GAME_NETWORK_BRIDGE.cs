using Sawada.Cordex;
using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// 澤田さんの通信（NetworkTestSession）と、ゲームの進行（GAME_MANAGER）をつなぐ橋渡し
//
// ■ タイムアップしたとき
//   ・澤田さんの順位集計（RequestRanking）を呼ぶ
//   ・集計が終わるまでリザルトへ移るのを待つ（GAME_MANAGER.ResultHold）
//   ・集計結果（P1 ～ P4 のスコア）を GAME_SCORE に書き込む
//     → リザルトシーン（GAME_RESULT）はそれを読んで順位を出す
//
// ■ 他のプレイヤーが先にタイムアップしたとき
//   ・Host から順位の結果が届いたら、自分もタイムアップにする
//     （みんなほぼ同時にリザルトへ進む）
//
// ・集計が届かないとき（通信が切れた等）は、自分のスコアだけ入れてリザルトへ進む
// ・澤田さんのスクリプトは変更していない
//==================================================
public class GAME_NETWORK_BRIDGE : MonoBehaviour
{
    [Tooltip("空欄ならシーンから自動で探す")]
    [SerializeField]
    private NetworkTestSession m_session;

    [Tooltip("順位の集計を待つ最大の時間（秒）")]
    [SerializeField]
    private float m_rankingTimeout = 8f;

    [Tooltip("Host から順位の結果が届いたら、プレイ中でもタイムアップにする")]
    [SerializeField]
    private bool m_endWhenRankingArrives = true;

    // 集計を待っている間 true
    private bool m_isWaiting;
    private float m_waitTimer;
    private float m_retryTimer;

    // 集計結果を受け取ったか
    private bool m_hasResult;

    // GAME_MANAGER.ResultHold に渡すもの（外すときに同じものか比べるので取っておく）
    private System.Func<bool> m_holdFunc;

    void Awake()
    {
        if (m_session == null)
        {
            m_session = FindFirstObjectByType<NetworkTestSession>();
        }
    }

    void OnEnable()
    {
        GAME_MANAGER.TimeUp += OnTimeUp;
        if (m_holdFunc == null)
        {
            m_holdFunc = IsHolding;
        }

        GAME_MANAGER.ResultHold = m_holdFunc;

        if (m_session != null)
        {
            m_session.RankingCompleted += OnRankingCompleted;
        }
    }

    void OnDisable()
    {
        GAME_MANAGER.TimeUp -= OnTimeUp;

        if (GAME_MANAGER.ResultHold == m_holdFunc)
        {
            GAME_MANAGER.ResultHold = null;
        }

        if (m_session != null)
        {
            m_session.RankingCompleted -= OnRankingCompleted;
        }
    }

    bool IsHolding()
    {
        return m_isWaiting;
    }

    //==================================================
    // タイムアップ → 順位集計を頼む
    //==================================================

    void OnTimeUp()
    {
        if (m_hasResult)
        {
            return;
        }

        // 集計が届かなかったとき用に、自分のスコアだけ先に入れておく
        WriteLocalScore();

        if (m_session == null)
        {
            return;
        }

        m_isWaiting = true;
        m_waitTimer = 0f;
        m_retryTimer = 0f;

        m_session.RequestRanking();
    }

    void Update()
    {
        if (!m_isWaiting)
        {
            return;
        }

        m_waitTimer += Time.unscaledDeltaTime;
        m_retryTimer += Time.unscaledDeltaTime;

        // 届かなければ 1 秒ごとに頼み直す（Host が受け付けられなかったとき用）
        if (m_retryTimer >= 1f &&
            m_session != null)
        {
            m_retryTimer = 0f;
            m_session.RequestRanking();
        }

        if (m_waitTimer >= m_rankingTimeout)
        {
            Debug.LogWarning(
                "GAME_NETWORK_BRIDGE: 順位の集計が届かなかったので、自分のスコアだけでリザルトへ進みます");

            m_isWaiting = false;
        }
    }

    //==================================================
    // 順位の結果が届いた
    //==================================================

    void OnRankingCompleted()
    {
        if (m_session == null ||
            m_session.LastRanking == null ||
            m_session.LastRanking.Count == 0)
        {
            return;
        }

        GAME_SCORE.ResetScores();

        foreach (ScoreRankingCoordinator.Entry entry in m_session.LastRanking)
        {
            // id は 1 ～ 4（P1 ～ P4）。GAME_SCORE は 0 ～ 3
            GAME_SCORE.SetPlayerScore(
                entry.id - 1,
                entry.submitted ? entry.score : 0);
        }

        m_hasResult = true;
        m_isWaiting = false;

        // 自分はまだプレイ中（他の人が先に終わった）ならタイムアップにする
        if (m_endWhenRankingArrives &&
            GAME_MANAGER.Instance != null &&
            GAME_MANAGER.Instance.IsPlaying)
        {
            GAME_MANAGER.Instance.ForceTimeUp();
        }
    }

    //==================================================
    // 自分のスコア（集計が届かなかったとき用）
    //==================================================

    void WriteLocalScore()
    {
        PlayerScore localScore = FindLocalPlayerScore(out int playerIndex);

        if (localScore == null)
        {
            return;
        }

        GAME_SCORE.SetPlayerScore(playerIndex, localScore.GetTotalScore());
    }

    // 自分のプレイヤー（PlayerInput が有効なもの）の PlayerScore を探す
    // 通信のプレイヤーは「Network Player 番号」という名前で作られるので、そこから番号を取る
    static PlayerScore FindLocalPlayerScore(out int playerIndex)
    {
        playerIndex = 0;

        PlayerScore[] scores =
            FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);

        foreach (PlayerScore score in scores)
        {
            PlayerInput input =
                score.GetComponentInParent<PlayerInput>();

            if (input == null ||
                !input.enabled)
            {
                continue;
            }

            playerIndex = GetPlayerIndexFromName(score.transform);
            return score;
        }

        return scores.Length > 0 ? scores[0] : null;
    }

    static int GetPlayerIndexFromName(Transform target)
    {
        const string PREFIX = "Network Player ";

        for (Transform t = target; t != null; t = t.parent)
        {
            if (t.name.StartsWith(PREFIX) &&
                int.TryParse(t.name.Substring(PREFIX.Length), out int id))
            {
                return Mathf.Clamp(id - 1, 0, GAME_SCORE.MAX_PLAYERS - 1);
            }
        }

        return 0;
    }
}
