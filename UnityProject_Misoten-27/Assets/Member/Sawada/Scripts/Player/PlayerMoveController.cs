using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float m_acceleration = 15.0f;
    [SerializeField] private float m_deceleration = 20.0f;

    [Header("Rotation")]
    [SerializeField] private float m_rotationSpeed = 720.0f;
    [SerializeField, Min(0f)] private float m_sprintTurnSpeed = 60.0f;

    [Header("Input")]
    [SerializeField] private float m_inputThreshold = 0.01f;

    [Header("Stop")]
    [SerializeField] private float m_stopThreshold = 0.001f;

    [Header("Camera")]
    [SerializeField] private Transform m_movementReference;
    private TestPlayerCameraLookController m_movementCamera;
    [SerializeField] private Unity.Cinemachine.CinemachineCamera m_tackleCamera;
    [SerializeField] private Sawada.Cordex.TackleCameraEffects.Settings m_tackleCameraEffects = new();
    private bool m_sprintCameraActive;
    public bool SprintCameraActive => m_sprintCameraActive || NetworkSprinting;
    public int TackleHitSequence { get; private set; }

    public void SetNetworkTackleHitSequence(int sequence) => TackleHitSequence = sequence;
    public void EndSprintCameraEffect() => m_sprintCameraActive = false;

    [Header("Inventory")]
    [SerializeField] private Inventory m_inventory;

    [Header("Tackle Knockback")]
    [SerializeField, Min(0f)] private float m_tackleImpulse = 10.0f;
    [SerializeField, Min(0f)] private float m_knockbackDuration = 0.35f;
    private float m_knockbackUntil;
    private bool IsKnockedBack => Time.time < m_knockbackUntil;

    private Rigidbody m_rb;
    [SerializeField] private PlayerInput m_playerInput;
    private InputAction m_moveAction;
    private InputAction m_sprintAction;

    // Sprint
    private bool m_rushFlag = false;
    private bool m_networkInput;
    private Vector2 m_networkMove;
    private bool m_networkSprint;
    private float m_networkSprintSteer;
    private Vector3 m_networkForward = Vector3.forward;
    private Vector3 m_sprintDirection;

    // The Host supplies each owner's input; offline PlayerInput behavior is unchanged.
    public void SetNetworkInput(Vector2 move, bool sprint, Vector3 forward, float sprintSteer)
    {
        m_networkInput = true;
        m_networkMove = Vector2.ClampMagnitude(move, 1f);
        m_networkSprint = sprint;
        m_networkSprintSteer = Mathf.Clamp(sprintSteer, -1f, 1f);
        if (forward.sqrMagnitude > .001f) m_networkForward = forward.normalized;
    }


    // =========================================================
    // Public
    // =========================================================
    public Vector2 MoveInput
    {
        get
        {
            if (m_networkInput) return m_networkMove;
            if (m_playerInput == null ||
                !m_playerInput.enabled ||
                m_moveAction == null)
            {
                return Vector2.zero;
            }

            return m_moveAction.ReadValue<Vector2>();
        }
    }

    public bool HasMoveInput => IsMoveInput(MoveInput);

    public bool IsMoveInput(Vector2 input) => input.sqrMagnitude > m_inputThreshold * m_inputThreshold;

    public bool PressSprint // pcテスト用
    {
        get
        {
            return m_networkInput ? m_networkSprint : m_sprintAction.IsPressed();
        }
    }

    public void SetRushFlag(bool n) { m_rushFlag = n; }

    public void SetMovementCamera(TestPlayerCameraLookController camera, Transform cameraTarget)
    {
        if (m_movementReference == cameraTarget)
            m_movementCamera = camera;
    }


    // =========================================================
    // Unity
    // =========================================================
    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        //m_playerInput = GetComponent<PlayerInput>();

        m_moveAction = m_playerInput.actions.FindAction("Move", true);
        m_sprintAction = m_playerInput.actions.FindAction("Sprint", true);

        if (m_tackleCamera == null)
            m_tackleCamera = m_playerInput.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
        if (m_tackleCamera != null)
        {
            var effects = m_tackleCamera.GetComponent<Sawada.Cordex.TackleCameraEffects>();
            if (effects == null) effects = m_tackleCamera.gameObject.AddComponent<Sawada.Cordex.TackleCameraEffects>();
            effects.Bind(this, m_tackleCameraEffects);
        }
    }

    //private void OnTriggerEnter(Collider other)
    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Character") || collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("接触");
            RushCharacter(collision.gameObject.GetComponent<CharacterReferenceController>());
        }
    }


    // =========================================================
    // Move
    // =========================================================
    public void Move(float maxSpeed)
    {
        if (IsKnockedBack) return;
        Vector2 input = MoveInput;
        SetHorizontalVelocity(StepWalkVelocity(m_rb.linearVelocity, input, GetMoveDirection(input), maxSpeed, Time.fixedDeltaTime));
    }

    public bool HasNetworkInput => m_networkInput;
    public bool NetworkSprinting { get; set; }

    public Vector3 StepWalkVelocity(Vector3 velocity, Vector2 input, Vector3 direction, float maxSpeed, float deltaTime)
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 targetVelocity = direction * maxSpeed;
        bool hasInput = IsMoveInput(input);

        float speedChange = hasInput ? m_acceleration : m_deceleration;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity, targetVelocity, speedChange * deltaTime);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            horizontalVelocity = Vector3.zero;

        return horizontalVelocity;
    }
    public void BeginSprint()
    {
        m_sprintCameraActive = true;
        Vector3 forward = m_rb.rotation * Vector3.forward;
        if (m_networkInput)
            forward = m_networkForward;
        else if (m_movementReference != null)
            forward = m_movementReference.forward;

        m_sprintDirection = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
    }

    public void MoveSprint(float maxSpeed, float minSpeed, float time, float timer)
    {
        if (IsKnockedBack) return;
        float steer = m_networkInput ? m_networkSprintSteer : MoveInput.x;
        if (Mathf.Abs(steer) > m_inputThreshold)
        {
            float turn = steer * m_sprintTurnSpeed * Time.fixedDeltaTime;
            m_sprintDirection = Quaternion.AngleAxis(turn, Vector3.up) * m_sprintDirection;
        }

        float currentSpeed = Mathf.Lerp(maxSpeed, minSpeed, Mathf.Clamp01(timer / time));
        SetHorizontalVelocity(m_sprintDirection * currentSpeed);
    }

    public void Decelerate()
    {
        if (IsKnockedBack) return;
        Vector3 velocity = m_rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity, Vector3.zero, m_deceleration * Time.fixedDeltaTime);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            horizontalVelocity = Vector3.zero;

        SetHorizontalVelocity(horizontalVelocity);
    }


    // =========================================================
    // Sprint
    // =========================================================
    // アーマーの有無にかかわらず吹き飛ばし、装備中の場合だけ強奪する。
    public void RushCharacter(CharacterReferenceController reference)
    {
        if (m_rushFlag == false || IsKnockedBack || m_inventory == null) return;
        if (reference == null || reference.Inventory == m_inventory ||
            reference.GetComponentInParent<Rigidbody>() == m_rb) return;

        // 空スロットやInventory未設定のNPCでは装備交換を行わない。
        if (reference.Inventory != null && reference.Inventory.GetCurrentArmor() != null)
        {
            BaseArmorSO armorSO = reference.Inventory.SwapArmor(null);
            BaseArmorSO oldArmorSO = m_inventory.SwapArmor(armorSO);
            Debug.Log($"強奪完了: {armorSO}->{oldArmorSO}");
        }

        SetRushFlag(false);
        var targetController = reference.GetComponentInParent<PlayerMoveController>();
        if (targetController != null) targetController.TackleHitSequence++;
        ApplyTackleKnockback(reference);
    }

    private void ApplyTackleKnockback(CharacterReferenceController reference)
    {
        Rigidbody targetBody = reference.GetComponentInParent<Rigidbody>();
        if (targetBody == null || targetBody == m_rb || targetBody.isKinematic || m_tackleImpulse <= 0f)
            return;

        // 強奪側の正面を中心とした水平180度（左右90度）から選ぶ。
        Vector3 forward = Vector3.ProjectOnPlane(m_rb.rotation * Vector3.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Vector3 direction = Quaternion.AngleAxis(Random.Range(-90f, 90f), Vector3.up) * forward;

        if (targetBody.TryGetComponent<PlayerMoveController>(out var targetController))
            targetController.m_knockbackUntil = Time.time + Mathf.Max(m_knockbackDuration, Time.fixedDeltaTime);

        targetBody.AddForce(direction * m_tackleImpulse, ForceMode.Impulse);
    }

    // =========================================================
    // Rotation
    // =========================================================
    public void Rotate()
    {
        if (IsKnockedBack) return;
        var rotation = StepWalkRotation(m_rb.rotation, m_rb.linearVelocity, Time.fixedDeltaTime);
        if (rotation != m_rb.rotation) m_rb.MoveRotation(rotation);
    }

    public Quaternion StepWalkRotation(Quaternion rotation, Vector3 velocity, float deltaTime)
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        if (horizontalVelocity.sqrMagnitude <= m_stopThreshold * m_stopThreshold)
            return rotation;

        Quaternion targetRotation = Quaternion.LookRotation(
            horizontalVelocity.normalized, Vector3.up);

        return Quaternion.RotateTowards(rotation, targetRotation, m_rotationSpeed * deltaTime);
    }


    // =========================================================
    // Private
    // =========================================================
    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (m_networkInput) return new Vector3(input.x, 0, input.y);
        if (m_movementReference == null)
            return Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(
            m_movementReference.forward, Vector3.up).normalized;

        Vector3 right = Vector3.ProjectOnPlane(
            m_movementReference.right, Vector3.up).normalized;

        if (m_movementCamera != null &&
            m_movementCamera.TryGetMovementForward(input, forward, out Vector3 movementForward))
        {
            forward = movementForward;
            right = Vector3.Cross(Vector3.up, forward);
        }

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
