using UnityEngine;
using UnityEngine.UI;

//==================================================
// ミニゲーム「神速クッキング」の本体
//
// 厨房で食材カットのお手伝い。Joy-Con を包丁のように持って上下に振り、食材を切る
//
// ・振るたびに点数がたまり（まっすぐ 1 点 / ブレ 0.5 点）、そのぶん食材が短くなる
//   「切り終わるまでの点数」がたまったら、切り終わり = クリア
// ・まな板の上の食材の長さで、あとどれくらいで終わるかがわかる（COOKING_BOARD）
// ・振る速さに応じて集中線＆カメラズーム（SHAKE_EFFECTS）
// ・スコアは切り終わるまでの時間で決まる（早いほど高い）
//
// ・高橋さんのテンプレート（Template_MiniGame）をもとにしている
//==================================================
public class COOKING_GAME : MiniGame
{
    [Header("入力")]
    [SerializeField]
    private SHAKE_INPUT m_input;

    [Header("まな板")]
    [Tooltip("食材・包丁・切れ端の見た目")]
    [SerializeField]
    private COOKING_BOARD m_board;

    [Header("クリア条件")]
    [Tooltip("切り終わるまでに必要な点数（まっすぐ 1 回 = 1 点）")]
    [SerializeField]
    private float m_goalPoints = 50f;

    [Tooltip("制限時間（秒）。0 なら制限なし。時間切れはゲームオーバー")]
    [SerializeField]
    private float m_timeLimit = 0f;

    [Tooltip("切り終わってから終わるまでの時間（秒）")]
    [SerializeField]
    private float m_finishWaitTime = 1.2f;

    [Header("スコア（早く切り終わるほど高い）")]
    [Tooltip("この秒数以内に切り終わったら最高点")]
    [SerializeField]
    private float m_bestTime = 10f;

    [Tooltip("最高点")]
    [SerializeField]
    private int m_maxScore = 1000;

    [Tooltip("最低点（どれだけ遅くても切り終われば、もらえる）")]
    [SerializeField]
    private int m_minScore = 100;

    [Tooltip("最高点の時間より 1 秒遅れるごとに減る点数")]
    [SerializeField]
    private int m_penaltyPerSecond = 40;

    [Header("表示")]
    [Tooltip("あと何回で終わるかを出す文字（空欄でもよい）")]
    [SerializeField]
    private Text m_restText;

    private float m_points;
    private float m_time;
    private float m_finishTimer;
    private bool m_isCleared;
    private bool m_isFinished;

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        if (m_input != null)
        {
            m_input.Swung += OnSwung;
        }

        ResetGame();
        SetUiActive(false);
    }

    void OnDestroy()
    {
        if (m_input != null)
        {
            m_input.Swung -= OnSwung;
        }
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        if (m_isFinished)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        // 切り終わったあと、少し待ってから終わる
        if (m_isCleared)
        {
            m_finishTimer += deltaTime;

            if (m_finishTimer >= m_finishWaitTime)
            {
                Finish(true);
            }

            return;
        }

        m_time += deltaTime;

        // 振った分の点数をためる
        if (m_input != null)
        {
            m_points += m_input.ConsumePoints();
        }

        m_points = Mathf.Min(m_points, m_goalPoints);

        if (m_board != null)
        {
            m_board.SetRest(1f - m_points / m_goalPoints);
        }

        UpdateRestText();

        // 切り終わり
        if (m_points >= m_goalPoints)
        {
            m_isCleared = true;
            m_finishTimer = 0f;

            //ゲームクリア：スコアを渡す
            SetScore(CalculateScore());

            if (m_restText != null)
            {
                m_restText.text = "完成！";
            }

            return;
        }

        // 時間切れ（ゲーム失敗なのでスコアは渡さない）
        if (m_timeLimit > 0f &&
            m_time >= m_timeLimit)
        {
            Finish(false);
        }
    }

    public override void Activate()
    {
        ResetGame();

        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        SetUiActive(true);
        UpdateRestText();
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        // 食材は結果画面のあいだ切り終わったまま見せておき、次に遊ぶときに戻す
        SetUiActive(false);
    }

    // 1 回振った（包丁を下ろして、切れ端を出す）
    void OnSwung(float point)
    {
        if (m_isCleared || m_isFinished || m_board == null)
        {
            return;
        }

        m_board.Chop(point, m_input != null && m_input.IsStraight);
    }

    void ResetGame()
    {
        m_points = 0f;
        m_time = 0f;
        m_finishTimer = 0f;
        m_isCleared = false;
        m_isFinished = false;

        if (m_board != null)
        {
            m_board.ResetBoard();
        }
    }

    int CalculateScore()
    {
        float late = Mathf.Max(0f, m_time - m_bestTime);
        int score = m_maxScore - Mathf.RoundToInt(late * m_penaltyPerSecond);

        return Mathf.Clamp(score, m_minScore, m_maxScore);
    }

    void Finish(bool isClear)
    {
        if (m_isFinished)
        {
            return;
        }

        m_isFinished = true;

        //ミニゲーム終了フラグ（クリアなら true、失敗なら false）
        FinishMiniGame(isClear);
    }

    void UpdateRestText()
    {
        if (m_restText == null)
        {
            return;
        }

        int rest = Mathf.CeilToInt(m_goalPoints - m_points);
        m_restText.text = "あと " + rest + " カット";
    }

    void SetUiActive(bool isActive)
    {
        if (m_restText != null)
        {
            m_restText.gameObject.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_goalPoints = Mathf.Max(1f, m_goalPoints);
        m_timeLimit = Mathf.Max(0f, m_timeLimit);
        m_minScore = Mathf.Max(0, m_minScore);
        m_maxScore = Mathf.Max(m_minScore, m_maxScore);
    }
}
