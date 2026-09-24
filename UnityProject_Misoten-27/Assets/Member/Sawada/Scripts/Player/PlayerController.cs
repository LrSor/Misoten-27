using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField]
    private float maxSpeed = 5.0f;

    [SerializeField]
    private float acceleration = 15.0f;

    [SerializeField]
    private float deceleration = 20.0f;

    [Header("Rotation")]
    [SerializeField]
    private float rotationSpeed = 720.0f;

    [Header("Stop")]
    [SerializeField]
    private float stopThreshold = 0.001f;

    [Header("Camera")]
    [SerializeField]
    private Transform movementReference;

    private Rigidbody rb;
    private PlayerInput playerInput;
    private InputAction moveAction;

    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions.FindAction(
            "Move",
            true
        );
    }

    private void Update()
    {
        if (playerInput.enabled)/*作業用*/ moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (!playerInput.enabled) return;   // 作業用

        Move();
        Rotate();
    }

    private void Move()
    {
        if (movementReference == null)
        {
            return;
        }

        // カメラ基準の方向
        Vector3 cameraForward = movementReference.forward;
        Vector3 cameraRight = movementReference.right;

        // 上下方向を無視
        cameraForward.y = 0.0f;
        cameraRight.y = 0.0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        // カメラ基準の移動方向
        Vector3 moveDirection =
            cameraForward * moveInput.y +
            cameraRight * moveInput.x;

        moveDirection =
            Vector3.ClampMagnitude(
                moveDirection,
                1.0f
            );

        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0.0f,
                velocity.z
            );

        Vector3 targetVelocity =
            moveDirection * maxSpeed;

        float speedChange =
            moveInput.sqrMagnitude > 0.0f
                ? acceleration
                : deceleration;

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                speedChange * Time.fixedDeltaTime
            );

        if (horizontalVelocity.sqrMagnitude
            <= stopThreshold * stopThreshold)
        {
            horizontalVelocity = Vector3.zero;
        }

        // Y速度は重力用に維持
        rb.linearVelocity =
            new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
    }

    private void Rotate()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0.0f,
                velocity.z
            );

        if (horizontalVelocity.sqrMagnitude
            <= stopThreshold * stopThreshold)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                horizontalVelocity.normalized,
                Vector3.up
            );

        Quaternion newRotation =
            Quaternion.RotateTowards(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(newRotation);
    }
}
