using UnityEngine;
using UnityEngine.AI;

public class NPC_WANDER : MonoBehaviour
{
    [Header("移動範囲")]
    public Transform m_wanderArea;
    public float m_wanderRadius = 10f;

    [Header("待機時間")]
    public float m_minWaitTime = 1f;
    public float m_maxWaitTime = 4f;

    private NavMeshAgent m_navMeshAgent;

    private float m_waitTimer;
    private bool m_waitFlag;

    void Start()
    {
        m_navMeshAgent = GetComponent<NavMeshAgent>();

        SetNewDestination();
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

                SetNewDestination();
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

    void SetNewDestination()
    {
        Vector2 randomCircle = Random.insideUnitCircle * m_wanderRadius;

        Vector3 randomPosition =
            m_wanderArea.position +
            new Vector3(randomCircle.x, 0f, randomCircle.y);

        NavMeshHit navMeshHit;

        if (NavMesh.SamplePosition(
            randomPosition,
            out navMeshHit,
            3f,
            NavMesh.AllAreas))
        {
            m_navMeshAgent.SetDestination(navMeshHit.position);
        }
        else
        {
            SetNewDestination();
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
