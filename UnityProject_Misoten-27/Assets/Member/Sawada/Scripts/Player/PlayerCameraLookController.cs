using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraLookController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform m_cameraTarget;

    [Header("Sensitivity")]
    [SerializeField]
    private float m_mouseSensitivity = 0.08f;

    [SerializeField]
    private float m_gamepadSensitivity = 180.0f;

    [Header("Vertical Limit")]
    [SerializeField]
    private float m_minPitch = -40.0f;

    [SerializeField]
    private float m_maxPitch = 70.0f;

    [Header("Control Scheme")]
    [SerializeField]
    private string m_gamepadSchemeName = "Gamepad";

    [SerializeField]
    private string m_keyboardMouseSchemeName = "Keyboard&Mouse";

    [SerializeField]
    private PlayerInput m_playerInput;
    private InputAction m_lookAction;

    [Header("Camera Rotation")]
    private float m_rotationDuration = 1.0f; // 回転にかける秒数
    private float m_startYaw;       // 回転開始時の角度
    private float m_targetYaw;      // 回転終了時の角度
    private float m_rotationTimer;  // 経過時間
    private bool m_isRotating;      // 回転中かどうか

    private float m_yaw;
    private float m_pitch;


    private void Awake()
    {
        //m_playerInput = GetComponent<PlayerInput>();

        m_lookAction = m_playerInput.actions.FindAction(
            "Look",
            true
        );
    }

    private void Start()
    {
        Vector3 angles = m_cameraTarget.eulerAngles;

        m_yaw = angles.y;
        m_pitch = NormalizeAngle(angles.x);
    }

    public void Look()
    {
        if (!m_playerInput.enabled) return;   // 作業用

        Vector2 lookInput = m_lookAction.ReadValue<Vector2>();

        bool usingGamepad = m_playerInput.currentControlScheme == m_gamepadSchemeName;

        if (usingGamepad)
        {
            // Stickは -1 ～ +1 なので
            // 時間を掛けて「度/秒」にする
            m_yaw += lookInput.x * m_gamepadSensitivity * Time.deltaTime;
            m_pitch -= lookInput.y * m_gamepadSensitivity * Time.deltaTime;
        }
        else
        {
            // Mouse Deltaはフレーム内の移動量なので
            // Time.deltaTimeを掛けない
            m_yaw += lookInput.x * m_mouseSensitivity;
            m_pitch -= lookInput.y * m_mouseSensitivity;
        }

        m_pitch = Mathf.Clamp(m_pitch, m_minPitch, m_maxPitch);

        m_cameraTarget.rotation = Quaternion.Euler(m_pitch, m_yaw, 0.0f);

        HandleCursor();
    }

    public void Look_ver2()
    {
        if (!m_playerInput.enabled) return;

        // 回転中でなければ入力を受け付ける
        if (!m_isRotating && Keyboard.current != null)
        {
            float rotationAmount = 0.0f;

            if (Keyboard.current.rightArrowKey.wasReleasedThisFrame)
            {
                rotationAmount = 90.0f;
            }
            else if (Keyboard.current.leftArrowKey.wasReleasedThisFrame)
            {
                rotationAmount = -90.0f;
            }

            // 回転開始
            if (rotationAmount != 0.0f)
            {
                m_startYaw = m_yaw;
                m_targetYaw = m_yaw + rotationAmount;

                m_rotationTimer = 0.0f;
                m_isRotating = true;
            }
        }

        // 回転処理
        if (m_isRotating)
        {
            m_rotationTimer += Time.deltaTime;

            // Ease Out（最初が速く、最後が遅い）
            float t = Mathf.Clamp01(m_rotationTimer / Mathf.Max(0.0001f, m_rotationDuration));
            t = 1.0f - Mathf.Pow(1.0f - t, 3.0f);

            m_yaw = Mathf.Lerp(m_startYaw, m_targetYaw, t);

            // 回転終了
            if (t >= 1.0f)
            {
                m_yaw = m_targetYaw;
                m_isRotating = false;
            }
        }

        m_pitch = Mathf.Clamp(m_pitch, m_minPitch, m_maxPitch);

        m_cameraTarget.rotation = Quaternion.Euler(m_pitch, m_yaw, 0.0f);

        HandleCursor();
    }

    private void HandleCursor()
    {
        if (m_playerInput.currentControlScheme != m_keyboardMouseSchemeName)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            return;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180.0f)
            angle -= 360.0f;

        return angle;
    }
}
