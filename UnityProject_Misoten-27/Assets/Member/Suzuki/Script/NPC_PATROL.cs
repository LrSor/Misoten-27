using UnityEngine;

//==================================================
// 決められた順番でウェイポイントを巡回する NPC
// Inspector の m_wayPoints の上から順に歩く
// 最後まで行ったら、m_loop がオンなら最初に戻って続ける
//==================================================
public class NPC_PATROL : NPC_BASE
{
    enum PATROL_STATE
    {
        WALK,       // 次のウェイポイントへ移動中
        WAIT,       // ウェイポイントで立ち止まり中
        FINISHED    // 巡回終了（m_loop がオフのとき）
    }

    [Header("巡回")]
    [Tooltip("この順番に巡回する")]
    [SerializeField]
    private Transform[] m_wayPoints;

    [Tooltip("最後のウェイポイントまで行ったら最初に戻って巡回を続ける")]
    [SerializeField]
    private bool m_loop = true;

    [Header("ウェイポイントで立ち止まる時間")]
    [SerializeField]
    private float m_minWaitTime = 0.5f;

    [SerializeField]
    private float m_maxWaitTime = 1.5f;

    private PATROL_STATE m_currentState;

    private float m_waitTimer;

    // 今向かっている（最後に向かった）ウェイポイントの番号
    private int m_currentWayPointIndex = -1;

    void Start()
    {
        if (m_wayPoints == null ||
            m_wayPoints.Length == 0)
        {
            Debug.LogWarning(
                "巡回するウェイポイントが設定されていません: " + name);

            Finish();
            return;
        }

        MoveToNextWayPoint();
    }

    protected override void OnUpdate()
    {
        switch (m_currentState)
        {
            case PATROL_STATE.WALK:
                UpdateWalk();
                break;

            case PATROL_STATE.WAIT:
                UpdateWait();
                break;

            case PATROL_STATE.FINISHED:
                break;
        }
    }

    //==================================================
    // WALK
    //==================================================

    void MoveToNextWayPoint()
    {
        int nextIndex =
            m_currentWayPointIndex + 1;

        // 最後まで行った
        if (nextIndex >= m_wayPoints.Length)
        {
            if (!m_loop)
            {
                Finish();
                return;
            }

            nextIndex = 0;
        }

        m_currentWayPointIndex = nextIndex;

        Transform nextWayPoint =
            m_wayPoints[m_currentWayPointIndex];

        // 空欄、または NavMesh 外のウェイポイントは飛ばす
        // （少し立ち止まってから次へ。毎フレーム処理が回り続けないように）
        if (nextWayPoint == null ||
            !StartMove(nextWayPoint.position))
        {
            StartWait();
            return;
        }

        m_currentState = PATROL_STATE.WALK;
    }

    void UpdateWalk()
    {
        // 到着しても、詰まって諦めても、少し立ち止まって次へ
        if (UpdateMove() != MOVE_STATUS.MOVING)
        {
            StartWait();
        }
    }

    //==================================================
    // WAIT
    //==================================================

    void StartWait()
    {
        m_currentState = PATROL_STATE.WAIT;

        m_waitTimer =
            Random.Range(
                m_minWaitTime,
                m_maxWaitTime);

        StopAgent();
    }

    void UpdateWait()
    {
        m_waitTimer -= Time.deltaTime;

        if (m_waitTimer <= 0f)
        {
            MoveToNextWayPoint();
        }
    }

    //==================================================
    // 巡回終了
    //==================================================

    void Finish()
    {
        m_currentState = PATROL_STATE.FINISHED;

        StopAgent();
    }

    //==================================================
    // Inspector の入力ミス対策
    //==================================================

    protected override void OnValidate()
    {
        base.OnValidate();

        m_minWaitTime = Mathf.Max(0f, m_minWaitTime);
        m_maxWaitTime = Mathf.Max(m_minWaitTime, m_maxWaitTime);
    }

    //==================================================
    // デバッグ表示：巡回ルートを線で表示（NPC を選択したときだけ）
    //==================================================

    void OnDrawGizmosSelected()
    {
        if (m_wayPoints == null ||
            m_wayPoints.Length == 0)
        {
            return;
        }

        Gizmos.color = Color.green;

        Transform first = null;
        Transform previous = null;

        foreach (Transform wayPoint in m_wayPoints)
        {
            if (wayPoint == null)
            {
                continue;
            }

            if (first == null)
            {
                first = wayPoint;
            }

            if (previous != null)
            {
                Gizmos.DrawLine(
                    previous.position,
                    wayPoint.position);
            }

            Gizmos.DrawWireSphere(
                wayPoint.position,
                0.3f);

            previous = wayPoint;
        }

        // ループする場合は最後 → 最初も線でつなぐ
        if (m_loop &&
            first != null &&
            previous != null &&
            first != previous)
        {
            Gizmos.DrawLine(
                previous.position,
                first.position);
        }

        // 今向かっているウェイポイント
        if (m_currentWayPointIndex >= 0 &&
            m_currentWayPointIndex < m_wayPoints.Length &&
            m_wayPoints[m_currentWayPointIndex] != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                transform.position,
                m_wayPoints[m_currentWayPointIndex].position);
        }
    }
}
