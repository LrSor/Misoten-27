using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics;

public class TestPlayerCameraLookController : MonoBehaviour
{
    private const float MaxSplineDistance = 3.0f;
    private const float MoveInputDirectionDot = 0.85f;

    [Header("Target")]
    [SerializeField] private Transform m_cameraTarget;
    [SerializeField] private Transform m_player;

    [Header("Input")]
    [SerializeField] private PlayerInput m_playerInput;
    [SerializeField] private string m_moveActionName = "Move";
    [SerializeField] private float m_inputDeadZone = 0.15f;

    [Header("Splines")]
    [SerializeField]
    private Transform m_splinesParent;
    // 自動取得したSplineを保持
    private SplineContainer[] m_splineContainers;

    // Splineの切り替えを安定させる距離
    [SerializeField]
    private float m_splineSwitchMargin = 0.3f;

    [Header("Rotation")]
    [SerializeField]
    private float m_snapAngle = 30.0f;

    [SerializeField]
    private float m_rotationDuration = 1.0f;

    // 回転中に目標が大きく変化した場合の再補間角度
    [SerializeField]
    private float m_retargetAngle = 15.0f;

    [Header("Movement")]
    [SerializeField]
    private float m_minMovementSpeed = 0.05f;

    private InputAction m_moveAction;

    private SplineContainer m_currentSpline;

    private Vector3 m_previousPlayerPosition;
    private Vector2 m_previousMoveInput;
    private Vector2 m_freeMoveInput;
    private Vector3 m_freeMoveForward;
    private float? m_freeTargetYaw;
    private int m_lastLookFrame = -2;

    private bool m_directionLocked;
    private bool m_isReverseDirection;

    private float m_pitch;
    private float m_yaw;

    // EaseOut用
    private float m_startYaw;
    private float m_targetYaw;
    private float m_rotationTimer;
    private bool m_isRotating;
    private bool m_initialized;

    // Configure inactive network instances before Awake, without editing the prefab asset.
    public void ConfigureNetworkSplines(Transform splinesParent)
    {
        m_splinesParent = splinesParent;
    }

    public bool TryGetMovementForward(Vector2 moveInput, Vector3 cameraForward, out Vector3 forward)
    {
        forward = cameraForward;
        if (!isActiveAndEnabled || m_currentSpline != null ||
            Time.frameCount - m_lastLookFrame > 1 || moveInput == Vector2.zero)
        {
            m_freeMoveInput = Vector2.zero;
            return false;
        }

        // 同じ入力中にカメラが回っても、プレイヤーの進行方向を回さない。
        Vector2 inputDirection = moveInput.normalized;
        if (Vector2.Dot(m_freeMoveInput, inputDirection) < MoveInputDirectionDot)
        {
            m_freeMoveInput = inputDirection;
            m_freeMoveForward = cameraForward;
        }

        forward = m_freeMoveForward;
        return true;
    }


    private void Awake()
    {
        if (m_player == null)
            m_player = transform;

        if (m_player.TryGetComponent(out PlayerMoveController mover))
            mover.SetMovementCamera(this, m_cameraTarget);

        if (m_playerInput == null)
            m_playerInput = m_player.GetComponent<PlayerInput>();

        if (m_playerInput != null)
        {
            m_moveAction = m_playerInput.actions.FindAction(
                m_moveActionName, false
            );
        }

        if (m_splinesParent != null)
        {
            m_splineContainers =
                m_splinesParent.GetComponentsInChildren<SplineContainer>(true);
        }
        else
        {
            m_splineContainers = System.Array.Empty<SplineContainer>();
        }
    }

    private void Start()
    {
        if (m_cameraTarget != null)
        {
            Vector3 angles = m_cameraTarget.eulerAngles;

            m_pitch = Mathf.DeltaAngle(0.0f, angles.x);
            m_yaw = angles.y;
        }

        m_previousPlayerPosition = m_player.position;
        m_initialized = true;

        if (m_moveAction == null)
        {
            Debug.LogWarning(
                "Move Actionが見つかりません。移動入力の変化を検出できません。",
                this
            );
        }
    }

    // Cameraステートから毎フレーム呼び出す
    public void Look()
    {
        if (!m_initialized || m_cameraTarget == null || m_player == null)
            return;

        m_lastLookFrame = Time.frameCount;
        Vector3 currentPosition = m_player.position;

        Vector3 movement =
            currentPosition - m_previousPlayerPosition;

        m_previousPlayerPosition = currentPosition;
        movement.y = 0.0f;

        Vector2 moveInput = m_moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Listから最も近いSplineを取得
        if (!TryGetNearestSpline(
            currentPosition,
            out SplineContainer nearestSpline,
            out float t))
        {
            m_currentSpline = null;
            m_isRotating = false;
            m_previousMoveInput = Vector2.zero;
            if (moveInput == Vector2.zero)
                m_freeMoveInput = Vector2.zero;

            UpdateFreeRotation(movement);
            return;
        }

        m_freeMoveInput = Vector2.zero;
        m_freeTargetYaw = null;

        bool splineChanged = nearestSpline != m_currentSpline;
        m_currentSpline = nearestSpline;

        // SplineのXZ接線方向
        Vector3 direction =
            (Vector3)m_currentSpline.EvaluateTangent(t);

        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        // 移動方向によるSplineの向き判定
        UpdateMovementDirection(
            direction,
            splineChanged,
            moveInput
        );

        if (m_isReverseDirection)
            direction = -direction;

        // 接線方向から目標Yawを計算
        float desiredYaw = Mathf.Atan2(
            direction.x,
            direction.z
        ) * Mathf.Rad2Deg;

        // 角度差に合わせた回転
        UpdateRotation(desiredYaw);
    }


    // List内の最も近いSplineを取得
    private bool TryGetNearestSpline(
        Vector3 playerPosition,
        out SplineContainer result,
        out float resultT)
    {
        result = null;
        resultT = 0.0f;

        float minDistance = float.MaxValue;

        float currentDistance = float.MaxValue;
        float currentT = 0.0f;
        bool foundCurrentSpline = false;

        foreach (SplineContainer spline in m_splineContainers)
        {
            if (spline == null || spline.Spline.Count < 2)
                continue;

            Vector3 localPosition =
                spline.transform.InverseTransformPoint(
                    playerPosition
                );

            SplineUtility.GetNearestPoint(
                spline.Spline,
                (float3)localPosition,
                out float3 nearest,
                out float t
            );

            Vector3 nearestWorld =
                spline.transform.TransformPoint(
                    (Vector3)nearest
                );

            float distance = Vector3.Distance(
                playerPosition,
                nearestWorld
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                result = spline;
                resultT = t;
            }

            if (spline == m_currentSpline)
            {
                currentDistance = distance;
                currentT = t;
                foundCurrentSpline = true;
            }
        }

        if (result == null || minDistance > MaxSplineDistance)
            return false;

        // わずかな距離差ではSplineを切り替えない
        if (foundCurrentSpline &&
            result != m_currentSpline &&
            currentDistance <= MaxSplineDistance &&
            currentDistance <= minDistance + m_splineSwitchMargin)
        {
            result = m_currentSpline;
            resultT = currentT;
        }

        return true;
    }


    // Playerの移動方向とSplineの向きを比較
    private void UpdateMovementDirection(
        Vector3 splineDirection,
        bool splineChanged,
        Vector2 moveInput)
    {
        // 別Splineに切り替わった場合
        if (splineChanged)
        {
            // 現在向かおうとしているカメラ方向を基準にする
            float referenceYaw = m_isRotating
                ? m_targetYaw
                : m_yaw;

            Vector3 referenceDirection =
                Quaternion.Euler(0.0f, referenceYaw, 0.0f)
                * Vector3.forward;

            m_isReverseDirection =
                Vector3.Dot(referenceDirection, splineDirection) < 0.0f;
        }

        float deadZoneSqr =
            m_inputDeadZone * m_inputDeadZone;

        bool isMoving =
            moveInput.sqrMagnitude > deadZoneSqr;

        bool wasMoving =
            m_previousMoveInput.sqrMagnitude > deadZoneSqr;

        // 移動入力が変化したか確認
        bool inputChanged = false;

        if (isMoving && wasMoving)
        {
            float inputDot = Vector2.Dot(
                moveInput.normalized,
                m_previousMoveInput.normalized
            );

            inputChanged = inputDot < MoveInputDirectionDot;
        }

        // 入力開始時、または入力方向が変化したとき
        if (isMoving && (!wasMoving || inputChanged))
        {
            // カメラのXZ方向を取得
            Quaternion cameraRotation =
                Quaternion.Euler(0.0f, m_yaw, 0.0f);

            Vector3 cameraForward =
                cameraRotation * Vector3.forward;

            Vector3 cameraRight =
                cameraRotation * Vector3.right;

            // 入力から移動予定方向を計算
            Vector3 moveDirection =
                cameraRight * moveInput.x +
                cameraForward * moveInput.y;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                moveDirection.Normalize();

                // Splineの向きと比較
                float dot = Vector3.Dot(
                    moveDirection,
                    splineDirection
                );

                if (Mathf.Abs(dot) > 0.2f)
                {
                    m_isReverseDirection = dot < 0.0f;
                }
            }
        }

        m_previousMoveInput = moveInput;
    }


    private void UpdateFreeRotation(Vector3 movement)
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0.0f)
            return;

        float minDistance = m_minMovementSpeed * deltaTime;
        if (movement.sqrMagnitude > minDistance * minDistance)
        {
            Vector3 forward = m_player.forward;
            m_freeTargetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        if (!m_freeTargetYaw.HasValue)
            return;

        // 現在のカメラ角度から毎フレーム補間し、途中で向きが変わってもその場から追い直す。
        float t = Mathf.Clamp01(deltaTime / Mathf.Max(0.0001f, m_rotationDuration));
        m_yaw = Mathf.LerpAngle(m_yaw, m_freeTargetYaw.Value, EaseOutCubic(t));
        m_cameraTarget.rotation = Quaternion.Euler(m_pitch, m_yaw, 0.0f);
    }


    // 角度差によって回転方法を変更
    private void UpdateRotation(float desiredYaw)
    {
        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(m_yaw, desiredYaw)
        );

        if (!m_isRotating)
        {
            if (angleDifference <= m_snapAngle)
            {
                // 小さい角度は即時回転
                m_yaw = desiredYaw;
            }
            else
            {
                // 大きい角度はEaseOut開始
                StartRotation(desiredYaw);
            }
        }
        else
        {
            float targetDifference = Mathf.Abs(
                Mathf.DeltaAngle(m_targetYaw, desiredYaw)
            );

            if (targetDifference >= m_retargetAngle)
            {
                // 目標方向が大幅に変更された
                StartRotation(desiredYaw);
            }
            else
            {
                // 小さい変化なら補間先のみ更新
                m_targetYaw = desiredYaw;
            }
        }

        if (m_isRotating)
        {
            m_rotationTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                m_rotationTimer /
                Mathf.Max(0.0001f, m_rotationDuration)
            );

            m_yaw = Mathf.LerpAngle(
                m_startYaw,
                m_targetYaw,
                EaseOutCubic(t)
            );

            if (t >= 1.0f)
            {
                m_yaw = m_targetYaw;
                m_isRotating = false;
            }
        }

        // Pitchを維持してYawだけ変更
        m_cameraTarget.rotation =
            Quaternion.Euler(m_pitch, m_yaw, 0.0f);
    }

    private static float EaseOutCubic(float t)
    {
        return 1.0f - Mathf.Pow(1.0f - t, 3.0f);
    }

    private void StartRotation(float targetYaw)
    {
        m_startYaw = m_yaw;
        m_targetYaw = targetYaw;

        m_rotationTimer = 0.0f;
        m_isRotating = true;
    }
}
