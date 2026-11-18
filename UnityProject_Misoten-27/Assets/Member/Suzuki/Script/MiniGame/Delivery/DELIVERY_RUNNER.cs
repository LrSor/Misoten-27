using UnityEngine;

//==================================================
// 「特急デリバリー」の配達員（プレイヤー）の見た目
//
// ・走る速さで 3 段階に変わる：遅い / ふつう / 速い
//   （振っていないときは「遅い」）
// ・今は仮の見た目（カプセル）で、段階ごとに色・前かがみ・上下のゆれ方が変わる
//
// ■ 本番のモデルができたら
//   ・「段階ごとに切り替える物」に、遅い・ふつう・速い のモデル（またはエフェクト）を入れると、
//     その段階のものだけ表示される
//   ・アニメーターを入れると、パラメーター（Speed：0 / 1 / 2）を送る
//   ・仮の色が要らなくなったら「仮の色を付ける」をオフにする
//==================================================
public class DELIVERY_RUNNER : MonoBehaviour
{
    [Header("動かす体")]
    [Tooltip("前かがみ・上下にゆらす部分（仮のカプセル）")]
    [SerializeField]
    private Transform m_body;

    [Header("段階（遅い / ふつう / 速い）")]
    [Tooltip("段階ごとに切り替える物（空欄でもよい）")]
    [SerializeField]
    private GameObject[] m_levelObjects = new GameObject[3];

    [Tooltip("段階ごとの前かがみの角度（度）")]
    [SerializeField]
    private float[] m_leanAngles = { 5f, 15f, 28f };

    [Tooltip("段階ごとの、1 秒に上下にゆれる回数")]
    [SerializeField]
    private float[] m_bobSpeeds = { 2f, 3.5f, 5.5f };

    [Tooltip("上下にゆれる幅（m）")]
    [SerializeField]
    private float m_bobHeight = 0.12f;

    [Header("仮の色")]
    [Tooltip("段階ごとに色を付ける（本番のモデルにしたらオフ）")]
    [SerializeField]
    private bool m_usePlaceholderColor = true;

    [Tooltip("色を付ける物（空欄なら「動かす体」全体）")]
    [SerializeField]
    private GameObject m_colorTarget;

    [Tooltip("段階ごとの色（遅い / ふつう / 速い）")]
    [SerializeField]
    private Color[] m_levelColors =
    {
        new Color(0.45f, 0.6f, 1f),
        new Color(1f, 0.75f, 0.2f),
        new Color(1f, 0.25f, 0.2f),
    };

    [Header("アニメーション（本番のモデル用）")]
    [SerializeField]
    private Animator m_animator;

    [SerializeField]
    private string m_speedParameter = "Speed";

    private int m_level = -1;
    private float m_lean;
    private float m_bobPhase;
    private bool m_isGoal;
    private Vector3 m_bodyStartLocalPosition;
    private Quaternion m_bodyStartLocalRotation;
    private bool m_hasBodyStart;

    void Awake()
    {
        SaveBodyStart();
    }

    void SaveBodyStart()
    {
        if (m_hasBodyStart || m_body == null)
        {
            return;
        }

        m_bodyStartLocalPosition = m_body.localPosition;
        m_bodyStartLocalRotation = m_body.localRotation;
        m_hasBodyStart = true;
    }

    // 振る速さの段階をもらう（振っていないときは「遅い」）
    public void SetLevel(SHAKE_INPUT.SPEED_LEVEL speedLevel)
    {
        int level = Mathf.Clamp((int)speedLevel - 1, 0, 2);

        if (level == m_level)
        {
            return;
        }

        m_level = level;

        if (m_levelObjects != null)
        {
            for (int i = 0; i < m_levelObjects.Length; i++)
            {
                if (m_levelObjects[i] != null)
                {
                    m_levelObjects[i].SetActive(i == level);
                }
            }
        }

        GameObject colorTarget = m_colorTarget != null
            ? m_colorTarget
            : (m_body != null ? m_body.gameObject : null);

        if (m_usePlaceholderColor &&
            colorTarget != null &&
            m_levelColors != null &&
            m_levelColors.Length > level)
        {
            PLACEHOLDER_COLOR.SetColor(colorTarget, m_levelColors[level]);
        }

        if (m_animator != null &&
            !string.IsNullOrEmpty(m_speedParameter))
        {
            m_animator.SetFloat(m_speedParameter, level);
        }
    }

    // ゴールした（体を起こして止まる）
    public void SetGoal()
    {
        m_isGoal = true;
    }

    // スタートの状態に戻す
    public void ResetVisual()
    {
        SaveBodyStart();

        m_isGoal = false;
        m_lean = 0f;
        m_bobPhase = 0f;
        m_level = -1;
        SetLevel(SHAKE_INPUT.SPEED_LEVEL.SLOW);

        if (m_body != null && m_hasBodyStart)
        {
            m_body.localPosition = m_bodyStartLocalPosition;
            m_body.localRotation = m_bodyStartLocalRotation;
        }
    }

    void Update()
    {
        if (m_body == null || !m_hasBodyStart || m_level < 0)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        float targetLean = m_isGoal ? 0f : GetValue(m_leanAngles, m_level);
        float bobSpeed = m_isGoal ? 0f : GetValue(m_bobSpeeds, m_level);

        m_lean = Mathf.Lerp(m_lean, targetLean, 1f - Mathf.Exp(-deltaTime * 8f));
        m_bobPhase += bobSpeed * deltaTime * Mathf.PI * 2f;

        float bob = m_isGoal ? 0f : Mathf.Abs(Mathf.Sin(m_bobPhase)) * m_bobHeight;

        m_body.localPosition = m_bodyStartLocalPosition + Vector3.up * bob;
        m_body.localRotation = Quaternion.Euler(m_lean, 0f, 0f) * m_bodyStartLocalRotation;
    }

    static float GetValue(float[] values, int index)
    {
        if (values == null || values.Length == 0)
        {
            return 0f;
        }

        return values[Mathf.Clamp(index, 0, values.Length - 1)];
    }
}
