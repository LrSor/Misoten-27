using UnityEngine;
using UnityEngine.AI;

public class NPC_WANDER : MonoBehaviour
{
    enum NPC_ACTION
    {
        WALK,
        WAIT,
        LOOK_AROUND,
        REST
    }

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

    [Header("周囲を見る")]
    [SerializeField]
    private float m_lookAroundTime = 3f;

    [SerializeField]
    private float m_lookAroundAngle = 90f;

    [Header("長めに休む")]
    [SerializeField]
    private float m_minRestTime = 5f;

    [SerializeField]
    private float m_maxRestTime = 10f;

    private NavMeshAgent m_navMeshAgent;

    private NPC_ACTION m_currentAction;

    private float m_actionTimer;

    private int m_currentWayPointIndex = -1;

    private float m_lookStartAngle;
    private float m_lookTargetAngle;

    void Start()
    {
        m_navMeshAgent =
            GetComponent<NavMeshAgent>();

        StartWalk();
    }

    void Update()
    {
        switch (m_currentAction)
        {
            case NPC_ACTION.WALK:
                UpdateWalk();
                break;

            case NPC_ACTION.WAIT:
                UpdateWait();
                break;

            case NPC_ACTION.LOOK_AROUND:
                UpdateLookAround();
                break;

            case NPC_ACTION.REST:
                UpdateRest();
                break;
        }
    }

    //==================================================
    // WALK
    //==================================================

    void StartWalk()
    {
        m_currentAction = NPC_ACTION.WALK;

        m_navMeshAgent.isStopped = false;

        MoveToNextWayPoint();
    }

    void UpdateWalk()
    {
        if (m_navMeshAgent.pathPending)
        {
            return;
        }

        if (m_navMeshAgent.remainingDistance <=
            m_navMeshAgent.stoppingDistance)
        {
            StartNextAction();
        }
    }

    //==================================================
    // WAIT
    //==================================================

    void StartWait()
    {
        m_currentAction = NPC_ACTION.WAIT;

        m_actionTimer =
            Random.Range(
                m_minWaitTime,
                m_maxWaitTime);

        m_navMeshAgent.isStopped = true;
    }

    void UpdateWait()
    {
        m_actionTimer -= Time.deltaTime;

        if (m_actionTimer <= 0f)
        {
            StartNextAction();
        }
    }

    //==================================================
    // LOOK AROUND
    //==================================================

    void StartLookAround()
    {
        m_currentAction =
            NPC_ACTION.LOOK_AROUND;

        m_actionTimer =
            m_lookAroundTime;

        m_navMeshAgent.isStopped = true;

        m_lookStartAngle =
            transform.eulerAngles.y;

        m_lookTargetAngle =
            m_lookStartAngle +
            Random.Range(
                -m_lookAroundAngle,
                m_lookAroundAngle);
    }

    void UpdateLookAround()
    {
        m_actionTimer -= Time.deltaTime;

        float lookProgress =
            1f -
            (m_actionTimer /
            m_lookAroundTime);

        float currentAngle =
            Mathf.LerpAngle(
                m_lookStartAngle,
                m_lookTargetAngle,
                lookProgress);

        transform.rotation =
            Quaternion.Euler(
                0f,
                currentAngle,
                0f);

        if (m_actionTimer <= 0f)
        {
            StartNextAction();
        }
    }

    //==================================================
    // REST
    //==================================================

    void StartRest()
    {
        m_currentAction =
            NPC_ACTION.REST;

        m_actionTimer =
            Random.Range(
                m_minRestTime,
                m_maxRestTime);

        m_navMeshAgent.isStopped = true;
    }

    void UpdateRest()
    {
        m_actionTimer -= Time.deltaTime;

        if (m_actionTimer <= 0f)
        {
            StartNextAction();
        }
    }

    //==================================================
    // 次の行動を決める
    //==================================================

    void StartNextAction()
    {
        int actionIndex =
            Random.Range(
                0,
                4);

        switch (actionIndex)
        {
            case 0:
                StartWalk();
                break;

            case 1:
                StartWait();
                break;

            case 2:
                StartLookAround();
                break;

            case 3:
                StartRest();
                break;
        }
    }

    //==================================================
    // Waypointへ移動
    //==================================================

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

    //==================================================
    // 次のWaypointを探す
    //==================================================

    int FindNextWayPoint()
    {
        int[] candidateWayPoints =
            new int[m_wayPoints.Length];

        int candidateCount = 0;

        float closestDistance =
            Mathf.Infinity;

        int closestWayPointIndex = -1;

        for (int i = 0;
            i < m_wayPoints.Length;
            i++)
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

            // 一番近いWaypointを記録
            if (distance < closestDistance)
            {
                closestDistance = distance;

                closestWayPointIndex = i;
            }

            // 検索範囲内なら候補に追加
            if (distance <= m_wayPointSearchDistance)
            {
                candidateWayPoints[
                    candidateCount] = i;

                candidateCount++;
            }
        }

        // 近くに候補がある場合
        if (candidateCount > 0)
        {
            int randomIndex =
                Random.Range(
                    0,
                    candidateCount);

            return candidateWayPoints[
                randomIndex];
        }

        // 近くに候補がなければ
        // 一番近いWaypointを使用
        return closestWayPointIndex;
    }
}
