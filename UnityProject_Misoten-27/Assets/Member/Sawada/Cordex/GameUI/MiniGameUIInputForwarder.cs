using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>入力取得とミニゲーム処理を分離するための接続例。専用の InputAction を所有する。</summary>
public sealed class MiniGameUIInputForwarder : MonoBehaviour
{
    [SerializeField] private MiniGameUIBase m_target;
    [Tooltip("Vector2 入力。必要に応じて Inspector でデバイスの binding を変更する。")]
    [SerializeField] private InputAction m_vector2Input = CreateVector2Input();
    [SerializeField] private InputAction m_buttonInput = CreateButtonInput();

    private static InputAction CreateVector2Input()
    {
        var action = new InputAction("MiniGameVector2", InputActionType.Value, expectedControlType: "Vector2");
        action.AddBinding("<Gamepad>/leftStick");
        action.AddBinding("<Mouse>/delta");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        return action;
    }

    private static InputAction CreateButtonInput()
    {
        var action = new InputAction("MiniGameButton", InputActionType.Button);
        action.AddBinding("<Gamepad>/buttonSouth");
        // 既存 JoyconInputBridge の仮想 Gamepad は SL を leftShoulder に渡す。
        action.AddBinding("<Gamepad>/leftShoulder");
        action.AddBinding("<Keyboard>/space");
        action.AddBinding("<Mouse>/leftButton");
        return action;
    }

    private void Awake()
    {
        if (m_target == null) m_target = GetComponent<MiniGameUIBase>();
    }

    private void OnEnable()
    {
        m_vector2Input.Enable();
        m_buttonInput.Enable();
    }

    private void Update()
    {
        if (m_target == null) return;
        m_target.ReceiveVector2Input(m_vector2Input.ReadValue<Vector2>());
        if (m_buttonInput.WasPressedThisFrame()) m_target.ReceiveButtonInput(true);
        if (m_buttonInput.WasReleasedThisFrame()) m_target.ReceiveButtonInput(false);
    }

    private void OnDisable()
    {
        if (m_target != null)
        {
            m_target.ReceiveVector2Input(Vector2.zero);
            m_target.ReceiveButtonInput(false);
        }
        m_vector2Input.Disable();
        m_buttonInput.Disable();
    }

    private void OnDestroy()
    {
        m_vector2Input.Dispose();
        m_buttonInput.Dispose();
    }
}
