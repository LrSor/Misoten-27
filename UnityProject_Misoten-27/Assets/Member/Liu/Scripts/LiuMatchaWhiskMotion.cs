using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class LiuMatchaWhiskMotion : MonoBehaviour
{
    [Header("キーボード動作テスト")]
    [SerializeField] private bool m_keyboardTest = true;

    [Tooltip("スペースキーを押している間の回転速度（周／秒）")]
    [SerializeField, Min(0f)]
    private float m_testTurnsPerSecond = 1f;

    [Header("茶筅の動き")]
    [Tooltip("茶碗の中心からの移動半径")]
    [SerializeField, Range(0f, 0.25f)]
    private float m_orbitRadius = 0.15f;

    private Vector3 m_startLocalPosition;
    private float m_angle;
    private float m_currentRadius;
    private float m_turnsPerSecond;
    private bool m_initialized;

    private void Awake()
    {
        // 配置した位置を円運動の中心として保存する。
        m_startLocalPosition = transform.localPosition;
        m_initialized = true;
    }

    private void Update()
    {
        float speed = m_turnsPerSecond;

        // テスト入力と外部からの入力を切り替える。
        if (m_keyboardTest && (JoyconManager.Instance == null || JoyconManager.Instance.GetIsKeyboardPlay()))
        {
            Keyboard keyboard = Keyboard.current;

            bool isStirring =
                keyboard != null && keyboard.spaceKey.isPressed;

            speed = isStirring ? m_testTurnsPerSecond : 0f;
        }

        if (speed != 0f)
        {
            m_angle = Mathf.Repeat(
                m_angle + speed * Mathf.PI * 2f * Time.deltaTime,
                Mathf.PI * 2f);
        }

        // 動き始めは外側へ、停止時は中心へ滑らかに移動する。
        float targetRadius = speed != 0f ? m_orbitRadius : 0f;

        m_currentRadius = Mathf.MoveTowards(
            m_currentRadius,
            targetRadius,
            Time.deltaTime);

        Vector3 offset = new Vector3(
            Mathf.Cos(m_angle),
            0f,
            Mathf.Sin(m_angle)) * m_currentRadius;

        // 親のXZ平面上で動かし、茶筅の傾きは維持する。
        transform.localPosition = m_startLocalPosition + offset;
    }

    // 外部制御時は Keyboard Test をオフにする。
    public void SetStirSpeed(float turnsPerSecond)
    {
        m_turnsPerSecond = Mathf.Max(0f, turnsPerSecond);
    }

    public void SetSignedStirSpeed(float signedTurnsPerSecond)
    {
        m_turnsPerSecond = signedTurnsPerSecond;
    }

    public void ResetMotion()
    {
        m_turnsPerSecond = 0f;
        m_angle = 0f;
        m_currentRadius = 0f;

        if (m_initialized)
        {
            transform.localPosition = m_startLocalPosition;
        }
    }

    private void OnDisable()
    {
        // 無効化されたら初期位置へ戻す。
        ResetMotion();
    }
}
