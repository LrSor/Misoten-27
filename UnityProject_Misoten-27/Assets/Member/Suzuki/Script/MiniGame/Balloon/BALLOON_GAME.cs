using UnityEngine;
using UnityEngine.UI;

//==================================================
// ミニゲーム「突風バルーンキャッチ」の本体
//
// 子どもの風船が飛ばされた！ Joy-Con を縦に持ち、ZL を押しながら水平に円を描くように回し（縄をブンブン）、
// 風船が真ん中に来たら ZL を離して、縦に振り下ろして縄を投げ、キャッチする
//
// ・ZL を押している間（回している間）は、振り下ろしを見ない（回す動きで勝手に投げないように）
// ・ZL を離してから少しの間だけ、振り下ろしを受け付ける。その間に振り下ろすと投げる
//
// ・制限時間内に、風船が真ん中を通るチャンスは 3 回（BALLOON_FLIGHT の「流れ方」で調整）
// ・風船が見えたら、カメラが少しズーム
// ・一定の速さ以上で回していないと、縄がへたってキャッチできない
// ・縄はいつも真ん中へまっすぐ飛ぶ。縄が届いた瞬間に、風船が真ん中の判定エリアにいればキャッチ（クリア）
//   （縄が飛んでいる間も風船は動くので、少し先を読んで投げる必要がある）
//   キャッチできたら、ズーム＆集中線。はずれたときは特に何もない（また回して投げられる）
// ・スタートと同時にアクション指示。回していない・遅いときは点滅
// ・時間切れ、または 3 回とも逃したらゲームオーバー（スコアは渡さない）
// ・スコア：何回目のチャンスでキャッチしたか（1 回目が最高）
//
// ・高橋さんのテンプレート（Template_MiniGame）をもとにしている
//==================================================
public class BALLOON_GAME : MiniGame
{
    enum STATE
    {
        PLAY,
        CATCH,
        FAILED,
        DONE,
    }

    [Header("入力と見た目")]
    [Tooltip("回す・ボタン・振り下ろしの入力（「ボタンを使う」「振り下ろしを調べる」をオン、ボタンは ZL にしておく）")]
    [SerializeField]
    private ROTATE_INPUT m_input;

    [SerializeField]
    private BALLOON_FLIGHT m_flight;

    [SerializeField]
    private BALLOON_LASSO m_lasso;

    [SerializeField]
    private MINIGAME_CAMERA m_camera;

    [Tooltip("キャッチしたときの集中線（空欄でもよい）")]
    [SerializeField]
    private SPEED_LINES m_speedLines;

    [Header("ルール")]
    [Tooltip("制限時間（秒）")]
    [SerializeField]
    private float m_timeLimit = 40f;

    [Tooltip("真ん中の判定エリアの半分の幅（m）。縄が届いた瞬間に、風船がこの中にいればキャッチ")]
    [SerializeField]
    private float m_zoneHalfWidth = 0.9f;

    [Tooltip("1 秒にこの回転数以上で回していないと、縄がへたってキャッチできない")]
    [SerializeField]
    private float m_needRate = 1.0f;

    [Tooltip("ZL を離す直前、この秒数以内にしっかり回していればよい（離すときに回転がゆるむため）")]
    [SerializeField]
    private float m_spinGrace = 0.6f;

    [Tooltip("ZL を離してから、この秒数のあいだに振り下ろすと投げる")]
    [SerializeField]
    private float m_throwWindow = 1.0f;

    [Tooltip("投げた瞬間の振動の強さ（0 で振動しない）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_throwRumble = 0.7f;

    [Tooltip("投げた瞬間の振動の長さ（秒）")]
    [SerializeField]
    private float m_throwRumbleTime = 0.15f;

    [Tooltip("キャッチしてから終わるまでの時間（秒）")]
    [SerializeField]
    private float m_catchWaitTime = 2f;

    [Header("判定エリアの目印（空欄でもよい）")]
    [Tooltip("左右の目印。判定エリアの幅に合わせて自動で動かす")]
    [SerializeField]
    private Transform[] m_zoneMarkers;

    [Header("カメラ")]
    [SerializeField]
    private float m_normalFov = 60f;

    [Tooltip("風船が見えているときの画角（少しズーム）")]
    [SerializeField]
    private float m_balloonFov = 50f;

    [Tooltip("キャッチしたときの画角（ズーム）")]
    [SerializeField]
    private float m_catchFov = 38f;

    [Tooltip("風船がこの X より内側に来たら「見えた」とする")]
    [SerializeField]
    private float m_visibleX = 8f;

    [Header("スコア")]
    [Tooltip("何回目のチャンスでキャッチしたかの点数（1 回目、2 回目、3 回目…）")]
    [SerializeField]
    private int[] m_chanceScores = { 1000, 700, 450 };

    [Header("表示（空欄でもよい）")]
    [Tooltip("残り時間と何回目か")]
    [SerializeField]
    private Text m_infoText;

    [Tooltip("アクション指示")]
    [SerializeField]
    private Text m_promptText;

    [Tooltip("真ん中に大きく出す文字（キャッチ！など）")]
    [SerializeField]
    private Text m_centerText;

    [Tooltip("アクション指示の点滅の速さ")]
    [SerializeField]
    private float m_blinkSpeed = 4f;

    private STATE m_state = STATE.DONE;
    private float m_timeLeft;
    private float m_stateTimer;
    private float m_lastEnoughTime = -999f;
    private float m_centerTextTimer;
    private float m_speedLinesTimer;
    private float m_throwWindowTimer;     // ZL を離したあと、振り下ろしを受け付ける残り時間
    private float m_releasedSpeed;        // ZL を離したときの回す速さ（縄の見た目用）

    public override float NetworkRemainingSeconds { get { return Mathf.Max(0f, m_timeLeft); } }

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        PlaceZoneMarkers();
        SetUiActive(false);
    }

    public override void Activate()
    {
        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        m_state = STATE.PLAY;
        m_timeLeft = m_timeLimit;
        m_lastEnoughTime = -999f;
        m_throwWindowTimer = 0f;
        m_stars = null;
        m_speedLinesTimer = 0f;

        if (m_flight != null)
        {
            m_flight.StartFlights();
        }

        if (m_lasso != null)
        {
            m_lasso.ResetLasso();
        }

        if (m_camera != null)
        {
            m_camera.ResetView();
            m_camera.ZoomFov = m_normalFov;
        }

        if (m_speedLines != null)
        {
            m_speedLines.Intensity = 0f;
        }

        PlaceZoneMarkers();
        SetUiActive(true);
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        SetUiActive(false);

        if (m_flight != null)
        {
            m_flight.Stop();
        }

        if (m_camera != null)
        {
            m_camera.ZoomFov = 0f;
            m_camera.Shake = 0f;
        }

        if (m_speedLines != null)
        {
            m_speedLines.Intensity = 0f;
        }
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        float deltaTime = Time.deltaTime;

        switch (m_state)
        {
            case STATE.PLAY:
                UpdatePlay(deltaTime);
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

        UpdateEffects(deltaTime);
        UpdateUi(deltaTime);
    }

    void UpdatePlay(float deltaTime)
    {
        // 時間切れ
        m_timeLeft -= deltaTime;

        if (m_timeLeft <= 0f)
        {
            m_timeLeft = 0f;
            Fail();
            return;
        }

        // ZL を押しながら回すと、縄が回る
        bool isHeld = m_input != null && m_input.IsButtonHeld;
        bool isEnough = isHeld && m_input.TurnRate >= m_needRate;

        if (isEnough)
        {
            m_lastEnoughTime = Time.time;
        }

        // ZL を離してから、振り下ろすまでのあいだは、縄が回り続けている（だんだんゆっくり）
        bool isWaitingThrow = m_throwWindowTimer > 0f;

        if (isWaitingThrow)
        {
            m_throwWindowTimer -= deltaTime;
            m_releasedSpeed = Mathf.MoveTowards(m_releasedSpeed, 0f, deltaTime * 0.5f);
        }

        if (m_lasso != null)
        {
            if (isHeld)
            {
                m_lasso.SetSpin(m_input.Speed01, isEnough);
            }
            else
            {
                m_lasso.SetSpin(isWaitingThrow ? m_releasedSpeed : 0f, isWaitingThrow);
            }
        }

        // 風船を動かす
        if (m_flight != null)
        {
            m_flight.Tick(deltaTime);

            // 縄が届いたか（キャッチできたか）
            CheckLassoArrived();

            if (m_state != STATE.PLAY)
            {
                return;
            }

            // 3 回とも逃した
            if (m_flight.IsAllDone)
            {
                Fail();
                return;
            }
        }

        // 風船が見えたら少しズーム
        if (m_camera != null)
        {
            bool isVisible = m_flight != null && m_flight.IsFlying && Mathf.Abs(m_flight.X) < m_visibleX;
            m_camera.ZoomFov = isVisible ? m_balloonFov : m_normalFov;
        }

        if (m_input == null)
        {
            return;
        }

        // ZL を押している間（回している間）は、振り下ろしを見ない
        if (isHeld)
        {
            m_input.ConsumeSwingDown();
            m_throwWindowTimer = 0f;
        }

        // ZL を離した：しっかり回せていたら、振り下ろしを受け付ける
        if (m_input.ConsumeRelease() &&
            m_lasso != null && !m_lasso.IsBusy)
        {
            if (Time.time - m_lastEnoughTime <= m_spinGrace)
            {
                m_throwWindowTimer = m_throwWindow;
                m_releasedSpeed = m_input.Speed01;
                m_input.ConsumeSwingDown();
            }
            else
            {
                ShowCenterText("回転が足りない！", 0.8f);
            }
        }

        // 振り下ろした：投げる
        if (m_input.ConsumeSwingDown() && m_throwWindowTimer > 0f)
        {
            m_throwWindowTimer = 0f;
            TryThrow();
        }
    }

    void TryThrow()
    {
        if (m_lasso == null || m_lasso.IsBusy)
        {
            return;
        }

        // 縄はいつも真ん中へまっすぐ飛ぶ（高さは今の風船の高さ）
        float height = m_flight != null ? m_flight.Height : 4.5f;

        if (m_flight != null && m_flight.IsFlying && m_flight.Balloon != null)
        {
            height = m_flight.Balloon.localPosition.y;
        }

        Vector3 zoneCenter = m_flight != null
            ? m_flight.transform.TransformPoint(new Vector3(0f, height, m_flight.Depth))
            : transform.position + Vector3.forward * 8f;

        m_lasso.Throw(zoneCenter, null);

        // 投げた瞬間の振動
        if (m_input != null && m_throwRumble > 0f)
        {
            m_input.Rumble(m_throwRumble, m_throwRumbleTime);
        }
    }

    // 縄が真ん中に届いた瞬間に、風船がいればキャッチ
    void CheckLassoArrived()
    {
        if (m_lasso == null || !m_lasso.ConsumeArrived())
        {
            return;
        }

        if (m_flight != null && m_flight.IsInZone(m_zoneHalfWidth))
        {
            Transform balloon = m_flight.Balloon;

            m_flight.Caught();
            m_lasso.Attach(balloon);

            Catch();
        }
    }

    void Catch()
    {
        m_state = STATE.CATCH;
        m_stateTimer = m_catchWaitTime;

        int chance = m_flight != null ? m_flight.PassIndex : 0;

        //ゲームクリア：スコアを渡す
        int score = (m_chanceScores != null && m_chanceScores.Length > 0)
            ? m_chanceScores[Mathf.Clamp(chance, 0, m_chanceScores.Length - 1)]
            : 1000;
        SetScore(score);

        // 星（通信のときの評価）：1 回目のチャンスで星 3、2 回目で 2、3 回目以降は 1
        m_stars = Mathf.Clamp(3 - chance, 1, 3);

        // ズーム＆集中線
        if (m_camera != null)
        {
            m_camera.ZoomFov = m_catchFov;
            m_camera.Impulse(0.06f);
        }

        m_speedLinesTimer = m_catchWaitTime;

        ShowCenterText("キャッチ！", m_catchWaitTime);
    }

    void Fail()
    {
        if (m_state == STATE.FAILED || m_state == STATE.DONE)
        {
            return;
        }

        m_state = STATE.FAILED;

        if (m_camera != null)
        {
            m_camera.ZoomFov = m_normalFov;
        }

        //ミニゲーム終了フラグ（失敗なのでスコアは渡さない）
        FinishMiniGame(false);
    }

    //==================================================
    // 演出・表示
    //==================================================

    void UpdateEffects(float deltaTime)
    {
        if (m_speedLines == null)
        {
            return;
        }

        m_speedLinesTimer = Mathf.Max(0f, m_speedLinesTimer - deltaTime);
        m_speedLines.Intensity = m_speedLinesTimer > 0f ? 1f : 0f;
    }

    void UpdateUi(float deltaTime)
    {
        if (m_infoText != null)
        {
            int count = m_flight != null ? m_flight.PassCount : 3;
            int current = m_flight != null ? Mathf.Min(m_flight.PassIndex + 1, count) : 1;

            m_infoText.text = "のこり " + Mathf.CeilToInt(m_timeLeft) + " 秒　風船 " + current + " / " + count + " 回目";
        }

        if (m_centerText != null && m_centerText.gameObject.activeSelf)
        {
            m_centerTextTimer -= deltaTime;

            if (m_centerTextTimer <= 0f)
            {
                m_centerText.gameObject.SetActive(false);
            }
        }

        if (m_promptText == null)
        {
            return;
        }

        bool isPlaying = m_state == STATE.PLAY;

        if (m_promptText.gameObject.activeSelf != isPlaying)
        {
            m_promptText.gameObject.SetActive(isPlaying);
        }

        if (!isPlaying)
        {
            return;
        }

        bool isHeld = m_input != null && m_input.IsButtonHeld;
        bool isEnough = (isHeld && m_input.TurnRate >= m_needRate) || m_throwWindowTimer > 0f;
        bool isChance = m_flight != null && m_flight.IsInZone(m_zoneHalfWidth);

        if (m_throwWindowTimer > 0f)
        {
            m_promptText.text = "振り下ろせ！";
        }
        else if (!isHeld)
        {
            m_promptText.text = "ZLを押しながら、縄をグルグル回せ！";
        }
        else if (isEnough && isChance)
        {
            m_promptText.text = "今だ！ZLを離して振り下ろせ！";
        }
        else
        {
            m_promptText.text = "回したまま、風船が真ん中に来たらZLを離して振り下ろせ！";
        }

        // 回していない・遅いときは点滅
        bool show = isEnough || Mathf.Repeat(Time.time * m_blinkSpeed, 1f) < 0.6f;

        Color color = m_promptText.color;
        color.a = show ? 1f : 0.15f;
        m_promptText.color = color;
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

    void SetUiActive(bool isActive)
    {
        if (m_infoText != null)
        {
            m_infoText.gameObject.SetActive(isActive);
        }

        if (m_promptText != null)
        {
            m_promptText.gameObject.SetActive(isActive);
        }

        if (m_centerText != null)
        {
            m_centerText.gameObject.SetActive(false);
        }
    }

    // 判定エリアの目印を、幅に合わせて左右に置く
    void PlaceZoneMarkers()
    {
        if (m_zoneMarkers == null)
        {
            return;
        }

        for (int i = 0; i < m_zoneMarkers.Length; i++)
        {
            if (m_zoneMarkers[i] == null)
            {
                continue;
            }

            Vector3 position = m_zoneMarkers[i].localPosition;
            position.x = (i % 2 == 0 ? -1f : 1f) * m_zoneHalfWidth;
            m_zoneMarkers[i].localPosition = position;
        }
    }

    void OnValidate()
    {
        m_timeLimit = Mathf.Max(1f, m_timeLimit);
        m_zoneHalfWidth = Mathf.Max(0.1f, m_zoneHalfWidth);
        m_needRate = Mathf.Max(0f, m_needRate);
    }
}
