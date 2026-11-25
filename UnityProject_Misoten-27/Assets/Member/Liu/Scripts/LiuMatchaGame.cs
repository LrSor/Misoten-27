using UnityEngine;
using UnityEngine.UI;

public class LiuMatchaGame : MiniGame
{
    [SerializeField] private LiuMatchaBowlDamage m_bowlDamage;
    [SerializeField] private QuestData m_questData;
    [SerializeField] private GameObject m_gameOverPanel;
    [SerializeField] private GameObject m_scoreDisplay;
    [SerializeField, Min(1)] private int m_targetTurns = 10;

    [Header("制限時間と評価（仮）")]
    [SerializeField, Min(1f)] private float m_timeLimit = 30f;
    [SerializeField, Min(0f)] private float m_twoStarSeconds = 7f;
    [SerializeField, Min(0f)] private float m_threeStarSeconds = 15f;

    [Header("表示")]
    [SerializeField] private bool m_showTimer;
    [SerializeField] private Text m_timerText;
    [SerializeField] private Text m_actionHint;
    [SerializeField] private Text m_starText;
    [SerializeField] private GameObject m_goodSpeedSparkles;

    [Header("クリア時の表情（2つ星は元の笑顔）")]
    [SerializeField] private Image m_resultFace;
    [SerializeField] private Sprite m_oneStarFace;
    [SerializeField] private Sprite m_threeStarFace;

    [Header("カメラのズーム")]
    [SerializeField, Min(1f)] private float m_maxZoom = 1.5f;
    [Tooltip("最大ズームに達する回転速度（周／秒）")]
    [SerializeField, Min(0.01f)] private float m_speedAtFullZoom = 2f;
    [Tooltip("カメラの表示サイズが1秒間に変化する量")]
    [SerializeField, Min(0.01f)] private float m_zoomUnitsPerSecond = 2.4f;

    [Header("回しすぎたときのカメラの揺れ")]
    [SerializeField, Min(0f)] private float m_shakeAmplitude = 0.04f;
    [Tooltip("横方向の揺れの回数（1秒あたり）")]
    [SerializeField, Min(0.1f)] private float m_shakeFrequency = 9f;

    private LiuMatchaWhiskController m_whisk;
    private LiuMatchaSpeedJudge m_speedJudge;
    private Camera m_matchaCamera;
    private float m_defaultCameraSize;
    private Vector3 m_defaultCameraPosition;
    private float m_shakeTime;
    private Text m_failureText;
    private string m_brokenBowlMessage;

    public override float NetworkRemainingSeconds => m_showTimer ? base.NetworkRemainingSeconds : -1f;

    private void Awake()
    {
        m_whisk = m_bowlDamage.GetComponent<LiuMatchaWhiskController>();
        m_speedJudge = m_bowlDamage.GetComponent<LiuMatchaSpeedJudge>();
        m_matchaCamera = GetComponentInChildren<Camera>(true);
        if (m_matchaCamera != null)
        {
            m_defaultCameraSize = m_matchaCamera.orthographicSize;
            m_defaultCameraPosition = m_matchaCamera.transform.localPosition;
        }
        SetActionHintVisible(false);
        SetSparklesVisible(false);
        if (m_timerText != null)
            m_timerText.gameObject.SetActive(false);

        if (m_gameOverPanel != null)
            m_failureText = m_gameOverPanel.GetComponentInChildren<Text>(true);
        if (m_failureText != null)
            m_brokenBowlMessage = m_failureText.text;

        // Score の子に星を設定した場合は、親の数字だけを非表示にする。
        if (m_starText != null && m_scoreDisplay != null
            && m_scoreDisplay.TryGetComponent(out Text numberText))
            numberText.enabled = false;
    }

    private void OnValidate()
    {
        m_timeLimit = Mathf.Max(1f, m_timeLimit);
        m_twoStarSeconds = Mathf.Clamp(m_twoStarSeconds, 0f, m_timeLimit);
        m_threeStarSeconds = Mathf.Clamp(m_threeStarSeconds, m_twoStarSeconds, m_timeLimit);
    }

    public override void Activate()
    {
        base.Activate();
        m_stars = 0;
        m_remainingSeconds = m_timeLimit;
        UpdateTimerDisplay();
        SetActionHintVisible(true);
        Debug.Log("[抹茶] ミニゲーム開始", this);
    }

    protected override void UpdateMiniGame()
    {
        if (GetIsFinish())
            return;

        // 前フレームの回転、破損、残り時間がすべて確定してから判定する。
        if (m_bowlDamage.IsBroken || m_remainingSeconds <= 0f)
        {
            if (m_failureText != null)
                m_failureText.text = m_bowlDamage.IsBroken
                    ? m_brokenBowlMessage : "ゲームオーバー\n時間切れです。";
            FinishMiniGame(false);
            return;
        }

        if (m_whisk.StirredTurns >= m_targetTurns)
        {
            int stars = 1;
            if (m_remainingSeconds >= m_threeStarSeconds)
                stars = 3;
            else if (m_remainingSeconds >= m_twoStarSeconds)
                stars = 2;

            SetScore(stars);
            m_stars = stars;
            if (m_starText != null)
                m_starText.text = new string('★', stars);
            if (m_resultFace != null)
            {
                // 2つ星のときは上書きを解除し、元の笑顔に戻す。
                m_resultFace.overrideSprite = stars switch
                {
                    1 => m_oneStarFace,
                    3 => m_threeStarFace,
                    _ => null
                };
            }
            FinishMiniGame(true);
            return;
        }

        base.UpdateMiniGame();
        var speed = m_speedJudge.CurrentState;
        SetActionHintVisible(speed == LiuMatchaSpeedJudge.SpeedState.Stopped
            || speed == LiuMatchaSpeedJudge.SpeedState.TooSlow);
        SetSparklesVisible(speed == LiuMatchaSpeedJudge.SpeedState.Good);
        UpdateCameraZoom(m_speedJudge.CurrentSpeed);
        UpdateCameraShake(speed == LiuMatchaSpeedJudge.SpeedState.TooFast);
        m_remainingSeconds = Mathf.Max(0f, m_remainingSeconds - Time.deltaTime);
        UpdateTimerDisplay();
    }

    private void UpdateCameraZoom(float turnsPerSecond)
    {
        if (m_matchaCamera == null || !m_matchaCamera.orthographic)
            return;

        float speedRatio = Mathf.Clamp01(turnsPerSecond / m_speedAtFullZoom);
        float zoom = Mathf.Lerp(1f, m_maxZoom, speedRatio);
        float targetSize = m_defaultCameraSize / zoom;
        m_matchaCamera.orthographicSize = Mathf.MoveTowards(
            m_matchaCamera.orthographicSize, targetSize, m_zoomUnitsPerSecond * Time.deltaTime);
    }

    private void UpdateCameraShake(bool tooFast)
    {
        if (m_matchaCamera == null || (!tooFast && m_shakeTime == 0f))
            return;

        Transform cameraTransform = m_matchaCamera.transform;
        if (!tooFast)
        {
            m_shakeTime = 0f;
            cameraTransform.localPosition = m_defaultCameraPosition;
            return;
        }

        m_shakeTime += Time.deltaTime;
        float phase = 2f * Mathf.PI * m_shakeFrequency * m_shakeTime;
        Vector3 offset = new Vector3(Mathf.Sin(phase), 0.6f * Mathf.Sin(phase * 1.37f), 0f);
        // 画面の左右・上下に、元の位置を基準に揺らす。
        cameraTransform.localPosition = m_defaultCameraPosition
            + cameraTransform.localRotation * (m_shakeAmplitude * offset);
    }

    private void ResetCameraView()
    {
        UpdateCameraShake(false);
        if (m_matchaCamera != null && m_matchaCamera.orthographic)
            m_matchaCamera.orthographicSize = m_defaultCameraSize;
    }

    private void OnDisable()
    {
        ResetCameraView();
    }

    private void SetActionHintVisible(bool visible)
    {
        if (m_actionHint != null && m_actionHint.gameObject.activeSelf != visible)
            m_actionHint.gameObject.SetActive(visible);
    }

    private void SetSparklesVisible(bool visible)
    {
        // 「Play On Awake」が有効なので、適正速度に入ると再生が始まる。
        if (m_goodSpeedSparkles != null && m_goodSpeedSparkles.activeSelf != visible)
            m_goodSpeedSparkles.SetActive(visible);
    }

    private void UpdateTimerDisplay()
    {
        if (m_timerText == null)
            return;

        if (m_timerText.gameObject.activeSelf != m_showTimer)
            m_timerText.gameObject.SetActive(m_showTimer);
        if (m_showTimer)
            m_timerText.text = Mathf.CeilToInt(m_remainingSeconds).ToString();
    }

    public override void Deactivate()
    {
        base.Deactivate();
        ResetCameraView();
        SetActionHintVisible(false);
        SetSparklesVisible(false);
        if (m_timerText != null)
            m_timerText.gameObject.SetActive(false);

        if (GetIsFinish() && !GetIsGameClear())
        {
            if (m_scoreDisplay != null)
                m_scoreDisplay.SetActive(false);
            if (m_gameOverPanel != null)
                m_gameOverPanel.SetActive(true);
        }

        Debug.Log("[抹茶] ミニゲーム終了", this);
    }
}
