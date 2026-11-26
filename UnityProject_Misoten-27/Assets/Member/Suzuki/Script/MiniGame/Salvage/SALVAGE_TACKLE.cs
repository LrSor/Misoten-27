using UnityEngine;

//==================================================
// 「漂流サルベージ」の釣り道具の見た目（竿・糸・うき・落とし物）
//
// ・SALVAGE_GAME から「投げる・待つ・チャンス・巻く・釣り上げ・糸が切れる」を伝えられて動く
// ・今は仮の見た目（カプセルや箱）。本番のモデルに差し替えるときは、
//   それぞれの Transform の子のモデルを差し替えればよい
//
//   竿の根元（m_rodPivot）を回して、竿を引っぱられたように曲げる
//   糸（m_line）は、竿の先からうきまで、細い箱を伸ばして表している
//==================================================
public class SALVAGE_TACKLE : MonoBehaviour
{
    enum STATE
    {
        IDLE,
        CAST,
        FLOAT,
        BITE,
        REEL,
        CATCH,
        SNAP,
    }

    [Header("竿")]
    [Tooltip("竿の根元（ここを回して竿を傾ける）")]
    [SerializeField]
    private Transform m_rodPivot;

    [Tooltip("竿の先（糸の出るところ）")]
    [SerializeField]
    private Transform m_rodTip;

    [Tooltip("引っぱられたとき、竿が前に倒れる角度（度）")]
    [SerializeField]
    private float m_rodPullAngle = 25f;

    [Header("糸")]
    [Tooltip("糸（細い箱。竿の先からうきまで伸ばす）")]
    [SerializeField]
    private Transform m_line;

    [Tooltip("糸の太さ（m）")]
    [SerializeField]
    private float m_lineWidth = 0.02f;

    [Header("うき・落とし物")]
    [SerializeField]
    private Transform m_bobber;

    [Tooltip("釣れる落とし物（巻いている間だけ見える）")]
    [SerializeField]
    private Transform m_item;

    [Tooltip("うきを投げる先（真ん中）")]
    [SerializeField]
    private Transform m_castTarget;

    [Tooltip("投げる先の左右のばらつき（m）")]
    [SerializeField]
    private float m_castRandomX = 3f;

    [Tooltip("巻き終わったときに、落とし物が来る場所（防波堤の手前）")]
    [SerializeField]
    private Transform m_reelEnd;

    [Tooltip("投げたときに、うきが飛ぶ高さ（m）")]
    [SerializeField]
    private float m_castHeight = 4f;

    [Tooltip("海面の高さ（このオブジェクトから見た Y）")]
    [SerializeField]
    private float m_waterHeight = 0f;

    private STATE m_state = STATE.IDLE;
    private float m_timer;
    private float m_duration;
    private Vector3 m_castFrom;
    private Vector3 m_landing;          // うきが着いた場所（ローカル）
    private float m_progress;
    private float m_tension;
    private Quaternion m_rodStartRotation;
    private Vector3 m_itemStart;
    private Vector3 m_itemCatchFrom;

    void Awake()
    {
        if (m_rodPivot != null)
        {
            m_rodStartRotation = m_rodPivot.localRotation;
        }

        ResetTackle();
    }

    //==================================================
    // SALVAGE_GAME から呼ぶ
    //==================================================

    // 最初の状態に戻す（うきは竿の先、落とし物は隠す）
    public void ResetTackle()
    {
        m_state = STATE.IDLE;
        m_progress = 0f;
        m_tension = 0f;

        if (m_item != null)
        {
            m_item.gameObject.SetActive(false);
        }

        if (m_bobber != null && m_rodTip != null)
        {
            m_bobber.gameObject.SetActive(true);
            m_bobber.position = m_rodTip.position;
        }

        if (m_line != null)
        {
            m_line.gameObject.SetActive(true);
        }
    }

    // 投げる
    public void Cast(float seconds)
    {
        ResetTackle();

        m_state = STATE.CAST;
        m_timer = 0f;
        m_duration = Mathf.Max(0.1f, seconds);

        m_castFrom = m_rodTip != null ? transform.InverseTransformPoint(m_rodTip.position) : Vector3.zero;

        Vector3 target = m_castTarget != null ? m_castTarget.localPosition : new Vector3(0f, 0f, 10f);
        target.x += Random.Range(-m_castRandomX, m_castRandomX);
        target.y = m_waterHeight;
        m_landing = target;
    }

    // うきがプカプカ
    public void Float()
    {
        m_state = STATE.FLOAT;
        m_timer = 0f;
    }

    // チャンス（うきが沈む）
    public void Bite()
    {
        m_state = STATE.BITE;
        m_timer = 0f;
    }

    // 引っかかった（落とし物が見える）
    public void Hook()
    {
        m_state = STATE.REEL;
        m_progress = 0f;
        m_tension = 0f;

        if (m_item != null)
        {
            m_item.gameObject.SetActive(true);
            m_itemStart = m_landing + Vector3.down * 0.4f;
            m_item.localPosition = m_itemStart;
        }
    }

    // 巻いている（progress：0～1 の進み具合、tension：0～1 の引っぱられ具合）
    public void SetReel(float progress, float tension)
    {
        m_progress = Mathf.Clamp01(progress);
        m_tension = Mathf.Clamp01(tension);
    }

    // 釣り上げた（落とし物が竿の先まで飛び上がる）
    public void Catch()
    {
        m_state = STATE.CATCH;
        m_timer = 0f;
        m_tension = 0f;

        if (m_item != null)
        {
            m_itemCatchFrom = m_item.localPosition;
        }
    }

    // 糸が切れた（落とし物は沈み、糸が消える）
    public void Snap()
    {
        m_state = STATE.SNAP;
        m_timer = 0f;
        m_tension = 0f;

        if (m_line != null)
        {
            m_line.gameObject.SetActive(false);
        }
    }

    //==================================================
    // 動き
    //==================================================

    void Update()
    {
        float deltaTime = Time.deltaTime;
        m_timer += deltaTime;

        Vector3 bobber = m_landing;

        switch (m_state)
        {
            case STATE.IDLE:
                bobber = m_rodTip != null ? transform.InverseTransformPoint(m_rodTip.position) : Vector3.zero;
                break;

            case STATE.CAST:
            {
                // 竿の先から放物線を描いて飛ぶ
                float t = Mathf.Clamp01(m_timer / m_duration);
                bobber = Vector3.Lerp(m_castFrom, m_landing, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * m_castHeight);

                if (t >= 1f)
                {
                    m_state = STATE.FLOAT;
                    m_timer = 0f;
                }
                break;
            }

            case STATE.FLOAT:
                // プカプカ
                bobber = m_landing + Vector3.up * (Mathf.Sin(m_timer * 2.5f) * 0.06f);
                break;

            case STATE.BITE:
                // グッと沈んで、ピクピク動く
                bobber = m_landing + Vector3.down * 0.25f +
                         new Vector3(Mathf.Sin(m_timer * 30f) * 0.04f, Mathf.Sin(m_timer * 23f) * 0.05f, 0f);
                break;

            case STATE.REEL:
            {
                Vector3 end = m_reelEnd != null ? m_reelEnd.localPosition : Vector3.zero;
                end.y = m_waterHeight;

                // 引っぱられているほど、左右にゆれる
                float shake = Mathf.Lerp(0.03f, 0.25f, m_tension);
                Vector3 wobble = new Vector3(Mathf.Sin(m_timer * 9f) * shake, 0f, 0f);

                bobber = Vector3.Lerp(m_landing, end, m_progress) + wobble;

                if (m_item != null)
                {
                    m_item.localPosition = bobber + Vector3.down * 0.4f;
                    m_item.Rotate(0f, 40f * deltaTime, 0f, Space.Self);
                }
                break;
            }

            case STATE.CATCH:
            {
                // 落とし物が竿の先へ飛び上がる
                float t = Mathf.Clamp01(m_timer / 0.8f);
                Vector3 tip = m_rodTip != null ? transform.InverseTransformPoint(m_rodTip.position) : Vector3.zero;
                Vector3 target = tip + Vector3.down * 0.8f;

                if (m_item != null)
                {
                    m_item.localPosition = Vector3.Lerp(m_itemCatchFrom, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 2f);
                }

                bobber = m_item != null ? m_item.localPosition + Vector3.up * 0.4f : target;
                break;
            }

            case STATE.SNAP:
                // 落とし物は沈んでいく、うきはその場でプカプカ
                if (m_item != null)
                {
                    m_item.localPosition += Vector3.down * (deltaTime * 1.5f);

                    if (m_timer > 1f)
                    {
                        m_item.gameObject.SetActive(false);
                    }
                }

                bobber = (m_bobber != null ? transform.InverseTransformPoint(m_bobber.position) : m_landing);
                bobber.y = m_waterHeight + Mathf.Sin(m_timer * 2.5f) * 0.06f;
                break;
        }

        if (m_bobber != null)
        {
            m_bobber.position = transform.TransformPoint(bobber);
        }

        UpdateRod();
        UpdateLine();
    }

    // 竿を傾ける（引っぱられ具合と、チャンスのときのピクピク）
    void UpdateRod()
    {
        if (m_rodPivot == null)
        {
            return;
        }

        float angle = m_rodPullAngle * m_tension;

        if (m_state == STATE.BITE)
        {
            angle += 6f + Mathf.Sin(m_timer * 30f) * 3f;
        }
        else if (m_state == STATE.REEL)
        {
            angle += 4f + Mathf.Sin(m_timer * 12f) * (1f + m_tension * 4f);
        }

        m_rodPivot.localRotation = m_rodStartRotation * Quaternion.Euler(angle, 0f, 0f);
    }

    // 糸を、竿の先からうきまで伸ばす
    void UpdateLine()
    {
        if (m_line == null || m_rodTip == null || m_bobber == null || !m_line.gameObject.activeSelf)
        {
            return;
        }

        Vector3 from = m_rodTip.position;
        Vector3 to = m_bobber.position;
        Vector3 direction = to - from;
        float length = direction.magnitude;

        if (length < 0.001f)
        {
            m_line.localScale = Vector3.zero;
            return;
        }

        m_line.position = (from + to) * 0.5f;
        m_line.rotation = Quaternion.LookRotation(direction);

        // 親の大きさの影響を受けないように、ワールドでの大きさに合わせる
        Vector3 parentScale = m_line.parent != null ? m_line.parent.lossyScale : Vector3.one;
        m_line.localScale = new Vector3(
            m_lineWidth / Mathf.Max(0.0001f, parentScale.x),
            m_lineWidth / Mathf.Max(0.0001f, parentScale.y),
            length / Mathf.Max(0.0001f, parentScale.z));
    }
}
