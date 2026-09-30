using UnityEngine;
using UnityEngine.AI;

public class NPC_WANDER : MonoBehaviour
{
    [Header("ウェイポイント")]
    [SerializeField]
    private Transform[] m_wayPoints;

    [SerializeField]
    private float m_wayPointSearchDistance = 8f;

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

        MoveToNextWayPoint();
    }

    void Update()
    {
        // 待機中
        if (m_waitFlag)
        {
            m_waitTimer -= Time.deltaTime;

            if (m_waitTimer <= 0f)
            {
                m_waitFlag = false;

                m_navMeshAgent.isStopped = false;

                MoveToNextWayPoint();
            }

            return;
        }

        // Waypointに到着したか確認
        if (!m_navMeshAgent.pathPending &&
            m_navMeshAgent.remainingDistance <=
            m_navMeshAgent.stoppingDistance)
        {
            StartWaiting();
        }
    }

    void MoveToNextWayPoint()
    {
        if (m_wayPoints == null ||
            m_wayPoints.Length == 0)
        {
            Debug.LogWarning(
                "ウェイポイントが設定されていません。");

            return;
        }

        int nextWayPointIndex =
            FindNextWayPoint();

        if (nextWayPointIndex == -1)
        {
            Debug.LogWarning(
                "移動可能なウェイポイントが見つかりません。");

            return;
        }

        m_currentWayPointIndex =
            nextWayPointIndex;

        Transform nextWayPoint =
            m_wayPoints[
                m_currentWayPointIndex];

        NavMeshHit navMeshHit;

        if (NavMesh.SamplePosition(
            nextWayPoint.position,
            out navMeshHit,
            2f,
            NavMesh.AllAreas))
        {
            m_navMeshAgent.SetDestination(
                navMeshHit.position);
        }
        else
        {
            Debug.LogWarning(
                "ウェイポイントがNavMesh上にありません: "
                + nextWayPoint.name);
        }
    }

    int FindNextWayPoint()
    {
        int[] candidateWayPoints =
            new int[m_wayPoints.Length];

        int candidateCount = 0;

        for (int i = 0; i < m_wayPoints.Length; i++)
        {
            if (m_wayPoints[i] == null)
            {
                continue;
            }

            // 現在のWaypointは除外
            if (i == m_currentWayPointIndex)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    m_wayPoints[i].position);

            if (distance <= m_wayPointSearchDistance)
            {
                candidateWayPoints[candidateCount] =
                    i;

                candidateCount++;
            }
        }

        if (candidateCount == 0)
        {
            return -1;
        }

        // 候補の中からランダムに選択
        int randomIndex =
            Random.Range(
                0,
                candidateCount);

        return candidateWayPoints[randomIndex];
    }

    void StartWaiting()
    {
        m_waitFlag = true;

        m_waitTimer =
            Random.Range(
                m_minWaitTime,
                m_maxWaitTime);

        m_navMeshAgent.isStopped = true;
    }
}
