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

    // 消えたときにスクリプトから受け取るためのイベント（NPC_SPAWNER が使う）
    public event System.Action<NPC_VANISH> Vanished;

    private NPC_FADER m_fader;

    private VANISH_STATE m_currentState;

    // FADE_WHILE_WALKING 用
    private float m_totalDistance = -1f;        // 道のり全体の長さ
    private bool m_isFadingWhileWalking;        // 薄くなり始めたか
    private float m_fadeStartRemaining;         // 薄くなり始めた地点での残り距離

    // 経路の曲がり角を受け取るバッファ（毎フレーム new しないよう使い回す）
    private static readonly Vector3[] s_pathCorners = new Vector3[64];

    protected override void Awake()
    {
        base.Awake();

        m_fader =
            GetComponent<NPC_FADER>();
    }

    //==================================================
    // 目的地をスクリプトから設定する
    // （プレハブからシーンの Transform は参照できないので、
    //   NPC_SPAWNER が生成直後に呼ぶ。Start より前に呼ぶこと）
    //==================================================

    public void SetDestination(Transform destination)
    {
        m_destination = destination;
    }

    void Start()
    {
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

    //==================================================
    // 歩きながら消える
    //
    // 1. 道のりの m_fadeStartProgress 割まで進んだら「薄くなり始める地点」とする
    // 2. その地点での残り距離を覚えておく
    // 3. 透明度 = 今の残り距離 ÷ 覚えた残り距離
    //    → 薄くなり始める地点でちょうど 1、目的地でちょうど 0 になる
    //==================================================

    void UpdateFadeWhileWalking()
    {
        // 経路の計算が終わるまでは待つ
        if (m_navMeshAgent.pathPending ||
            !m_navMeshAgent.hasPath)
        {
            return;
        }

        float remainingDistance =
            GetRemainingPathDistance();

        // 最初に分かった残り距離を「道のり全体の長さ」にする
        if (m_totalDistance < 0f)
        {
            m_totalDistance = remainingDistance;
        }

        if (!m_isFadingWhileWalking)
        {
            float progress =
                m_totalDistance > 0.01f
                    ? 1f - (remainingDistance / m_totalDistance)
                    : 1f;

            // まだ薄くなり始める地点に来ていない
            if (progress < m_fadeStartProgress)
            {
                return;
            }

            // ここから薄くなり始める
            m_isFadingWhileWalking = true;

            m_fadeStartRemaining =
                Mathf.Max(remainingDistance, 0.01f);
        }

        float alpha =
            Mathf.Clamp01(remainingDistance / m_fadeStartRemaining);

        // 遠回りで一時的に離れても、濃く戻らないようにする
        alpha =
            Mathf.Min(alpha, m_fader.Alpha);

        m_fader.SetAlpha(alpha);
    }

    // 経路に沿った目的地までの残り距離（水平距離）
    // NavMeshAgent.remainingDistance は状況によって Infinity になり、
    // 直線距離で代用すると値が飛ぶので、経路の曲がり角から自分で計算する
    float GetRemainingPathDistance()
    {
        int cornerCount =
            m_navMeshAgent.path.GetCornersNonAlloc(s_pathCorners);

        Vector3 position = transform.position;

        // 曲がり角が取れない場合は目的地までの直線距離
        if (cornerCount < 2)
        {
            return FlatDistance(
                position,
                m_navMeshAgent.destination);
        }

        // 次に向かっている曲がり角を探す
        Vector3 steeringTarget =
            m_navMeshAgent.steeringTarget;

        int nextCorner = cornerCount - 1;
        float closest = Mathf.Infinity;

        for (int i = 1; i < cornerCount; i++)
        {
            float distance =
                FlatDistance(s_pathCorners[i], steeringTarget);

            if (distance < closest)
            {
                closest = distance;
                nextCorner = i;
            }
        }

        // 今の位置 → 次の曲がり角 → … → 目的地
        float total =
            FlatDistance(position, s_pathCorners[nextCorner]);

        for (int i = nextCorner; i < cornerCount - 1; i++)
        {
            total += FlatDistance(
                s_pathCorners[i],
                s_pathCorners[i + 1]);
        }

        return total;
    }

    // 高さを無視した距離（上下の揺れで値がぶれないように）
    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
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

        if (Vanished != null)
        {
            Vanished(this);
        }

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
