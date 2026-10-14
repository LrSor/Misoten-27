using UnityEngine;
using UnityEngine.AI;

//==================================================
// 全 NPC 共通の土台クラス
//
// ・歩く速度（NPC ごとにランダム）
// ・目的地への移動 / 到着判定 / 詰まったときのタイムアウト
// ・その場で止める
// ・歩くときの上下の揺れ
//
// NPC の種類ごとの「どう動くか」は、このクラスを継承して
// OnUpdate() に書く（Update() は派生クラスで定義しないこと）
//==================================================
[RequireComponent(typeof(NavMeshAgent))]
public abstract class NPC_BASE : MonoBehaviour
{
    // 移動の状態（UpdateMove の戻り値）
    protected enum MOVE_STATUS
    {
        MOVING,     // 移動中
        ARRIVED,    // 到着した
        FAILED      // 経路が無い / 詰まって進めない
    }

    [Header("移動（共通）")]
    [Tooltip("目的地付近の NavMesh を探す半径")]
    [SerializeField]
    protected float m_navMeshSampleDistance = 2f;

    [Tooltip("この秒数のあいだ、ほとんど前に進めなかったら「詰まった」とみなして諦める")]
    [SerializeField]
    protected float m_stuckTime = 3f;

    // この距離 (m) 以上動いたら「進んでいる」とみなす
    const float STUCK_MOVE_DISTANCE = 0.5f;

    [Header("歩く速度（NPC ごとにこの範囲でランダム）")]
    [Tooltip("NavMeshAgent の Speed はこの値で上書きされます")]
    [SerializeField]
    private float m_minWalkSpeed = 2.6f;

    [SerializeField]
    private float m_maxWalkSpeed = 3.4f;

    [Header("歩くときの上下の揺れ")]
    [Tooltip("上下に揺れる高さ (m)")]
    [SerializeField]
    private float m_bobHeight = 0.05f;

    [Tooltip("1m 進むごとの歩数。速く歩くほど揺れも速くなる")]
    [SerializeField]
    private float m_stepsPerMeter = 0.9f;

    [Header("曲がり方")]
    [Tooltip("向きを変える速さ。小さいほど曲がり角でゆっくり大きく曲がる")]
    [SerializeField]
    private float m_turnSmoothness = 6f;

    [Header("すれ違い（よけ合い）の優先度")]
    [Tooltip("NPC ごとにこの範囲でランダム。数字が小さいほど優先され、大きい NPC が道を譲る（0 ～ 99）")]
    [SerializeField]
    private int m_minAvoidancePriority = 30;

    [SerializeField]
    private int m_maxAvoidancePriority = 70;

    // 揺れの高さを目標値に近づける速さ
    const float BOB_SMOOTHING = 15f;

    // この速さ (m/s) 未満のときは向きを変えない（止まりかけでクルッと回らないように）
    const float TURN_MIN_SPEED = 0.1f;

    protected NavMeshAgent m_navMeshAgent;

    // 移動のタイムアウト用
    private float m_stuckTimer;
    private Vector3 m_lastProgressPosition;

    // 上下揺れ用
    private float m_baseOffset;
    private float m_bobPhase;
    private float m_currentBob;

    protected virtual void Awake()
    {
        m_navMeshAgent =
            GetComponent<NavMeshAgent>();

        // NPC ごとに歩く速さを変える
        m_navMeshAgent.speed =
            Random.Range(
                m_minWalkSpeed,
                m_maxWalkSpeed);

        // NPC ごとに譲り合いの優先度を変える
        // （全員同じだと、すれ違うときに押し合いになりやすい）
        // Random.Range(int, int) は最大値を含まないので +1 する
        m_navMeshAgent.avoidancePriority =
            Random.Range(
                m_minAvoidancePriority,
                m_maxAvoidancePriority + 1);

        // 向きは UpdateTurn で自分で回す
        // （NavMeshAgent に任せると曲がり角でカクッと向きが変わる）
        m_navMeshAgent.updateRotation = false;

        m_baseOffset =
            m_navMeshAgent.baseOffset;

        // 全員が同じタイミングで揺れないようにずらす
        m_bobPhase =
            Random.Range(0f, Mathf.PI);
    }

    protected virtual void OnDisable()
    {
        // 揺れの途中で止まっても高さを元に戻す
        if (m_navMeshAgent != null)
        {
            m_navMeshAgent.baseOffset = m_baseOffset;
        }
    }

    void Update()
    {
        // NavMesh に乗っていない間は Agent を操作できない
        if (!m_navMeshAgent.isOnNavMesh)
        {
            return;
        }

        OnUpdate();

        // OnUpdate の中で非表示・削除された場合は何もしない
        if (!isActiveAndEnabled)
        {
            return;
        }

        UpdateTurn();

        UpdateBob();
    }

    // NPC の種類ごとの毎フレーム処理
    protected abstract void OnUpdate();

    //==================================================
    // 移動
    //==================================================

    // 指定位置へ歩き始める。歩き始められたら true
    protected bool StartMove(Vector3 targetPosition)
    {
        if (!m_navMeshAgent.isOnNavMesh)
        {
            Debug.LogWarning(
                "NPC が NavMesh 上にいません: " + name);

            return false;
        }

        NavMeshHit navMeshHit;

        if (!NavMesh.SamplePosition(
            targetPosition,
            out navMeshHit,
            m_navMeshSampleDistance,
            NavMesh.AllAreas))
        {
            Debug.LogWarning(
                "目的地が NavMesh 上にありません: "
                + name + " → " + targetPosition);

            return false;
        }

        m_navMeshAgent.isStopped = false;

        if (!m_navMeshAgent.SetDestination(
            navMeshHit.position))
        {
            return false;
        }

        ResetStuckCheck();

        return true;
    }

    // 詰まり判定をリセット（今の位置から測り直す）
    void ResetStuckCheck()
    {
        m_stuckTimer = 0f;

        m_lastProgressPosition =
            transform.position;
    }

    // 移動中に毎フレーム呼ぶ。到着・失敗を返す
    protected MOVE_STATUS UpdateMove()
    {
        // 詰まり判定
        // 「歩き始めてから何秒」ではなく「進めていない時間」で判定するので、
        // 遠い目的地でも途中で諦めない
        Vector3 moved =
            transform.position - m_lastProgressPosition;

        moved.y = 0f;

        if (moved.sqrMagnitude >=
            STUCK_MOVE_DISTANCE * STUCK_MOVE_DISTANCE)
        {
            // 進んでいるのでリセット
            ResetStuckCheck();
        }
        else
        {
            m_stuckTimer += Time.deltaTime;

            // 他の NPC や障害物で詰まった場合の保険
            if (m_stuckTimer >= m_stuckTime)
            {
                return MOVE_STATUS.FAILED;
            }
        }

        if (m_navMeshAgent.pathPending)
        {
            return MOVE_STATUS.MOVING;
        }

        // 経路が作れなかった
        if (m_navMeshAgent.pathStatus ==
            NavMeshPathStatus.PathInvalid)
        {
            return MOVE_STATUS.FAILED;
        }

        if (m_navMeshAgent.remainingDistance <=
            m_navMeshAgent.stoppingDistance)
        {
            return MOVE_STATUS.ARRIVED;
        }

        return MOVE_STATUS.MOVING;
    }

    // その場で止める
    protected void StopAgent()
    {
        if (!m_navMeshAgent.isOnNavMesh)
        {
            return;
        }

        m_navMeshAgent.isStopped = true;

        // 残っている経路と慣性を消して、滑らないようにする
        m_navMeshAgent.ResetPath();
        m_navMeshAgent.velocity = Vector3.zero;
    }

    //==================================================
    // 進む方向へゆっくり向きを変える
    //==================================================

    void UpdateTurn()
    {
        // 実際に進んでいる方向（加速度で少しずつ変わるので、経路の折れ線より滑らか）
        Vector3 moveDirection =
            m_navMeshAgent.velocity;

        moveDirection.y = 0f;

        // 止まっている・止まりかけのときは回さない
        // （見回しなど、派生クラスが自分で向きを決めているときも邪魔しない）
        if (moveDirection.sqrMagnitude <
            TURN_MIN_SPEED * TURN_MIN_SPEED)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(moveDirection);

        // 向きの差が大きいほど速く、近づくほどゆっくり回る（自然な減速）
        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-m_turnSmoothness * Time.deltaTime));
    }

    //==================================================
    // 歩くときの上下の揺れ
    //==================================================

    void UpdateBob()
    {
        // 実際の移動速度（加速・減速中は小さくなる）
        float currentSpeed =
            m_navMeshAgent.velocity.magnitude;

        // 0 ～ 1 : 止まっているほど揺れが小さい
        float speedRate =
            m_navMeshAgent.speed > 0f
                ? Mathf.Clamp01(currentSpeed / m_navMeshAgent.speed)
                : 0f;

        // 進んだ距離に合わせて揺れを進める（1歩で1回上下）
        m_bobPhase +=
            currentSpeed *
            m_stepsPerMeter *
            Mathf.PI *
            Time.deltaTime;

        // |sin| で「着地 → 持ち上がる → 着地」の弾む動き
        float bob =
            Mathf.Abs(Mathf.Sin(m_bobPhase)) *
            m_bobHeight *
            speedRate;

        // 急停止したときに高さがカクッと戻らないよう滑らかにする
        m_currentBob =
            Mathf.Lerp(
                m_currentBob,
                bob,
                1f - Mathf.Exp(-BOB_SMOOTHING * Time.deltaTime));

        // baseOffset を変えると見た目の高さだけが変わり、
        // NavMeshAgent の移動とは競合しない
        m_navMeshAgent.baseOffset =
            m_baseOffset + m_currentBob;
    }

    //==================================================
    // Inspector の入力ミス対策
    //==================================================

    protected virtual void OnValidate()
    {
        m_navMeshSampleDistance = Mathf.Max(0.1f, m_navMeshSampleDistance);
        m_stuckTime = Mathf.Max(0.5f, m_stuckTime);

        m_minWalkSpeed = Mathf.Max(0.1f, m_minWalkSpeed);
        m_maxWalkSpeed = Mathf.Max(m_minWalkSpeed, m_maxWalkSpeed);

        m_bobHeight = Mathf.Max(0f, m_bobHeight);
        m_stepsPerMeter = Mathf.Max(0f, m_stepsPerMeter);

        m_turnSmoothness = Mathf.Max(0.1f, m_turnSmoothness);

        m_minAvoidancePriority = Mathf.Clamp(m_minAvoidancePriority, 0, 99);
        m_maxAvoidancePriority = Mathf.Clamp(m_maxAvoidancePriority, m_minAvoidancePriority, 99);
    }
}
