using UnityEngine;
using UnityEngine.UI;

//==================================================
// ミニゲーム「漂流サルベージ」の本体
//
// 流れてきた落とし物が引っかかったら、Joy-Con を横に持って垂直にグルグル回し、リールを巻いて釣り上げる
//
//   1. 投げる  ：防波堤から海に、うきを投げる（自動）
//   2. 待つ    ：うきがプカプカ。制限時間が減っていく
//   3. チャンス：Joy-Con が振動＆少しズーム。決まった時間内に回し始めれば引っかかる
//                （逃すとズームが戻って、また待つ）。チャンスは全部で 3 回
//   4. 巻く    ：制限時間は止まる。ズーム＆カメラのゆれ。アクション指示を出す
//                決まった回数だけ回したら釣り上げ（クリア）
//                回すのが弱い状態が続くと、竿がだんだん引っぱられ、決まった秒数続くと糸が切れる
//   5. 糸が切れたら：制限時間とチャンスが残っていれば、また投げるところから
//
// ・時間切れ、またはチャンスを使い切ったらゲームオーバー（スコアは渡さない）
// ・スコア：何回目のチャンスで釣れたか ＋ 巻く速さ（1 回目のチャンスで速く巻くのが最高）
//
// ・高橋さんのテンプレート（Template_MiniGame）をもとにしている
//==================================================
public class SALVAGE_GAME : MiniGame
{
    enum STATE
    {
        CAST,       // 投げている
        WAIT,       // 待っている
        CHANCE,     // チャンス（引っかかった）
        REEL,       // 巻いている
        LOST,       // 糸が切れた
        CATCH,      // 釣り上げた
        FAILED,     // ゲームオーバー
        DONE,
    }

    [Header("入力と見た目")]
    [SerializeField]
    private ROTATE_INPUT m_input;

    [Tooltip("竿・糸・うき・落とし物")]
    [SerializeField]
    private SALVAGE_TACKLE m_tackle;

    [SerializeField]
    private MINIGAME_CAMERA m_camera;

    [Header("時間とチャンス")]
    [Tooltip("制限時間（秒）。巻いている間は止まる")]
    [SerializeField]
    private float m_timeLimit = 30f;

    [Tooltip("引っかかるチャンスの回数")]
    [SerializeField]
    private int m_chanceCount = 3;

    [Tooltip("うきが着いてから、チャンスが来るまでの時間（秒）（最小～最大でランダム）")]
    [SerializeField]
    private Vector2 m_waitRange = new Vector2(2.5f, 5f);

    [Tooltip("チャンスの長さ（秒）。この間に回し始めないと逃す")]
    [SerializeField]
    private float m_chanceTime = 1.5f;

    [Tooltip("投げてから、うきが着くまでの時間（秒）")]
    [SerializeField]
    private float m_castTime = 1f;

    [Header("巻く")]
    [Tooltip("チャンスのとき、これだけ回したら引っかかる（回転）")]
    [SerializeField]
    private float m_hookTurns = 0.25f;

    [Tooltip("釣り上げるまでに回す回数")]
    [SerializeField]
    private float m_reelTurns = 15f;

    [Tooltip("1 秒にこの回転数より遅いと「弱い」（竿が引っぱられる）")]
    [SerializeField]
    private float m_weakRate = 0.8f;

    [Tooltip("弱い状態がこの秒数続くと糸が切れる")]
    [SerializeField]
    private float m_breakTime = 3f;

    [Tooltip("しっかり回すと、引っぱられた分が戻る速さ（倍）")]
    [SerializeField]
    private float m_recoverSpeed = 1.5f;

    [Tooltip("糸が切れてから、また投げるまでの時間（秒）")]
    [SerializeField]
    private float m_lostWaitTime = 1.5f;

    [Tooltip("釣り上げてから終わるまでの時間（秒）")]
    [SerializeField]
    private float m_catchWaitTime = 1.8f;

    [Header("カメラ")]
    [SerializeField]
    private float m_normalFov = 55f;

    [Tooltip("チャンスのときの画角（少しズーム）")]
    [SerializeField]
    private float m_chanceFov = 48f;

    [Tooltip("巻いているときの画角（ズーム）")]
    [SerializeField]
    private float m_reelFov = 38f;

    [Tooltip("巻いているときのゆれ（遅いとき～速いとき）（m）")]
    [SerializeField]
    private Vector2 m_reelShake = new Vector2(0.01f, 0.05f);

    [Header("振動")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_chanceRumble = 0.8f;

    [Header("スコア")]
    [Tooltip("何回目のチャンスで釣れたかの点数（1 回目、2 回目、3 回目…）")]
    [SerializeField]
    private int[] m_chanceScores = { 1000, 700, 450 };

    [Tooltip("この秒数以内に巻き終わったら減点なし")]
    [SerializeField]
    private float m_bestReelTime = 6f;

    [Tooltip("それより 1 秒遅れるごとに減る点数")]
    [SerializeField]
    private int m_penaltyPerSecond = 40;

    [Tooltip("最低点（釣り上げれば、もらえる）")]
    [SerializeField]
    private int m_minScore = 100;

    [Header("表示（空欄でもよい）")]
    [Tooltip("残り時間とチャンスの数")]
    [SerializeField]
    private Text m_infoText;

    [Tooltip("アクション指示（リールを回せ！）")]
    [SerializeField]
    private Text m_promptText;

    [Tooltip("真ん中に大きく出す文字（ヒット！など）")]
    [SerializeField]
    private Text m_centerText;

    [Tooltip("あと何回転か")]
    [SerializeField]
    private Text m_progressText;

    [Tooltip("糸の引っぱられ具合のゲージ（全体）")]
    [SerializeField]
    private GameObject m_tensionGauge;

    [Tooltip("ゲージの中身（横の大きさで量を表す。左端を基準にしておく）")]
    [SerializeField]
    private RectTransform m_tensionFill;

    [Tooltip("アクション指示の点滅の速さ")]
    [SerializeField]
    private float m_blinkSpeed = 4f;

    private STATE m_state;
    private float m_timeLeft;
    private int m_chancesLeft;
    private int m_chanceIndex;
    private float m_stateTimer;
    private float m_reelProgress;
    private float m_reelTime;
    private float m_weakTime;
    private float m_hookTurnsNow;
    private float m_centerTextTimer;

    public override float NetworkRemainingSeconds { get { return Mathf.Max(0f, m_timeLeft); } }

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        SetUiActive(false);
    }

    public override void Activate()
    {
        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        m_timeLeft = m_timeLimit;
        m_chancesLeft = Mathf.Max(1, m_chanceCount);
        m_chanceIndex = 0;
        m_stars = null;

        if (m_camera != null)
        {
            m_camera.ResetView();
            m_camera.ZoomFov = m_normalFov;
        }

        SetUiActive(true);
        StartCast();
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        SetUiActive(false);

        if (m_camera != null)
        {
            m_camera.Shake = 0f;
            m_camera.ZoomFov = 0f;
        }
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        float deltaTime = Time.deltaTime;

        switch (m_state)
        {
            case STATE.CAST:
                CountDown(deltaTime);
                m_stateTimer -= deltaTime;
                DiscardTurns();

                if (m_stateTimer <= 0f)
                {
                    StartWait();
                }
                break;

            case STATE.WAIT:
                CountDown(deltaTime);
                m_stateTimer -= deltaTime;
                DiscardTurns();

                if (m_stateTimer <= 0f)
                {
                    StartChance();
                }
                break;

            case STATE.CHANCE:
                CountDown(deltaTime);
                UpdateChance(deltaTime);
                break;

            case STATE.REEL:
                UpdateReel(deltaTime);
                break;

            case STATE.LOST:
                m_stateTimer -= deltaTime;
                DiscardTurns();

                if (m_stateTimer <= 0f)
                {
                    if (m_timeLeft > 0f && m_chancesLeft > 0)
                    {
                        StartCast();
                    }
                    else
                    {
                        Fail();
                    }
                }
                break;

            case STATE.CATCH:
                m_stateTimer -= deltaTime;

                if (m_stateTimer <= 0f)
                {
                    m_state = STATE.DONE;

                    //ミニゲーム終了フラグ（クリア）
                    FinishMiniGame(true);
                }
                break;
        }

        UpdateUi(deltaTime);
    }

    //==================================================
    // 各段階
    //==================================================

    void StartCast()
    {
        m_state = STATE.CAST;
        m_stateTimer = m_castTime;

        if (m_tackle != null)
        {
            m_tackle.Cast(m_castTime);
        }

        SetZoom(m_normalFov, 0f);
        SetPrompt(false);
    }

    void StartWait()
    {
        m_state = STATE.WAIT;
        m_stateTimer = Random.Range(m_waitRange.x, Mathf.Max(m_waitRange.x, m_waitRange.y));

        if (m_tackle != null)
        {
            m_tackle.Float();
        }

        SetZoom(m_normalFov, 0f);
    }

    void StartChance()
    {
        if (m_chancesLeft <= 0)
        {
            Fail();
            return;
        }

        m_state = STATE.CHANCE;
        m_stateTimer = m_chanceTime;
        m_chanceIndex = m_chanceCount - m_chancesLeft;
        m_chancesLeft--;
        m_hookTurnsNow = 0f;

        DiscardTurns();

        if (m_tackle != null)
        {
            m_tackle.Bite();
        }

        if (m_input != null)
        {
            m_input.Rumble(m_chanceRumble, m_chanceTime);
        }

        SetZoom(m_chanceFov, 0f);
        ShowCenterText("！", m_chanceTime);
    }

    void UpdateChance(float deltaTime)
    {
        if (m_input != null)
        {
            m_hookTurnsNow += m_input.ConsumeTurns();
        }

        // 回し始めたら引っかかる
        if (m_hookTurnsNow >= m_hookTurns)
        {
            StartReel();
            return;
        }

        m_stateTimer -= deltaTime;

        // 逃した
        if (m_stateTimer <= 0f)
        {
            if (m_chancesLeft > 0)
            {
                ShowCenterText("逃げられた…", 1f);
                StartWait();
            }
            else
            {
                Fail();
            }
        }
    }

    void StartReel()
    {
        m_state = STATE.REEL;
        m_reelProgress = 0f;
        m_reelTime = 0f;
        m_weakTime = 0f;

        if (m_tackle != null)
        {
            m_tackle.Hook();
        }

        if (m_camera != null)
        {
            m_camera.Impulse(0.08f);
        }

        SetZoom(m_reelFov, m_reelShake.x);
        ShowCenterText("ヒット！", 1f);
        SetPrompt(true);
    }

    void UpdateReel(float deltaTime)
    {
        m_reelTime += deltaTime;

        if (m_input != null)
        {
            m_reelProgress += m_input.ConsumeTurns() / Mathf.Max(0.1f, m_reelTurns);
        }

        // 弱いと引っぱられる。しっかり回すと戻る
        bool isWeak = m_input == null || m_input.TurnRate < m_weakRate;

        if (isWeak)
        {
            m_weakTime += deltaTime;
        }
        else
        {
            m_weakTime = Mathf.Max(0f, m_weakTime - deltaTime * m_recoverSpeed);
        }

        float tension = Mathf.Clamp01(m_weakTime / Mathf.Max(0.1f, m_breakTime));

        if (m_tackle != null)
        {
            m_tackle.SetReel(Mathf.Clamp01(m_reelProgress), tension);
        }

        // 速く回すほどゆれる
        if (m_camera != null)
        {
            float speed = m_input != null ? m_input.Speed01 : 0f;
            m_camera.Shake = Mathf.Lerp(m_reelShake.x, m_reelShake.y, speed);
        }

        // 釣り上げた
        if (m_reelProgress >= 1f)
        {
            Catch();
            return;
        }

        // 糸が切れた
        if (tension >= 1f)
        {
            Lose();
        }
    }

    void Catch()
    {
        m_state = STATE.CATCH;
        m_stateTimer = m_catchWaitTime;

        if (m_tackle != null)
        {
            m_tackle.Catch();
        }

        //ゲームクリア：スコアを渡す
        SetScore(CalculateScore());

        // 星（通信のときの評価）：1 回目のチャンスで星 3、2 回目で 2、3 回目以降は 1
        m_stars = Mathf.Clamp(3 - m_chanceIndex, 1, 3);

        SetZoom(m_normalFov, 0f);

        if (m_camera != null)
        {
            m_camera.Impulse(0.05f);
        }

        SetPrompt(false);
        ShowCenterText("釣り上げた！", m_catchWaitTime);
    }

    void Lose()
    {
        m_state = STATE.LOST;
        m_stateTimer = m_lostWaitTime;

        if (m_tackle != null)
        {
            m_tackle.Snap();
        }

        if (m_camera != null)
        {
            m_camera.Impulse(0.1f);
        }

        SetZoom(m_normalFov, 0f);
        SetPrompt(false);
        ShowCenterText("糸が切れた…", m_lostWaitTime);
    }

    void Fail()
    {
        if (m_state == STATE.FAILED || m_state == STATE.DONE)
        {
            return;
        }

        m_state = STATE.FAILED;

        SetZoom(m_normalFov, 0f);
        SetPrompt(false);

        //ミニゲーム終了フラグ（失敗なのでスコアは渡さない）
        FinishMiniGame(false);
    }

    // 制限時間を減らす（巻いている間は呼ばない）
    void CountDown(float deltaTime)
    {
        m_timeLeft -= deltaTime;

        if (m_timeLeft <= 0f)
        {
            m_timeLeft = 0f;
            Fail();
        }
    }

    // 回した分を捨てる（待っている間に回しても意味がないように）
    void DiscardTurns()
    {
        if (m_input != null)
        {
            m_input.ConsumeTurns();
        }
    }

    int CalculateScore()
    {
        int baseScore = (m_chanceScores != null && m_chanceScores.Length > 0)
            ? m_chanceScores[Mathf.Clamp(m_chanceIndex, 0, m_chanceScores.Length - 1)]
            : 1000;

        float late = Mathf.Max(0f, m_reelTime - m_bestReelTime);
        int score = baseScore - Mathf.RoundToInt(late * m_penaltyPerSecond);

        return Mathf.Max(m_minScore, score);
    }

    //==================================================
    // カメラ・表示
    //==================================================

    void SetZoom(float fov, float shake)
    {
        if (m_camera == null)
        {
            return;
        }

        m_camera.ZoomFov = fov;
        m_camera.Shake = shake;
    }

    void SetPrompt(bool isActive)
    {
        if (m_promptText != null)
        {
            m_promptText.gameObject.SetActive(isActive);
        }

        if (m_tensionGauge != null)
        {
            m_tensionGauge.SetActive(isActive);
        }

        if (m_progressText != null)
        {
            m_progressText.gameObject.SetActive(isActive);
        }
    }

    void ShowCenterText(string text, float seconds)
    {
        if (m_centerText == null)
        {
            return;
        }

        m_centerText.text = text;
        m_centerText.gameObject.SetActive(true);
        m_centerTextTimer = seconds;
    }

    void UpdateUi(float deltaTime)
    {
        if (m_infoText != null)
        {
            m_infoText.text =
                "のこり " + Mathf.CeilToInt(m_timeLeft) + " 秒" +
                (m_state == STATE.REEL ? "（ストップ）" : "") +
                "　チャンス " + m_chancesLeft + " / " + m_chanceCount;
        }

        if (m_centerText != null && m_centerText.gameObject.activeSelf)
        {
            m_centerTextTimer -= deltaTime;

            if (m_centerTextTimer <= 0f)
            {
                m_centerText.gameObject.SetActive(false);
            }
        }

        if (m_state != STATE.REEL)
        {
            return;
        }

        if (m_progressText != null)
        {
            float rest = Mathf.Max(0f, (1f - m_reelProgress) * m_reelTurns);
            m_progressText.text = "あと " + Mathf.CeilToInt(rest) + " 回転";
        }

        if (m_tensionFill != null)
        {
            float tension = Mathf.Clamp01(m_weakTime / Mathf.Max(0.1f, m_breakTime));
            m_tensionFill.localScale = new Vector3(tension, 1f, 1f);
        }

        // 回していない・遅いときは点滅
        if (m_promptText != null)
        {
            bool isSlow = m_input == null || m_input.TurnRate < m_weakRate;
            bool show = !isSlow || Mathf.Repeat(Time.time * m_blinkSpeed, 1f) < 0.6f;

            Color color = m_promptText.color;
            color.a = show ? 1f : 0.15f;
            m_promptText.color = color;
        }
    }

    void SetUiActive(bool isActive)
    {
        if (m_infoText != null)
        {
            m_infoText.gameObject.SetActive(isActive);
        }

        if (m_centerText != null)
        {
            m_centerText.gameObject.SetActive(false);
        }

        if (!isActive)
        {
            SetPrompt(false);
        }
    }

    void OnValidate()
    {
        m_timeLimit = Mathf.Max(1f, m_timeLimit);
        m_chanceCount = Mathf.Max(1, m_chanceCount);
        m_chanceTime = Mathf.Max(0.1f, m_chanceTime);
        m_reelTurns = Mathf.Max(0.5f, m_reelTurns);
        m_breakTime = Mathf.Max(0.1f, m_breakTime);
    }
}
