using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCameraLook : MonoBehaviour
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

    private PlayerInput m_playerInput;
    private InputAction m_lookAction;

    private float m_yaw;
    private float m_pitch;


    private void Awake()
    {
        m_playerInput = GetComponent<PlayerInput>();

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
