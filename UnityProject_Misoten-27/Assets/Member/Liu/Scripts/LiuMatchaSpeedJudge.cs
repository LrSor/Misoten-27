using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(LiuMatchaWhiskMotion))]
[DefaultExecutionOrder(-100)]
public class LiuMatchaSpeedJudge : MonoBehaviour
{
    public enum SpeedState
    {
        Stopped,
        TooSlow,
        Good,
        TooFast
    }

    [Header("速度の判定基準（仮）")]
    [SerializeField, Min(0.01f)]
    private float m_minGoodSpeed = 0.6f;

    [SerializeField, Min(0.01f)]
    private float m_maxGoodSpeed = 1.5f;

    [Header("運動データによる適正速度上限の加算（周／秒・仮）")]
    [SerializeField, Min(0f)] private float m_groupSpeedBonus = 0.5f;
    [SerializeField, Min(0f)] private float m_exactSpeedBonus = 1f;

    private float m_roundSpeedBonus;

    [Header("キーボード動作テスト")]
    [SerializeField] private bool m_keyboardTest = true;

    [Tooltip("数字1キー：低速（周／秒）")]
    [SerializeField, Min(0f)]
    private float m_slowTestSpeed = 0.3f;

    [Tooltip("スペースキー：通常速度（周／秒）")]
    [SerializeField, Min(0f)]
    private float m_normalTestSpeed = 1f;

    [Tooltip("数字2キー：高速（周／秒）")]
    [SerializeField, Min(0f)]
    private float m_fastTestSpeed = 2f;

    [Header("右Joy-Conの水平かき混ぜ")]
    [Tooltip("反応させる横加速度の下限（G）")]
    [SerializeField, Min(0.005f)] private float m_minCircleAcceleration = 0.025f;
    [SerializeField] private bool m_invertJoyconDirection;

    private readonly LiuMatchaCircleTracker m_circle = new LiuMatchaCircleTracker();
    private Joycon m_rightJoycon;
    private LiuMatchaWhiskMotion m_whiskMotion;

    public float CurrentSpeed { get; private set; }

    public SpeedState CurrentState { get; private set; }
        = SpeedState.Stopped;

    private void Awake()
    {
        m_whiskMotion = GetComponent<LiuMatchaWhiskMotion>();
    }

    private void Update()
    {
        var manager = JoyconManager.Instance;
        if (manager != null && !manager.GetIsKeyboardPlay())
        {
            ReadRightJoycon(manager);
            return;
        }

        if (!m_keyboardTest)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        float speed = 0f;

        if (keyboard != null)
        {
            // 複数押された場合は、2→1→スペースの順で優先する。
            if (keyboard.digit2Key.isPressed)
            {
                speed = m_fastTestSpeed;
            }
            else if (keyboard.digit1Key.isPressed)
            {
                speed = m_slowTestSpeed;
            }
            else if (keyboard.spaceKey.isPressed)
            {
                speed = m_normalTestSpeed;
            }
        }

        SetSpeed(speed);
    }

    private void ReadRightJoycon(JoyconManager manager)
    {
        Joycon right = manager.EnableIMU && manager.j != null
            ? manager.j.Find(pad => pad != null && !pad.isLeft && pad.state == Joycon.state_.IMU_DATA_OK)
            : null;
        if (right != m_rightJoycon)
        {
            m_circle.Reset();
            m_rightJoycon = right;
        }

        float speed = right == null ? 0f :
            m_circle.Sample(right.GetAccel(), Time.unscaledDeltaTime, m_minCircleAcceleration);
        if (m_invertJoyconDirection)
            speed = -speed;

        ApplySpeed(Mathf.Abs(speed), speed < 0f);
    }

    public void SetRoundEquipment(QuestData quest, PartData equippedPart)
    {
        m_roundSpeedBonus = 0f;
        if (quest == null || equippedPart == null)
            return;

        PartData recommended = quest.recommendedPart;
        if (recommended != null && !string.IsNullOrWhiteSpace(recommended.partID)
            && equippedPart.partID == recommended.partID)
        {
            m_roundSpeedBonus = m_exactSpeedBonus;
        }
        else if (equippedPart.group == quest.group)
        {
            m_roundSpeedBonus = m_groupSpeedBonus;
        }
    }

    public void SetSpeed(float turnsPerSecond)
    {
        ApplySpeed(turnsPerSecond, false);
    }

    private void ApplySpeed(float turnsPerSecond, bool reverse)
    {
        if (float.IsNaN(turnsPerSecond) ||
            float.IsInfinity(turnsPerSecond))
        {
            turnsPerSecond = 0f;
        }

        CurrentSpeed = Mathf.Max(0f, turnsPerSecond);

        SpeedState nextState;

        if (CurrentSpeed <= 0f)
        {
            nextState = SpeedState.Stopped;
        }
        else if (CurrentSpeed < m_minGoodSpeed)
        {
            nextState = SpeedState.TooSlow;
        }
        else if (CurrentSpeed <= m_maxGoodSpeed + m_roundSpeedBonus)
        {
            nextState = SpeedState.Good;
        }
        else
        {
            nextState = SpeedState.TooFast;
        }

        // 判定に使った速度を、そのまま見た目の動きへ渡す。
        if (m_whiskMotion != null)
        {
            m_whiskMotion.SetSignedStirSpeed(reverse ? -CurrentSpeed : CurrentSpeed);
        }

        // 状態が変化したときだけログを出す。
        if (nextState != CurrentState)
        {
            CurrentState = nextState;

            string label = nextState switch
            {
                SpeedState.Stopped => "停止",
                SpeedState.TooSlow => "遅すぎる",
                SpeedState.Good => "適正",
                SpeedState.TooFast => "速すぎる",
                _ => ""
            };

            Debug.Log(
                $"[抹茶速度] {label}：{CurrentSpeed:F2} 周／秒",
                this);
        }
    }

    private void OnDisable()
    {
        m_circle.Reset();
        m_rightJoycon = null;
        CurrentSpeed = 0f;
        CurrentState = SpeedState.Stopped;

        if (m_whiskMotion != null)
        {
            m_whiskMotion.ResetMotion();
        }
    }

    private void OnValidate()
    {
        // 上限が下限より小さくならないようにする。
        m_minGoodSpeed = Mathf.Max(0.01f, m_minGoodSpeed);
        m_maxGoodSpeed = Mathf.Max(m_minGoodSpeed, m_maxGoodSpeed);
        m_groupSpeedBonus = Mathf.Max(0f, m_groupSpeedBonus);
        m_exactSpeedBonus = Mathf.Max(m_groupSpeedBonus, m_exactSpeedBonus);
    }
}

// 水平加速度の向きが回った角度から周／秒を求める。手首のひねりは不要。
internal sealed class LiuMatchaCircleTracker
{
    private bool m_initialized;
    private Vector3 m_filteredAcceleration;
    private Vector3 m_gravity;
    private Vector3 m_lastDirection;
    private float m_arcDegrees;
    private float m_signedSpeed;

    public void Reset()
    {
        m_initialized = false;
        ClearArc();
    }

    private void ClearArc()
    {
        m_lastDirection = Vector3.zero;
        m_arcDegrees = 0f;
        m_signedSpeed = 0f;
    }

    public float Sample(Vector3 acceleration, float dt, float minAcceleration)
    {
        if (!(dt > 0f && dt <= 0.15f) || !(acceleration.sqrMagnitude < 256f))
        {
            Reset();
            return 0f;
        }
        if (!m_initialized)
        {
            if (acceleration.sqrMagnitude < 0.16f)
                return 0f;
            m_filteredAcceleration = m_gravity = acceleration;
            m_initialized = true;
            return 0f;
        }

        m_filteredAcceleration = Vector3.Lerp(m_filteredAcceleration, acceleration,
            1f - Mathf.Exp(-dt / 0.035f));
        m_gravity = Vector3.Lerp(m_gravity, m_filteredAcceleration, 1f - Mathf.Exp(-dt / 0.7f));
        Vector3 up = m_gravity.normalized;
        Vector3 horizontal = Vector3.ProjectOnPlane(m_filteredAcceleration - m_gravity, up);
        if (horizontal.sqrMagnitude < minAcceleration * minAcceleration)
        {
            ClearArc();
            return 0f;
        }

        Vector3 direction = horizontal.normalized;
        Vector3 previous = m_lastDirection;
        m_lastDirection = direction;
        if (previous == Vector3.zero)
            return 0f;

        float advance = Mathf.Atan2(Vector3.Dot(up, Vector3.Cross(previous, direction)),
            Vector3.Dot(previous, direction)) * Mathf.Rad2Deg;
        // 直線を往復するときの約180度の反転は回転として数えない。
        if (Mathf.Abs(advance) > 100f)
        {
            ClearArc();
            return 0f;
        }
        m_arcDegrees = Mathf.Clamp(m_arcDegrees + advance, -75f, 75f);
        float rate = Mathf.Clamp(advance / (360f * dt), -5f, 5f);
        m_signedSpeed = Mathf.Lerp(m_signedSpeed, rate, 1f - Mathf.Exp(-dt / 0.14f));
        return Mathf.Abs(m_arcDegrees) >= 40f && Mathf.Abs(m_signedSpeed) >= 0.12f
            ? m_signedSpeed : 0f;
    }
}
