using UnityEngine;
using UnityEngine.UI;

//==================================================
// ミニゲーム「落書きクリーンアップ」の本体
//
// スプレーで落書きされた壁を、大きなモップで振りまくってきれいにする
// Joy-Con を縦に持って、向きは問わず振りまくる
//
// ・振るたびに 1 点たまる（向き・まっすぐさは問わない）
// ・汚れは 5 段階。決まった回数振るごとに、1 段階ずつ汚れが落ちる（GRAFFITI_WALL）
//   全部落ちたらクリア
// ・段階ごとにカメラのズームが変わる
// ・振っている間は、画面の下からモップがニョキニョキ出てくる（GRAFFITI_MOP）
// ・速く振るほど集中線＆キラキラ（SHAKE_EFFECTS）
// ・スコアはきれいにするまでの時間で決まる（早いほど高い）
//
// ・高橋さんのテンプレート（Template_MiniGame）をもとにしている
//==================================================
public class GRAFFITI_GAME : MiniGame
{
    [Header("入力")]
    [SerializeField]
    private SHAKE_INPUT m_input;

    [Header("見た目")]
    [Tooltip("落書きされた壁")]
    [SerializeField]
    private GRAFFITI_WALL m_wall;

    [Tooltip("ミニゲームのカメラ（段階ごとにズームを変える）")]
    [SerializeField]
    private MINIGAME_CAMERA m_camera;

    [Header("クリア条件")]
    [Tooltip("汚れの段階の数")]
    [SerializeField]
    private int m_stageCount = 5;

    [Tooltip("1 段階汚れを落とすのに必要な回数")]
    [SerializeField]
    private int m_swingsPerStage = 10;

    [Tooltip("制限時間（秒）。0 なら制限なし。時間切れはゲームオーバー")]
    [SerializeField]
    private float m_timeLimit = 0f;

    [Tooltip("きれいになってから終わるまでの時間（秒）")]
    [SerializeField]
    private float m_finishWaitTime = 1.5f;

    [Header("カメラ")]
    [Tooltip("汚れの段階ごとの画角（小さいほどズーム）。左から、汚れが一番ひどいとき → きれいになったとき")]
    [SerializeField]
    private float[] m_fovByStage = { 60f, 55f, 50f, 45f, 40f, 60f };

    [Tooltip("1 段階落ちたときのカメラのゆれ（m）")]
    [SerializeField]
    private float m_stageImpulse = 0.08f;

    [Header("演出")]
    [Tooltip("汚れが 1 段階落ちたときに、まとめて出して強く光らせるキラキラ（空欄でもよい）")]
    [SerializeField]
    private SPARKLE_PARTICLE m_goalSparkle;

    [Tooltip("1 段階落ちたときに、まとめて出す星の数（全部きれいになったときは 2 倍）")]
    [SerializeField]
    private int m_goalSparkleCount = 25;

    [Header("スコア（早くきれいにするほど高い）")]
    [Tooltip("この秒数以内にきれいにしたら最高点")]
    [SerializeField]
    private float m_bestTime = 10f;

    [Tooltip("最高点")]
    [SerializeField]
    private int m_maxScore = 1000;

    [Tooltip("最低点（どれだけ遅くてもきれいにすれば、もらえる）")]
    [SerializeField]
    private int m_minScore = 100;

    [Tooltip("最高点の時間より 1 秒遅れるごとに減る点数")]
    [SerializeField]
    private int m_penaltyPerSecond = 40;

    [Header("表示")]
    [Tooltip("きれい度を出す文字（空欄でもよい）")]
    [SerializeField]
    private Text m_cleanText;

    private float m_points;
    private int m_stage;           // 落とした段階の数（0 ～ m_stageCount）
    private float m_time;
    private float m_finishTimer;
    private bool m_isCleared;
    private bool m_isFinished;

    int GoalPoints
    {
        get { return Mathf.Max(1, m_stageCount) * Mathf.Max(1, m_swingsPerStage); }
    }

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        ResetGame();
        SetUiActive(false);
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

        // きれいになったあと、少し待ってから終わる
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

        // 振った回数をためる
        if (m_input != null)
        {
            m_points += m_input.ConsumePoints();
        }

        m_points = Mathf.Min(m_points, GoalPoints);

        // 決まった回数ごとに 1 段階きれいにする
        int stage = Mathf.Min(m_stageCount, Mathf.FloorToInt(m_points / Mathf.Max(1, m_swingsPerStage)));

        if (stage != m_stage)
        {
            m_stage = stage;
            OnStageChanged();
        }

        UpdateCleanText();

        // 全部きれいになった
        if (m_stage >= m_stageCount)
        {
            m_isCleared = true;
            m_finishTimer = 0f;

            //ゲームクリア：スコアを渡す
            SetScore(CalculateScore());

            if (m_cleanText != null)
            {
                m_cleanText.text = "ピカピカ！";
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

        // カメラは SHAKE_EFFECTS で最初の状態に戻るので、そのあとで最初の段階のズームにする
        ApplyZoom();

        SetUiActive(true);
        UpdateCleanText();
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        // 壁は結果画面のあいだ、きれいなまま見せておき、次に遊ぶときに戻す
        SetUiActive(false);
    }

    void OnStageChanged()
    {
        if (m_wall != null)
        {
            m_wall.SetCleanRate((float)m_stage / Mathf.Max(1, m_stageCount));
        }

        ApplyZoom();

        if (m_camera != null)
        {
            m_camera.Impulse(m_stageImpulse);
        }

        if (m_goalSparkle != null)
        {
            bool isClean = m_stage >= m_stageCount;
            m_goalSparkle.Burst(isClean ? m_goalSparkleCount * 2 : m_goalSparkleCount);
            m_goalSparkle.Flash(1f);
        }
    }

    void ApplyZoom()
    {
        if (m_camera == null || m_fovByStage == null || m_fovByStage.Length == 0)
        {
            return;
        }

        m_camera.ZoomFov = m_fovByStage[Mathf.Clamp(m_stage, 0, m_fovByStage.Length - 1)];
    }

    void ResetGame()
    {
        m_points = 0f;
        m_stage = 0;
        m_time = 0f;
        m_finishTimer = 0f;
        m_isCleared = false;
        m_isFinished = false;

        if (m_wall != null)
        {
            m_wall.ResetWall();
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

    void UpdateCleanText()
    {
        if (m_cleanText == null)
        {
            return;
        }

        int percent = Mathf.FloorToInt(100f * m_points / GoalPoints);
        m_cleanText.text = "きれい度 " + percent + "%　（汚れ " + (m_stageCount - m_stage) + " / " + m_stageCount + "）";
    }

    void SetUiActive(bool isActive)
    {
        if (m_cleanText != null)
        {
            m_cleanText.gameObject.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_stageCount = Mathf.Max(1, m_stageCount);
        m_swingsPerStage = Mathf.Max(1, m_swingsPerStage);
        m_timeLimit = Mathf.Max(0f, m_timeLimit);
        m_minScore = Mathf.Max(0, m_minScore);
        m_maxScore = Mathf.Max(m_minScore, m_maxScore);
    }
}
