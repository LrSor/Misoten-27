using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCameraController : MonoBehaviour
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


    // =========================================================
    // Public
    // =========================================================
    /// 現在の視点操作入力を取得する。
    public Vector2 LookInput
    {
        get
        {
            if (m_playerInput == null ||
                !m_playerInput.enabled ||
                m_lookAction == null)
            {
                return Vector2.zero;
            }

            return m_lookAction.ReadValue<Vector2>();
        }
    }

    /// 入力に応じてカメラの向きを更新する。
    public void Look()
    {
        // PlayerInputが使用できない場合は処理しない
        if (m_playerInput == null ||
            !m_playerInput.enabled ||
            m_lookAction == null ||
            m_cameraTarget == null)
        {
            return;
        }

        Vector2 lookInput = LookInput;

        bool usingGamepad =
            m_playerInput.currentControlScheme
            == m_gamepadSchemeName;


        // ---------------------------------------------------------
        // Gamepad
        // ---------------------------------------------------------
        if (usingGamepad)
        {
            // Stick入力は -1 ～ +1。
            // 感度を「度/秒」として扱うためdeltaTimeを掛ける。
            m_yaw +=
                lookInput.x *
                m_gamepadSensitivity *
                Time.deltaTime;

            m_pitch -=
                lookInput.y *
                m_gamepadSensitivity *
                Time.deltaTime;
        }


        // ---------------------------------------------------------
        // Mouse
        // ---------------------------------------------------------
        else
        {
            // Mouse Deltaはそのフレーム内で移動した量。
            m_yaw +=
                lookInput.x *
                m_mouseSensitivity;

            m_pitch -=
                lookInput.y *
                m_mouseSensitivity;
        }


        // ---------------------------------------------------------
        // Vertical Limit
        // ---------------------------------------------------------

        m_pitch = Mathf.Clamp(
            m_pitch,
            m_minPitch,
            m_maxPitch);


        // ---------------------------------------------------------
        // Rotation
        // ---------------------------------------------------------

        m_cameraTarget.rotation =
            Quaternion.Euler(
                m_pitch,
                m_yaw,
                0.0f);
    }


    /// <summary>
    /// マウスカーソルのロック状態を管理する。
    ///
    /// Escape
    ///     カーソルロック解除
    ///
    /// Left Click
    ///     カーソルロック
    ///
    /// Keyboard&Mouse使用時のみ処理する。
    /// StateのUpdateから呼び出す。
    /// </summary>
    public void HandleCursor()
    {
        if (m_playerInput == null ||
            !m_playerInput.enabled)
        {
            return;
        }

        // ゲームパッド使用時は
        // マウスカーソルの処理を行わない
        if (m_playerInput.currentControlScheme
            != m_keyboardMouseSchemeName)
        {
            return;
        }


        // ---------------------------------------------------------
        // Unlock Cursor
        // ---------------------------------------------------------

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;

            return;
        }


        // ---------------------------------------------------------
        // Lock Cursor
        // ---------------------------------------------------------

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        m_playerInput =
            GetComponent<PlayerInput>();

        m_lookAction =
            m_playerInput.actions.FindAction(
                "Look",
                true);
    }


    private void Start()
    {
        if (m_cameraTarget == null)
        {
            return;
        }

        // CameraTargetの現在角度を初期値として使用する
        Vector3 angles =
            m_cameraTarget.eulerAngles;

        m_yaw =
            angles.y;

        m_pitch =
            NormalizeAngle(angles.x);
    }


    // =========================================================
    // Private
    // =========================================================

    /// <summary>
    /// Unityの0～360度表現を
    /// -180～180度付近の値へ変換する。
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        if (angle > 180.0f)
        {
            angle -= 360.0f;
        }

        return angle;
    }
}
