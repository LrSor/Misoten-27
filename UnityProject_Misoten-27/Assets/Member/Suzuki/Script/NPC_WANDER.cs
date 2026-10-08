using System.Collections.Generic;
using UnityEngine;

//==================================================
// ランダムに歩き回る NPC
// 近くのウェイポイントへ歩く / 待つ / 見回す / 休む をランダムに繰り返す
// ウェイポイントは予約制で、他の NPC と同じ場所には向かわない
//==================================================
public class NPC_WANDER : NPC_BASE
{
    enum NPC_ACTION
    {
        WALK,
        WAIT,
        LOOK_AROUND,
        REST
    }

    // NPC_ACTION の数（StartNextAction の抽選用）
    const int ACTION_COUNT = 4;

    //==================================================
    // ウェイポイント予約（全 NPC_WANDER で共有）
    // key: ウェイポイント / value: 予約している NPC
    //==================================================

    private static readonly Dictionary<Transform, NPC_WANDER> s_reservations =
        new Dictionary<Transform, NPC_WANDER>();

    // ドメインリロード無しで再生した場合に前回の予約が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetReservations()
    {
        s_reservations.Clear();
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

    private NPC_ACTION m_currentAction;

    private float m_actionTimer;

    private int m_currentWayPointIndex = -1;

    // 自分が予約中のウェイポイント
    private Transform m_reservedWayPoint;

    private float m_lookStartAngle;
    private float m_lookTargetAngle;

    // 候補ウェイポイント（毎回 new しないよう使い回す）
    private readonly List<int> m_candidateWayPoints =
        new List<int>();

    void Start()
    {
        StartWalk();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        ReleaseWayPoint();
    }

    protected override void OnUpdate()
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
        // 行き先が決まらなかったら歩かずに待機する
        // （毎フレーム警告が出続けるのを防ぐ）
        if (!MoveToNextWayPoint())
        {
            StartWait();
            return;
        }

        m_currentAction = NPC_ACTION.WALK;
    }

    void UpdateWalk()
    {
        switch (UpdateMove())
        {
            case MOVE_STATUS.ARRIVED:
                // 到着後もそのウェイポイントにいる間は予約を持ち続ける
                StartNextAction();
                break;

            case MOVE_STATUS.FAILED:
                // たどり着けなかった場所は他の NPC に譲る
                ReleaseWayPoint();
                StartNextAction();
                break;
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

        StopAgent();
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

        StopAgent();

        // Agent の自動回転と競合しないよう一時的に止める
        // （次に歩き始めるときに StartMove で元に戻る）
        m_navMeshAgent.updateRotation = false;

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

        // m_lookAroundTime が 0 のときの 0 除算を防ぐ
        float lookProgress =
            m_lookAroundTime > 0f
                ? 1f - (m_actionTimer / m_lookAroundTime)
                : 1f;

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

        StopAgent();
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
                ACTION_COUNT);

        switch ((NPC_ACTION)actionIndex)
        {
            case NPC_ACTION.WALK:
                StartWalk();
                break;

            case NPC_ACTION.WAIT:
                StartWait();
                break;

            case NPC_ACTION.LOOK_AROUND:
                StartLookAround();
                break;

            case NPC_ACTION.REST:
                StartRest();
                break;
        }
    }

    //==================================================
    // ウェイポイントの予約
    //==================================================

    bool IsReservedByOther(Transform wayPoint)
    {
        NPC_WANDER owner;

        if (!s_reservations.TryGetValue(wayPoint, out owner))
        {
            return false;
        }

        // 予約者が破棄済みなら空いている扱い
        if (owner == null)
        {
            s_reservations.Remove(wayPoint);
            return false;
        }

        return owner != this;
    }

    void ReserveWayPoint(Transform wayPoint)
    {
        ReleaseWayPoint();

        s_reservations[wayPoint] = this;

        m_reservedWayPoint = wayPoint;
    }

    void ReleaseWayPoint()
    {
        if (m_reservedWayPoint == null)
        {
            return;
        }

        NPC_WANDER owner;

        if (s_reservations.TryGetValue(m_reservedWayPoint, out owner) &&
            owner == this)
        {
            s_reservations.Remove(m_reservedWayPoint);
        }

        m_reservedWayPoint = null;
    }

    //==================================================
    // Waypointへ移動
    // 目的地を設定できたら true
    //==================================================

    bool MoveToNextWayPoint()
    {
        if (m_wayPoints == null ||
            m_wayPoints.Length == 0)
        {
            Debug.LogWarning(
                "ウェイポイントが設定されていません: " + name);

            return false;
        }

        int nextWayPointIndex =
            FindNextWayPoint();

        // 空いているウェイポイントが無い
        // （他の NPC が全部予約中など。少し待てば空くので警告は出さない）
        if (nextWayPointIndex == -1)
        {
            return false;
        }

        Transform nextWayPoint =
            m_wayPoints[nextWayPointIndex];

        if (!StartMove(nextWayPoint.position))
        {
            return false;
        }

        // 新しい行き先を予約（前の場所の予約はここで解除される）
        m_currentWayPointIndex =
            nextWayPointIndex;

        ReserveWayPoint(nextWayPoint);

        return true;
    }

    //==================================================
    // 次のWaypointを探す
    // 他の NPC が予約中のウェイポイントは選ばない
    //==================================================

    int FindNextWayPoint()
    {
        m_candidateWayPoints.Clear();

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

            // 他の NPC が向かっている / 立っている場所は除外
            if (IsReservedByOther(m_wayPoints[i]))
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
                m_candidateWayPoints.Add(i);
            }
        }

        // 近くに候補がある場合
        if (m_candidateWayPoints.Count > 0)
        {
            int randomIndex =
                Random.Range(
                    0,
                    m_candidateWayPoints.Count);

            return m_candidateWayPoints[
                randomIndex];
        }

        // 近くに候補がなければ
        // 一番近いWaypointを使用
        return closestWayPointIndex;
    }

    //==================================================
    // Inspector の入力ミス対策
    //==================================================

    protected override void OnValidate()
    {
        base.OnValidate();

        m_wayPointSearchDistance = Mathf.Max(0f, m_wayPointSearchDistance);
        m_lookAroundTime = Mathf.Max(0f, m_lookAroundTime);

        m_minWaitTime = Mathf.Max(0f, m_minWaitTime);
        m_maxWaitTime = Mathf.Max(m_minWaitTime, m_maxWaitTime);

        m_minRestTime = Mathf.Max(0f, m_minRestTime);
        m_maxRestTime = Mathf.Max(m_minRestTime, m_maxRestTime);
    }

    //==================================================
    // デバッグ表示（NPC を選択したときだけ）
    //==================================================

    void OnDrawGizmosSelected()
    {
        // ウェイポイント検索範囲
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            m_wayPointSearchDistance);

        // 現在の目的地
        if (m_wayPoints != null &&
            m_currentWayPointIndex >= 0 &&
            m_currentWayPointIndex < m_wayPoints.Length &&
            m_wayPoints[m_currentWayPointIndex] != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                transform.position,
                m_wayPoints[m_currentWayPointIndex].position);
        }

        // 予約中のウェイポイント（再生中のみ）
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;

            foreach (KeyValuePair<Transform, NPC_WANDER> pair in s_reservations)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(
                    pair.Key.position,
                    0.5f);
            }
        }
    }
}
