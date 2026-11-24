using UnityEngine;

//==================================================
// ミニゲーム用カメラの動き（追いかける・ズーム・ゆれ）
//
// ・ミニゲームのカメラに付ける
// ・「追いかける相手」を入れると、その相手を後ろから追いかける（空欄なら置いた場所のまま）
// ・ZoomFov で画角（小さいほどズーム）を、Shake でゆれの強さを変えられる
//   （SHAKE_EFFECTS が振る速さに合わせて自動で変える）
//==================================================
[RequireComponent(typeof(Camera))]
public class MINIGAME_CAMERA : MonoBehaviour
{
    [Header("追いかける")]
    [Tooltip("追いかける相手（空欄なら置いた場所から動かない）")]
    [SerializeField]
    private Transform m_target;

    [Tooltip("相手から見たカメラの位置（相手の向きが基準。後ろなら z をマイナス）")]
    [SerializeField]
    private Vector3 m_offset = new Vector3(1.5f, 2.2f, -5f);

    [Tooltip("相手のどの高さを見るか（m）")]
    [SerializeField]
    private float m_lookHeight = 1.2f;

    [Tooltip("追いかける速さ。大きいほどぴったり付いてくる")]
    [SerializeField]
    private float m_followSmoothness = 6f;

    [Header("ズーム")]
    [Tooltip("ふだんの画角")]
    [SerializeField]
    private float m_defaultFov = 60f;

    [Tooltip("画角を変えるときの速さ")]
    [SerializeField]
    private float m_zoomSmoothness = 4f;

    [Header("ゆれ")]
    [Tooltip("ゆれの細かさ")]
    [SerializeField]
    private float m_shakeFrequency = 25f;

    private Camera m_camera;
    private Vector3 m_startLocalPosition;
    private Quaternion m_startLocalRotation;

    private Vector3 m_basePosition;
    private Quaternion m_baseRotation;

    private float m_targetFov;
    private float m_shake;
    private float m_impulse;

    public Transform Target
    {
        get { return m_target; }
        set { m_target = value; }
    }

    // 画角（小さいほどズーム）。0 以下でふだんの画角に戻す
    public float ZoomFov
    {
        get { return m_targetFov; }
        set { m_targetFov = value > 0f ? value : m_defaultFov; }
    }

    // ゆれの強さ（m）。ずっと続くゆれ
    public float Shake
    {
        get { return m_shake; }
        set { m_shake = Mathf.Max(0f, value); }
    }

    void Awake()
    {
        m_camera = GetComponent<Camera>();
        m_startLocalPosition = transform.localPosition;
        m_startLocalRotation = transform.localRotation;
        m_targetFov = m_defaultFov;
    }

    // カメラが切れた（ミニゲームが終わってフィールドに戻った）ときの通知
    public event System.Action Disabled;

    void OnEnable()
    {
        ResetView();
    }

    void OnDisable()
    {
        if (Disabled != null)
        {
            Disabled();
        }
    }

    // 1 回だけドンとゆらす（すぐ弱まる）
    public void Impulse(float strength)
    {
        m_impulse = Mathf.Max(m_impulse, strength);
    }

    // 最初の状態に戻す（ミニゲームの始め・終わりに呼ぶ）
    public void ResetView()
    {
        if (m_camera == null)
        {
            m_camera = GetComponent<Camera>();
        }

        m_shake = 0f;
        m_impulse = 0f;
        m_targetFov = m_defaultFov;
        m_camera.fieldOfView = m_defaultFov;

        if (m_target != null)
        {
            GetFollowPose(out m_basePosition, out m_baseRotation);
        }
        else
        {
            Transform parent = transform.parent;
            m_basePosition = parent != null ? parent.TransformPoint(m_startLocalPosition) : m_startLocalPosition;
            m_baseRotation = parent != null ? parent.rotation * m_startLocalRotation : m_startLocalRotation;
        }

        transform.SetPositionAndRotation(m_basePosition, m_baseRotation);
    }

    void LateUpdate()
    {
        float deltaTime = Time.deltaTime;

        // 追いかける
        if (m_target != null)
        {
            GetFollowPose(out Vector3 position, out Quaternion rotation);

            float t = 1f - Mathf.Exp(-deltaTime * m_followSmoothness);
            m_basePosition = Vector3.Lerp(m_basePosition, position, t);
            m_baseRotation = Quaternion.Slerp(m_baseRotation, rotation, t);
        }

        // ズーム
        m_camera.fieldOfView = Mathf.Lerp(
            m_camera.fieldOfView, m_targetFov, 1f - Mathf.Exp(-deltaTime * m_zoomSmoothness));

        // ゆれ
        m_impulse = Mathf.MoveTowards(m_impulse, 0f, deltaTime * 2f);

        float amount = m_shake + m_impulse;
        Vector3 offset = Vector3.zero;

        if (amount > 0f)
        {
            float time = Time.time * m_shakeFrequency;
            offset = new Vector3(
                Mathf.PerlinNoise(time, 0.1f) - 0.5f,
                Mathf.PerlinNoise(0.7f, time) - 0.5f,
                0f) * (amount * 2f);
        }

        transform.SetPositionAndRotation(m_basePosition + m_baseRotation * offset, m_baseRotation);
    }

    // ミニゲームが始まったときにカメラが写す位置と向き（クエストのパネルの映像などに使う）
    public void GetViewPose(out Vector3 position, out Quaternion rotation)
    {
        if (m_target != null)
        {
            GetFollowPose(out position, out rotation);
            return;
        }

        Transform parent = transform.parent;
        position = parent != null ? parent.TransformPoint(m_startLocalPosition) : m_startLocalPosition;
        rotation = parent != null ? parent.rotation * m_startLocalRotation : m_startLocalRotation;
    }

    // ふだんの画角
    public float DefaultFov
    {
        get { return m_defaultFov; }
    }

    void GetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        Quaternion yaw = Quaternion.Euler(0f, m_target.eulerAngles.y, 0f);
        Vector3 look = m_target.position + Vector3.up * m_lookHeight;

        position = m_target.position + yaw * m_offset;
        rotation = Quaternion.LookRotation(look - position, Vector3.up);
    }
}
