using UnityEngine;

//==================================================
// プレイヤーを斜め上から追いかけるカメラ（検証用の簡易版）
//
// ・カメラに付けて、m_target に追いかける相手を入れる
// ・向きは固定（ワールドの奥 = 画面の上）。W キーで画面の奥へ進む操作と合う
// ・開始時はいきなり目標位置に置き、その後は滑らかに追いかける
//==================================================
public class CAMERA_FOLLOW : MonoBehaviour
{
    [Tooltip("追いかける相手")]
    [SerializeField]
    private Transform m_target;

    [Tooltip("相手から見たカメラの位置（上に 12m、後ろに 10m など）")]
    [SerializeField]
    private Vector3 m_offset = new Vector3(0f, 12f, -10f);

    [Tooltip("相手のどの高さを見るか")]
    [SerializeField]
    private float m_lookHeight = 1f;

    [Tooltip("追いかける速さ。大きいほどぴったり付いてくる")]
    [SerializeField]
    private float m_followSmoothness = 8f;

    public Transform Target
    {
        get { return m_target; }
        set { m_target = value; }
    }

    void Start()
    {
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (m_target == null)
        {
            return;
        }

        Vector3 desiredPosition =
            m_target.position + m_offset;

        transform.position =
            Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-m_followSmoothness * Time.deltaTime));

        LookAtTarget();
    }

    // すぐに目標の位置へ移動する（開始時やワープ後に使う）
    public void SnapToTarget()
    {
        if (m_target == null)
        {
            return;
        }

        transform.position =
            m_target.position + m_offset;

        LookAtTarget();
    }

    void LookAtTarget()
    {
        Vector3 lookPoint =
            m_target.position + Vector3.up * m_lookHeight;

        Vector3 direction =
            lookPoint - transform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction);
        }
    }

    void OnValidate()
    {
        m_followSmoothness = Mathf.Max(0.1f, m_followSmoothness);
    }
}
