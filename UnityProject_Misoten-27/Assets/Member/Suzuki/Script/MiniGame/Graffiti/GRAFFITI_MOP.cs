using UnityEngine;

//==================================================
// 「落書きクリーンアップ」のモップ
//
// ・振っている間は、画面の下からモップがニョキニョキ出てくる
//   振るのをやめると、また下に引っ込む
// ・1 回振るごとに、モップが左右にゴシゴシ動く
// ・カメラの子に置いて使う（画面に対して出し入れするため）
//==================================================
public class GRAFFITI_MOP : MonoBehaviour
{
    [Tooltip("見る入力")]
    [SerializeField]
    private SHAKE_INPUT m_input;

    [Header("出し入れ")]
    [Tooltip("引っ込んでいるときの高さ（カメラから見た Y）")]
    [SerializeField]
    private float m_hiddenY = -1.9f;

    [Tooltip("出ているときの高さ（カメラから見た Y）")]
    [SerializeField]
    private float m_shownY = -0.75f;

    [Tooltip("最後に振ってから、何秒で引っ込むか")]
    [SerializeField]
    private float m_showTime = 0.6f;

    [Tooltip("出し入れの速さ")]
    [SerializeField]
    private float m_moveSmoothness = 10f;

    [Header("ゴシゴシ")]
    [Tooltip("左右に傾く角度（度）")]
    [SerializeField]
    private float m_swishAngle = 25f;

    [Tooltip("左右に動く幅（m）")]
    [SerializeField]
    private float m_swishWidth = 0.15f;

    [Tooltip("傾きが変わる速さ")]
    [SerializeField]
    private float m_swishSmoothness = 18f;

    private Vector3 m_startLocalPosition;
    private float m_lastSwingTime = -999f;
    private int m_side = 1;
    private float m_angle;
    private float m_offsetX;

    void Awake()
    {
        m_startLocalPosition = transform.localPosition;
        ResetMop();
    }

    void OnEnable()
    {
        if (m_input != null)
        {
            m_input.Swung += OnSwung;
        }
    }

    void OnDisable()
    {
        if (m_input != null)
        {
            m_input.Swung -= OnSwung;
        }
    }

    void OnSwung(float point)
    {
        m_lastSwingTime = Time.time;
        m_side = -m_side;
    }

    // 引っ込めた状態に戻す
    public void ResetMop()
    {
        m_lastSwingTime = -999f;
        m_angle = 0f;
        m_offsetX = 0f;

        transform.localPosition = new Vector3(m_startLocalPosition.x, m_hiddenY, m_startLocalPosition.z);
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        bool isShown =
            m_input != null &&
            m_input.IsActive &&
            Time.time - m_lastSwingTime <= m_showTime;

        // 出し入れ
        Vector3 position = transform.localPosition;
        float targetY = isShown ? m_shownY : m_hiddenY;
        position.y = Mathf.Lerp(position.y, targetY, 1f - Mathf.Exp(-deltaTime * m_moveSmoothness));

        // ゴシゴシ（振るたびに左右が入れかわる）
        float targetAngle = isShown ? m_swishAngle * m_side : 0f;
        float targetOffset = isShown ? m_swishWidth * m_side : 0f;
        float t = 1f - Mathf.Exp(-deltaTime * m_swishSmoothness);

        m_angle = Mathf.Lerp(m_angle, targetAngle, t);
        m_offsetX = Mathf.Lerp(m_offsetX, targetOffset, t);

        position.x = m_startLocalPosition.x + m_offsetX;
        transform.localPosition = position;
        transform.localRotation = Quaternion.Euler(0f, 0f, -m_angle);
    }
}
