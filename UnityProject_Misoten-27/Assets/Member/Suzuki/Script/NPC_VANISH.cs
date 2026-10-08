using UnityEngine;
using UnityEngine.Events;

//==================================================
// 目的地へ歩いていき、フェードアウトして消える NPC
//
// 消え方は Inspector の m_fadeMode で選ぶ
//   FADE_ON_ARRIVAL    : 目的地に着いてから、その場でフェードアウト
//   FADE_WHILE_WALKING : 歩きながら少しずつ薄くなり、着いたときに消える
//==================================================
[RequireComponent(typeof(NPC_FADER))]
public class NPC_VANISH : NPC_BASE
{
    public enum FADE_MODE
    {
        FADE_ON_ARRIVAL,
        FADE_WHILE_WALKING
    }

    enum VANISH_STATE
    {
        WALK,       // 目的地へ移動中
        FADE,       // その場でフェードアウト中
        DONE        // 消えた
    }

    [Header("目的地")]
    [SerializeField]
    private Transform m_destination;

    [Header("消え方")]
    [SerializeField]
    private FADE_MODE m_fadeMode = FADE_MODE.FADE_ON_ARRIVAL;

    [Tooltip("その場でフェードアウトするときにかかる時間（秒）")]
    [SerializeField]
    private float m_fadeDuration = 1.5f;

    [Tooltip("FADE_WHILE_WALKING のとき、道のりの何割まで進んだら薄くなり始めるか")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_fadeStartProgress = 0f;

    [Header("消えた後")]
    [Tooltip("オン：オブジェクトを削除する / オフ：非表示にするだけ")]
    [SerializeField]
    private bool m_destroyOnVanish = true;

    [Tooltip("消えたときに呼ばれる（スポーン管理などに使う）")]
    [SerializeField]
    private UnityEvent m_onVanished = new UnityEvent();

    private NPC_FADER m_fader;

    private VANISH_STATE m_currentState;

    // FADE_WHILE_WALKING 用
    private Vector3 m_startPosition;
    private float m_totalDistance = -1f;
    private float m_progress;

    protected override void Awake()
    {
        base.Awake();

        m_fader =
            GetComponent<NPC_FADER>();
    }

    void Start()
    {
        m_startPosition = transform.position;

        if (m_destination == null)
        {
            Debug.LogWarning(
                "目的地が設定されていません: " + name);

            StartFade();
            return;
        }

        // 歩き出せなかったら、その場で消える
        if (!StartMove(m_destination.position))
        {
            StartFade();
            return;
        }

        m_currentState = VANISH_STATE.WALK;
    }

    protected override void OnUpdate()
    {
        switch (m_currentState)
        {
            case VANISH_STATE.WALK:
                UpdateWalk();
                break;

            case VANISH_STATE.FADE:
                UpdateFade();
                break;

            case VANISH_STATE.DONE:
                break;
        }
    }

    //==================================================
    // WALK
    //==================================================

    void UpdateWalk()
    {
        MOVE_STATUS status = UpdateMove();

        if (m_fadeMode == FADE_MODE.FADE_WHILE_WALKING)
        {
            UpdateFadeWhileWalking();
        }

        switch (status)
        {
            case MOVE_STATUS.ARRIVED:
                if (m_fadeMode == FADE_MODE.FADE_WHILE_WALKING)
                {
                    // 歩きながら消えるタイプは、着いた時点で消える
                    Vanish();
                }
                else
                {
                    StartFade();
                }
                break;

            case MOVE_STATUS.FAILED:
                // たどり着けなかった場合は、その場で（残りを）フェードアウト
                StartFade();
                break;
        }
    }

    // 進んだ割合に合わせて透明度を下げる
    void UpdateFadeWhileWalking()
    {
        // 経路の計算が終わるまでは待つ
        if (m_navMeshAgent.pathPending)
        {
            return;
        }

        float remainingDistance =
            m_navMeshAgent.remainingDistance;

        // 経路の残り距離が取れない場合は直線距離で代用
        if (float.IsInfinity(remainingDistance))
        {
            remainingDistance =
                Vector3.Distance(
                    transform.position,
                    m_navMeshAgent.destination);
        }

        // 最初に分かった残り距離を「全体の距離」にする
        if (m_totalDistance < 0f)
        {
            m_totalDistance =
                Mathf.Max(
                    remainingDistance,
                    Vector3.Distance(
                        m_startPosition,
                        m_navMeshAgent.destination));
        }

        if (m_totalDistance <= 0.01f)
        {
            return;
        }

        float progress =
            1f - (remainingDistance / m_totalDistance);

        // 遠回りで一時的に離れても、濃く戻らないようにする
        m_progress =
            Mathf.Max(
                m_progress,
                Mathf.Clamp01(progress));

        float fadeRate =
            Mathf.InverseLerp(
                m_fadeStartProgress,
                1f,
                m_progress);

        m_fader.SetAlpha(1f - fadeRate);
    }

    //==================================================
    // FADE（その場でフェードアウト）
    //==================================================

    void StartFade()
    {
        m_currentState = VANISH_STATE.FADE;

        StopAgent();
    }

    void UpdateFade()
    {
        // 今の透明度から 0 に向かって下げる
        // （歩きながら薄くなっていた場合も続きから）
        float alpha =
            m_fadeDuration > 0f
                ? m_fader.Alpha - Time.deltaTime / m_fadeDuration
                : 0f;

        m_fader.SetAlpha(alpha);

        if (alpha <= 0f)
        {
            Vanish();
        }
    }

    //==================================================
    // 消える
    //==================================================

    void Vanish()
    {
        m_currentState = VANISH_STATE.DONE;

        m_fader.SetAlpha(0f);

        m_onVanished.Invoke();

        if (m_destroyOnVanish)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    //==================================================
    // Inspector の入力ミス対策
    //==================================================

    protected override void OnValidate()
    {
        base.OnValidate();

        m_fadeDuration = Mathf.Max(0f, m_fadeDuration);
    }

    //==================================================
    // デバッグ表示：目的地を表示（NPC を選択したときだけ）
    //==================================================

    void OnDrawGizmosSelected()
    {
        if (m_destination == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;

        Gizmos.DrawLine(
            transform.position,
            m_destination.position);

        Gizmos.DrawWireSphere(
            m_destination.position,
            0.5f);
    }
}
