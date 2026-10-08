using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float m_acceleration = 15.0f;
    [SerializeField] private float m_deceleration = 20.0f;

    [Header("Rotation")]
    [SerializeField] private float m_rotationSpeed = 720.0f;

    [Header("Input")]
    [SerializeField] private float m_inputThreshold = 0.01f;

    [Header("Stop")]
    [SerializeField] private float m_stopThreshold = 0.001f;

    [Header("Camera")]
    [SerializeField] private Transform m_movementReference;

    private Rigidbody m_rb;
    [SerializeField] private PlayerInput m_playerInput;
    private InputAction m_moveAction;


    // =========================================================
    // Public
    // =========================================================

    public Vector2 MoveInput
    {
        get
        {
            if (m_playerInput == null ||
                !m_playerInput.enabled ||
                m_moveAction == null)
            {
                return Vector2.zero;
            }

            return m_moveAction.ReadValue<Vector2>();
        }
    }

    public bool HasMoveInput
    {
        get
        {
            Vector2 input = MoveInput;

            return input.sqrMagnitude > m_inputThreshold * m_inputThreshold;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        //m_playerInput = GetComponent<PlayerInput>();

        m_moveAction = m_playerInput.actions.FindAction("Move", true);
    }


    // =========================================================
    // Move
    // =========================================================

    public void Move(float maxSpeed)
    {
        Vector2 input = MoveInput;

        Vector3 moveDirection = GetMoveDirection(input);

        Vector3 velocity = m_rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3( velocity.x, 0.0f, velocity.z);

        Vector3 targetVelocity = moveDirection * maxSpeed;

        bool hasInput = input.sqrMagnitude > m_inputThreshold * m_inputThreshold;

        float speedChange = hasInput ? m_acceleration : m_deceleration;

        Debug.Log($"hasInput: {hasInput}, speedChange: {speedChange}");
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity, targetVelocity, speedChange * Time.fixedDeltaTime);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            { horizontalVelocity = Vector3.zero;
        Debug.Log("Stopping...");
        }

        SetHorizontalVelocity(horizontalVelocity);
    }


    public void Decelerate()
    {
        Vector3 velocity = m_rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity, Vector3.zero, m_deceleration * Time.fixedDeltaTime);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            horizontalVelocity = Vector3.zero;

        SetHorizontalVelocity(horizontalVelocity);
    }


    // =========================================================
    // Rotation
    // =========================================================

    public void Rotate()
    {
        Vector3 velocity = m_rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            horizontalVelocity.normalized, Vector3.up);

        Quaternion newRotation = Quaternion.RotateTowards(
            m_rb.rotation, targetRotation, m_rotationSpeed * Time.fixedDeltaTime);

        m_rb.MoveRotation(newRotation);
    }


    // =========================================================
    // Private
    // =========================================================

    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (m_movementReference == null)
            return Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(
            m_movementReference.forward, Vector3.up).normalized;

        Vector3 right = Vector3.ProjectOnPlane(
            m_movementReference.right, Vector3.up).normalized;

        Vector3 moveDirection = forward * input.y + right * input.x;

        return Vector3.ClampMagnitude(moveDirection, 1.0f);
    }


    private void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        Vector3 velocity = m_rb.linearVelocity;

        m_rb.linearVelocity = new Vector3(
            horizontalVelocity.x, velocity.y, horizontalVelocity.z);
    }
}
