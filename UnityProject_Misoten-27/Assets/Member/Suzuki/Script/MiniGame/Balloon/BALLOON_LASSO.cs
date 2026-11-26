using UnityEngine;

//==================================================
// 「突風バルーンキャッチ」の縄（投げ縄）の見た目
//
// ・回している間：頭の上で縄の輪がブンブン回る。速さが足りないと、輪がへたって下がる
// ・投げる：輪が狙った場所まで飛んでいき、少し止まってから戻ってくる
//   届いた瞬間に ConsumeArrived() が 1 回だけ true になる（そこでキャッチできたか判定する）
//   Attach(風船) を呼ぶと、風船をくっつけたまま戻ってくる
//
// ・今は仮の見た目（小さい箱を円く並べた輪）。本番のモデルに差し替えるときは、
//   「縄の輪」の子のモデルを差し替えればよい
//==================================================
public class BALLOON_LASSO : MonoBehaviour
{
    enum STATE
    {
        SPIN,       // 頭の上で回している
        OUT,        // 投げて飛んでいる
        HOLD,       // 狙った場所で止まっている
        BACK,       // 戻ってくる
        CARRY,      // 風船をつかまえて手元にある
    }

    [Header("縄")]
    [Tooltip("縄の輪（これを回したり飛ばしたりする）")]
    [SerializeField]
    private Transform m_loop;

    [Tooltip("手の位置（回すときの中心、戻ってくる場所）")]
    [SerializeField]
    private Transform m_hand;

    [Tooltip("一番速いときの回る速さ（度/秒）")]
    [SerializeField]
    private float m_maxSpinSpeed = 900f;

    [Header("へたる（速さが足りないとき）")]
    [Tooltip("へたったときに、輪が傾く角度（度）")]
    [SerializeField]
    private float m_droopAngle = 60f;

    [Tooltip("へたったときの輪の大きさ（倍）")]
    [SerializeField]
    private float m_droopScale = 0.6f;

    [Tooltip("へたったときに、輪が下がる高さ（m）")]
    [SerializeField]
    private float m_droopDown = 0.6f;

    [Header("投げる")]
    [Tooltip("飛んでいく時間（秒）")]
    [SerializeField]
    private float m_outTime = 0.35f;

    [Tooltip("狙った場所で止まる時間（秒）")]
    [SerializeField]
    private float m_holdTime = 0.25f;

    [Tooltip("戻ってくる時間（秒）")]
    [SerializeField]
    private float m_backTime = 0.5f;

    [Tooltip("飛んでいくときの山の高さ（m）")]
    [SerializeField]
    private float m_throwArc = 1.5f;

    private STATE m_state = STATE.SPIN;
    private float m_timer;
    private float m_spinAngle;
    private float m_speed01;
    private bool m_isEnough;
    private float m_droop;              // 0 = しっかり回っている、1 = へたっている
    private Vector3 m_loopStartScale = Vector3.one;
    private Vector3 m_throwFrom;
    private Vector3 m_throwTo;
    private Transform m_caught;
    private bool m_isArrived;

    // 投げている最中か（このあいだは次を投げられない）
    public bool IsBusy { get { return m_state != STATE.SPIN; } }

    void Awake()
    {
        if (m_loop != null)
        {
            m_loopStartScale = m_loop.localScale;
        }
    }

    // 最初の状態に戻す
    public void ResetLasso()
    {
        m_state = STATE.SPIN;
        m_caught = null;
        m_isArrived = false;
        m_droop = 1f;
        m_speed01 = 0f;
        m_isEnough = false;
    }

    // 回す速さ（0～1）と、速さが足りているか
    public void SetSpin(float speed01, bool isEnough)
    {
        m_speed01 = Mathf.Clamp01(speed01);
        m_isEnough = isEnough;
    }

    // 狙った場所に届いたか（届いた瞬間に 1 回だけ true）
    public bool ConsumeArrived()
    {
        bool isArrived = m_isArrived;
        m_isArrived = false;
        return isArrived;
    }

    // 風船をつかまえた（くっつけたまま戻ってくる）
    public void Attach(Transform caught)
    {
        m_caught = caught;
    }

    // 投げる（caught：最初からつかまえている物。ふつうは null で、届いたときに Attach する）
    public void Throw(Vector3 worldTarget, Transform caught)
    {
        if (m_loop == null)
        {
            return;
        }

        m_state = STATE.OUT;
        m_timer = 0f;
        m_throwFrom = m_loop.position;
        m_throwTo = worldTarget;
        m_caught = caught;
        m_isArrived = false;
    }

    void Update()
    {
        if (m_loop == null || m_hand == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        m_timer += deltaTime;

        // 輪はいつも回っている（投げている間はゆっくり）
        float spinSpeed = m_state == STATE.SPIN ? m_maxSpinSpeed * m_speed01 : 200f;
        m_spinAngle += spinSpeed * deltaTime;

        switch (m_state)
        {
            case STATE.SPIN:
            {
                // 速さが足りないと、へたる
                m_droop = Mathf.MoveTowards(m_droop, m_isEnough ? 0f : 1f, deltaTime * 3f);

                m_loop.position = m_hand.position + Vector3.down * (m_droopDown * m_droop);
                m_loop.rotation = m_hand.rotation *
                                  Quaternion.Euler(m_droopAngle * m_droop, 0f, 0f) *
                                  Quaternion.Euler(0f, m_spinAngle, 0f);
                m_loop.localScale = m_loopStartScale * Mathf.Lerp(1f, m_droopScale, m_droop);
                break;
            }

            case STATE.OUT:
            {
                float t = Mathf.Clamp01(m_timer / Mathf.Max(0.01f, m_outTime));
                m_loop.position = Vector3.Lerp(m_throwFrom, m_throwTo, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * m_throwArc);
                m_loop.rotation = Quaternion.Euler(0f, m_spinAngle, 0f);
                m_loop.localScale = m_loopStartScale;

                if (t >= 1f)
                {
                    m_state = STATE.HOLD;
                    m_timer = 0f;
                    m_isArrived = true;
                }
                break;
            }

            case STATE.HOLD:
                m_loop.position = m_throwTo;
                m_loop.rotation = Quaternion.Euler(0f, m_spinAngle, 0f);

                // つかまえた風船は、輪の上にくっつく
                if (m_caught != null)
                {
                    m_caught.position = m_loop.position + Vector3.up * 0.6f;
                }

                if (m_timer >= m_holdTime)
                {
                    m_state = STATE.BACK;
                    m_timer = 0f;
                }
                break;

            case STATE.BACK:
            {
                float t = Mathf.Clamp01(m_timer / Mathf.Max(0.01f, m_backTime));
                Vector3 hand = m_hand.position;

                m_loop.position = Vector3.Lerp(m_throwTo, hand, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * m_throwArc * 0.5f);
                m_loop.rotation = Quaternion.Euler(0f, m_spinAngle, 0f);

                if (m_caught != null)
                {
                    m_caught.position = m_loop.position + Vector3.up * 0.6f;
                }

                if (t >= 1f)
                {
                    m_state = m_caught != null ? STATE.CARRY : STATE.SPIN;
                    m_timer = 0f;
                    m_droop = 1f;
                }
                break;
            }

            case STATE.CARRY:
                // 風船を手元で持っている（ふわふわ）
                m_loop.position = m_hand.position;
                m_loop.rotation = Quaternion.Euler(0f, m_spinAngle, 0f);

                if (m_caught != null)
                {
                    m_caught.position = m_hand.position + Vector3.up * (1.2f + Mathf.Sin(m_timer * 2f) * 0.1f);
                }
                break;
        }
    }
}
