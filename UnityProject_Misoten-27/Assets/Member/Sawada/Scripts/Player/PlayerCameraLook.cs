using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCameraLook : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform cameraTarget;

    [Header("Sensitivity")]
    [SerializeField]
    private float mouseSensitivity = 0.08f;

    [SerializeField]
    private float gamepadSensitivity = 180.0f;

    [Header("Vertical Limit")]
    [SerializeField]
    private float minPitch = -40.0f;

    [SerializeField]
    private float maxPitch = 70.0f;

    [Header("Control Scheme")]
    [SerializeField]
    private string gamepadSchemeName = "Gamepad";

    [SerializeField]
    private string keyboardMouseSchemeName = "Keyboard&Mouse";

    private PlayerInput playerInput;
    private InputAction lookAction;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        lookAction = playerInput.actions.FindAction(
            "Look",
            true
        );
    }

    private void Start()
    {
        Vector3 angles = cameraTarget.eulerAngles;

        yaw = angles.y;
        pitch = NormalizeAngle(angles.x);
    }

    private void Update()
    {
        if (!playerInput.enabled) return;   // 作業用

        Vector2 lookInput =
            lookAction.ReadValue<Vector2>();

        bool usingGamepad =
            playerInput.currentControlScheme
            == gamepadSchemeName;

        if (usingGamepad)
        {
            // Stickは -1 ～ +1 なので
            // 時間を掛けて「度/秒」にする
            yaw +=
                lookInput.x *
                gamepadSensitivity *
                Time.deltaTime;

            pitch -=
                lookInput.y *
                gamepadSensitivity *
                Time.deltaTime;
        }
        else
        {
            // Mouse Deltaはフレーム内の移動量なので
            // Time.deltaTimeを掛けない
            yaw +=
                lookInput.x *
                mouseSensitivity;

            pitch -=
                lookInput.y *
                mouseSensitivity;
        }

        pitch =
            Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );

        cameraTarget.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0.0f
            );

        HandleCursor();
    }

    private void HandleCursor()
    {
        if (playerInput.currentControlScheme
            != keyboardMouseSchemeName)
        {
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;

            return;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180.0f)
        {
            angle -= 360.0f;
        }

        return angle;
    }
}
