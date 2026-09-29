using UnityEngine;
using UnityEngine.AI;

public class NPC_WANDER : MonoBehaviour
{
    [Header("ウェイポイント")]
    [SerializeField]
    private Transform[] m_wayPoints;

    [Header("移動設定")]
    [SerializeField]
    private float m_moveSpeed = 2f;

    [Header("待機時間")]
    [SerializeField]
    private float m_minWaitTime = 1f;

    [SerializeField]
    private float m_maxWaitTime = 4f;

    private NavMeshAgent m_navMeshAgent;

    private float m_waitTimer;
    private bool m_waitFlag;

    private int m_currentWayPointIndex = -1;

    void Start()
    {
        m_navMeshAgent = GetComponent<NavMeshAgent>();

        // 移動速度を設定
        m_navMeshAgent.speed = m_moveSpeed;

        MoveToNextWayPoint();
    }

    void Update()
    {
        // 待機中の場合
        if (m_waitFlag)
        {
            m_waitTimer -= Time.deltaTime;

            if (m_waitTimer <= 0f)
            {
                m_waitFlag = false;

                MoveToNextWayPoint();
            }

            return;
        }

        // 目的地に到着したか確認
        if (!m_navMeshAgent.pathPending &&
            m_navMeshAgent.remainingDistance <= m_navMeshAgent.stoppingDistance)
        {
            StartWaiting();
        }
    }

    void MoveToNextWayPoint()
    {
        // ウェイポイントが設定されていない場合
        if (m_wayPoints == null || m_wayPoints.Length == 0)
        {
            Debug.LogWarning("ウェイポイントが設定されていません。");
            return;
        }

        int nextWayPointIndex;

        // 前回と違うウェイポイントを選択
        do
        {
            nextWayPointIndex = Random.Range(0, m_wayPoints.Length);
        }
        while (m_wayPoints.Length > 1 &&
               nextWayPointIndex == m_currentWayPointIndex);

        m_currentWayPointIndex = nextWayPointIndex;

        Transform nextWayPoint = m_wayPoints[m_currentWayPointIndex];

        if (nextWayPoint == null)
        {
            return;
        }

        NavMeshHit navMeshHit;

        // ウェイポイント周辺のNavMeshを取得
        if (NavMesh.SamplePosition(
            nextWayPoint.position,
            out navMeshHit,
            2f,
            NavMesh.AllAreas))
        {
            m_navMeshAgent.SetDestination(navMeshHit.position);
        }
        else
        {
            Debug.LogWarning(
                "ウェイポイントがNavMesh上にありません: "
                + nextWayPoint.name);
        }
    }

    void StartWaiting()
    {
        m_waitFlag = true;

        m_waitTimer = Random.Range(
            m_minWaitTime,
            m_maxWaitTime);
    }
}
