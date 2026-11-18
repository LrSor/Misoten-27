using UnityEngine;

//==================================================
// 振り方に合わせて演出を変える（振るミニゲーム 3 つで共通の部品）
//
// ・SHAKE_INPUT の「速さの段階」と「まっすぐさ」を見て、
//     集中線・キラキラ・カメラのゆれ・ズーム・アクション指示 を自動で変える
// ・使わない演出は空欄のままでよい
// ・何を見て強くするか（速さ / まっすぐさ / 両方）は演出ごとに選べる
//     例）特急デリバリー：まっすぐなほどキラキラ＆集中線 → 「まっすぐさ」
//         神速クッキング：速さに応じて集中線＆ズーム   → 「速さ」
//
// ■ 使い方
//   ミニゲーム本体（MiniGame）の「m_objects」に、SHAKE_INPUT の次に入れる
//==================================================
public class SHAKE_EFFECTS : MiniGameObject
{
    public enum SOURCE
    {
        SPEED,                  // 速さの段階
        STRAIGHT,               // まっすぐさ
        SPEED_AND_STRAIGHT,     // 両方（速くて、まっすぐなほど強い）
    }

    [Header("見る入力")]
    [SerializeField]
    private SHAKE_INPUT m_input;

    [Header("集中線")]
    [SerializeField]
    private SPEED_LINES m_speedLines;

    [SerializeField]
    private SOURCE m_speedLinesSource = SOURCE.SPEED;

    [Tooltip("段階ごとの強さ（NONE / SLOW / NORMAL / FAST）")]
    [SerializeField]
    private float[] m_speedLinesByLevel = { 0f, 0.25f, 0.6f, 1f };

    [Header("キラキラ")]
    [SerializeField]
    private SPARKLE_EFFECT m_sparkle;

    [SerializeField]
    private SOURCE m_sparkleSource = SOURCE.STRAIGHT;

    [Tooltip("段階ごとの強さ（NONE / SLOW / NORMAL / FAST）")]
    [SerializeField]
    private float[] m_sparkleByLevel = { 0f, 0.4f, 0.7f, 1f };

    [Header("カメラ")]
    [SerializeField]
    private MINIGAME_CAMERA m_camera;

    [Tooltip("段階ごとのゆれの強さ（m）（NONE / SLOW / NORMAL / FAST）")]
    [SerializeField]
    private float[] m_shakeByLevel = { 0f, 0f, 0.02f, 0.06f };

    [Tooltip("段階ごとにズームする")]
    [SerializeField]
    private bool m_useZoom = false;

    [Tooltip("段階ごとの画角（小さいほどズーム）（NONE / SLOW / NORMAL / FAST）")]
    [SerializeField]
    private float[] m_fovByLevel = { 60f, 57f, 52f, 46f };

    [Header("アクション指示")]
    [Tooltip("振っていない・弱いときに出すもの（「振れ！」の文字や画像）")]
    [SerializeField]
    private GameObject m_actionPrompt;

    [Tooltip("振るのをやめてから、何秒で指示を出すか")]
    [SerializeField]
    private float m_promptDelay = 0.6f;

    [Tooltip("点滅の速さ（0 で点滅しない）")]
    [SerializeField]
    private float m_promptBlinkSpeed = 4f;

    private float m_idleTime;
    private bool m_isActive;

    void Start()
    {
        SetEffectsActive(false);
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_isActive = true;
        m_idleTime = 0f;

        SetEffectsActive(true);
        Apply(0f, 0f, 0f, 0f);

        if (m_camera != null)
        {
            m_camera.ResetView();
        }
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        m_isActive = false;

        Apply(0f, 0f, 0f, 0f);
        SetEffectsActive(false);

        if (m_camera != null)
        {
            m_camera.Shake = 0f;
            m_camera.ZoomFov = 0f;
        }
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        if (!m_isActive || m_input == null)
        {
            return;
        }

        int level = (int)m_input.Level;

        float speedLines = GetValue(m_speedLinesSource, m_speedLinesByLevel, level);
        float sparkle = GetValue(m_sparkleSource, m_sparkleByLevel, level);
        float shake = GetLevelValue(m_shakeByLevel, level, 0f);
        float fov = m_useZoom ? GetLevelValue(m_fovByLevel, level, 0f) : 0f;

        Apply(speedLines, sparkle, shake, fov);

        UpdatePrompt();
    }

    void Apply(float speedLines, float sparkle, float shake, float fov)
    {
        if (m_speedLines != null)
        {
            m_speedLines.Intensity = speedLines;
        }

        if (m_sparkle != null)
        {
            m_sparkle.Intensity = sparkle;
        }

        if (m_camera != null)
        {
            m_camera.Shake = shake;

            if (m_useZoom)
            {
                m_camera.ZoomFov = fov;
            }
        }
    }

    // 段階の値に、まっすぐさを掛ける
    float GetValue(SOURCE source, float[] values, int level)
    {
        float straight = m_input.Straightness;

        switch (source)
        {
            case SOURCE.STRAIGHT:
                // 振っていないときは出さない。振っていれば、まっすぐなほど強い
                if (level == 0)
                {
                    return 0f;
                }

                return straight * GetLevelValue(values, values != null ? values.Length - 1 : 0, 1f);

            case SOURCE.SPEED_AND_STRAIGHT:
                return GetLevelValue(values, level, 0f) * straight;

            default:
                return GetLevelValue(values, level, 0f);
        }
    }

    static float GetLevelValue(float[] values, int level, float fallback)
    {
        if (values == null || values.Length == 0)
        {
            return fallback;
        }

        return values[Mathf.Clamp(level, 0, values.Length - 1)];
    }

    void UpdatePrompt()
    {
        if (m_actionPrompt == null)
        {
            return;
        }

        if (m_input.IsIdle)
        {
            m_idleTime += Time.deltaTime;
        }
        else
        {
            m_idleTime = 0f;
        }

        bool show = m_idleTime >= m_promptDelay;

        if (show && m_promptBlinkSpeed > 0f)
        {
            show = Mathf.Repeat(Time.time * m_promptBlinkSpeed, 1f) < 0.65f;
        }

        if (m_actionPrompt.activeSelf != show)
        {
            m_actionPrompt.SetActive(show);
        }
    }

    void SetEffectsActive(bool isActive)
    {
        if (m_speedLines != null)
        {
            m_speedLines.gameObject.SetActive(isActive);
        }

        if (m_sparkle != null)
        {
            m_sparkle.gameObject.SetActive(isActive);
        }

        if (m_actionPrompt != null)
        {
            m_actionPrompt.SetActive(false);
        }
    }
}
