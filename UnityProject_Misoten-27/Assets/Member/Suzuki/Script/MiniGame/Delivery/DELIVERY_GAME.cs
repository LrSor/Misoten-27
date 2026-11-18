using UnityEngine;
using UnityEngine.UI;

//==================================================
// ミニゲーム「特急デリバリー」の本体
//
// バイクが故障した配達員の代わりに、Joy-Con を縦に持って上下に振り、全力ダッシュで配達する
//
// ・振るたびに点数がたまり（まっすぐ 1 点 / ブレ 0.5 点）、点数に応じてゴールに近づく
//   「ゴールまでの点数」がたまったらゴール = クリア
// ・振らなくても少しずつは進む
// ・ゴールの位置は、子の「Goal」を動かせば変えられる（Scene ビューに黄色い線が出る）
// ・スコアはゴールまでの時間で決まる（早いほど高い）
// ・ゴールしたら、ミニゲームが終わってクエストを受けた場所に戻る（MiniGameSystem がしてくれる）
//
// ・高橋さんのテンプレート（Template_MiniGame）をもとにしている
//==================================================
public class DELIVERY_GAME : MiniGame
{
    [Header("入力")]
    [SerializeField]
    private SHAKE_INPUT m_input;

    [Header("コース")]
    [Tooltip("走る配達員")]
    [SerializeField]
    private Transform m_runner;

    [Tooltip("配達員の見た目（速さで変わる）")]
    [SerializeField]
    private DELIVERY_RUNNER m_runnerVisual;

    [Tooltip("スタート地点")]
    [SerializeField]
    private Transform m_start;

    [Tooltip("ゴール地点（動かすと距離が変わる）")]
    [SerializeField]
    private Transform m_goal;

    [Header("クリア条件")]
    [Tooltip("ゴールまでに必要な点数（まっすぐ 1 回 = 1 点）")]
    [SerializeField]
    private float m_goalPoints = 50f;

    [Tooltip("振らなくても 1 秒にたまる点数（0 なら振らないと進まない）")]
    [SerializeField]
    private float m_autoPointsPerSecond = 0.5f;

    [Tooltip("制限時間（秒）。0 なら制限なし。時間切れはゲームオーバー")]
    [SerializeField]
    private float m_timeLimit = 0f;

    [Tooltip("ゴールしてから終わるまでの時間（秒）")]
    [SerializeField]
    private float m_goalWaitTime = 1.2f;

    [Header("動き")]
    [Tooltip("配達員が目標の位置に追いつく速さ（大きいほどきびきび）")]
    [SerializeField]
    private float m_moveSmoothness = 5f;

    [Header("スコア（早くゴールするほど高い）")]
    [Tooltip("この秒数以内にゴールしたら最高点")]
    [SerializeField]
    private float m_bestTime = 10f;

    [Tooltip("最高点")]
    [SerializeField]
    private int m_maxScore = 1000;

    [Tooltip("最低点（どれだけ遅くてもゴールすればもらえる）")]
    [SerializeField]
    private int m_minScore = 100;

    [Tooltip("最高点の時間より 1 秒遅れるごとに減る点数")]
    [SerializeField]
    private int m_penaltyPerSecond = 40;

    [Header("カメラ")]
    [Tooltip("ミニゲームのカメラ。結果画面が終わってカメラが切れたら、配達員をスタートに戻す")]
    [SerializeField]
    private MINIGAME_CAMERA m_camera;

    [Header("表示")]
    [Tooltip("ゴールまでの距離を出す文字（空欄でもよい）")]
    [SerializeField]
    private Text m_distanceText;

    private float m_points;
    private float m_progress;          // 見た目の進み具合（0～1）
    private float m_time;
    private float m_goalTimer;
    private bool m_isGoal;
    private bool m_isFinished;

    public float Progress { get { return m_progress; } }
    public float CourseLength
    {
        get
        {
            return (m_start != null && m_goal != null)
                ? Vector3.Distance(m_start.position, m_goal.position)
                : 0f;
        }
    }

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        ResetCourse();
        SetUiActive(false);

        if (m_camera != null)
        {
            m_camera.Disabled += ResetCourse;
        }
    }

    void OnDestroy()
    {
        if (m_camera != null)
        {
            m_camera.Disabled -= ResetCourse;
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

        // ゴールしたあと、少し待ってから終わる
        if (m_isGoal)
        {
            MoveRunner(deltaTime);

            m_goalTimer += deltaTime;

            if (m_goalTimer >= m_goalWaitTime)
            {
                Finish(true);
            }

            return;
        }

        m_time += deltaTime;

        // 点数をためる（振った分 + 振らなくても進む分）
        if (m_input != null)
        {
            m_points += m_input.ConsumePoints();
        }

        m_points += m_autoPointsPerSecond * deltaTime;
        m_points = Mathf.Min(m_points, m_goalPoints);

        MoveRunner(deltaTime);

        // 速さに合わせて配達員の見た目を変える
        if (m_runnerVisual != null && m_input != null)
        {
            m_runnerVisual.SetLevel(m_input.Level);
        }

        UpdateDistanceText();

        // ゴール
        if (m_points >= m_goalPoints &&
            m_progress >= 0.995f)
        {
            m_isGoal = true;
            m_goalTimer = 0f;

            SetScore(CalculateScore());

            if (m_runnerVisual != null)
            {
                m_runnerVisual.SetGoal();
            }

            if (m_distanceText != null)
            {
                m_distanceText.text = "ゴール！";
            }

            return;
        }

        // 時間切れ
        if (m_timeLimit > 0f &&
            m_time >= m_timeLimit)
        {
            SetScore(0);
            Finish(false);
        }
    }

    public override void Activate()
    {
        // 先にスタートに戻す（このあとカメラが配達員の位置に合わせるため）
        ResetCourse();

        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        SetUiActive(true);
        UpdateDistanceText();
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        // 配達員は結果画面のあいだゴールに立たせておく
        // （カメラが切れたときに、スタートに戻す）
        SetUiActive(false);
    }

    void ResetCourse()
    {
        m_points = 0f;
        m_progress = 0f;
        m_time = 0f;
        m_goalTimer = 0f;
        m_isGoal = false;
        m_isFinished = false;

        if (m_runner != null && m_start != null && m_goal != null)
        {
            m_runner.position = m_start.position;

            Vector3 direction = m_goal.position - m_start.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                m_runner.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        if (m_runnerVisual != null)
        {
            m_runnerVisual.ResetVisual();
        }
    }

    // 点数に合わせて配達員を進める（なめらかに追いつく）
    void MoveRunner(float deltaTime)
    {
        if (m_runner == null || m_start == null || m_goal == null)
        {
            m_progress = m_points / Mathf.Max(0.01f, m_goalPoints);
            return;
        }

        float target = m_points / Mathf.Max(0.01f, m_goalPoints);

        m_progress = Mathf.Lerp(m_progress, target, 1f - Mathf.Exp(-deltaTime * m_moveSmoothness));

        if (target - m_progress < 0.002f)
        {
            m_progress = target;
        }

        m_runner.position = Vector3.Lerp(m_start.position, m_goal.position, m_progress);
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
        FinishMiniGame(isClear);
    }

    void UpdateDistanceText()
    {
        if (m_distanceText == null)
        {
            return;
        }

        float rest = CourseLength * (1f - m_progress);
        m_distanceText.text = "ゴールまで " + Mathf.CeilToInt(rest) + " m";
    }

    void SetUiActive(bool isActive)
    {
        if (m_distanceText != null)
        {
            m_distanceText.gameObject.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_goalPoints = Mathf.Max(1f, m_goalPoints);
        m_autoPointsPerSecond = Mathf.Max(0f, m_autoPointsPerSecond);
        m_timeLimit = Mathf.Max(0f, m_timeLimit);
        m_minScore = Mathf.Max(0, m_minScore);
        m_maxScore = Mathf.Max(m_minScore, m_maxScore);
    }

    // Scene ビューにコースを表示する
    void OnDrawGizmos()
    {
        if (m_start == null || m_goal == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(m_start.position, m_goal.position);
        Gizmos.DrawWireSphere(m_start.position, 0.5f);
        Gizmos.DrawWireSphere(m_goal.position, 1f);
    }
}
