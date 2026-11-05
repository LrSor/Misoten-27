using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Feeds JoyconLib input through the player's existing Input Actions.</summary>
[DisallowMultipleComponent]
public sealed class JoyconInputBridge : MonoBehaviour
{
    private const string ControlScheme = "Joycon";
    private static readonly Dictionary<Joycon, JoyconInputBridge> Owners = new();

    [SerializeField] private PlayerInput m_playerInput;
    [Tooltip("-1: first unassigned Joy-Con; otherwise index in JoyconManager.j")]
    [SerializeField] private int m_joyconIndex = -1;
    [Tooltip("Horizontal grip, matching Takahashi's Player movement axes")]
    [SerializeField] private bool m_horizontalGrip = true;
    [SerializeField] private Joycon.Button m_sprintButton = Joycon.Button.SL;
    [SerializeField] private Joycon.Button m_swapButton = Joycon.Button.SR;

    private Joycon m_source;
    private Gamepad m_virtualPad;
    private bool m_previousAutoSwitch;
    private string m_previousScheme;
    private InputDevice[] m_previousDevices;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOwners() => Owners.Clear();

    private void Awake()
    {
        if (m_playerInput == null) m_playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable() => InputSystem.onBeforeUpdate += FeedInput;

    private void OnDisable()
    {
        InputSystem.onBeforeUpdate -= FeedInput;
        Release();
    }

    private void FeedInput()
    {
        // Do not enqueue gameplay input during Editor or before-render updates.
        if (!Application.isPlaying ||
            (InputState.currentUpdateType != InputUpdateType.Dynamic &&
             InputState.currentUpdateType != InputUpdateType.Fixed)) return;

        if (m_playerInput == null || !m_playerInput.isActiveAndEnabled)
        {
            Release();
            return;
        }

        var manager = JoyconManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled || manager.j == null)
        {
            Release();
            return;
        }

        if (m_source != null &&
            (!manager.j.Contains(m_source) || m_source.state < Joycon.state_.ATTACHED))
            Release();

        if (m_source == null)
        {
            for (int i = 0; i < manager.j.Count; i++)
            {
                var candidate = manager.j[i];
                if ((m_joyconIndex >= 0 && m_joyconIndex != i) || candidate == null ||
                    candidate.state < Joycon.state_.ATTACHED || Owners.ContainsKey(candidate)) continue;
                Connect(candidate);
                break;
            }
        }

        if (m_source == null || m_virtualPad == null) return;

        // PlayerInput can be re-enabled or reassigned by the development selector.
        if (m_playerInput.currentControlScheme != ControlScheme || !IsPaired(m_virtualPad))
            PairDevices();

        InputSystem.QueueStateEvent(m_virtualPad,
            ReadState(m_source, m_horizontalGrip, m_sprintButton, m_swapButton));
    }

    private static GamepadState ReadState(Joycon source, bool horizontalGrip,
        Joycon.Button sprintButton, Joycon.Button swapButton)
    {
        var raw = source.GetStick();
        var move = new Vector2(raw[0], raw[1]);
        if (horizontalGrip)
            move = new Vector2(raw[1], source.isLeft ? raw[0] : -raw[0]);

        var state = new GamepadState { leftStick = Vector2.ClampMagnitude(move, 1f) };
        state = state.WithButton(GamepadButton.LeftShoulder, source.GetButton(sprintButton));
        return state.WithButton(GamepadButton.RightShoulder, source.GetButton(swapButton));
    }

    private void Connect(Joycon source)
    {
        if (m_playerInput.actions == null ||
            !m_playerInput.actions.FindControlScheme(ControlScheme).HasValue)
        {
            Debug.LogWarning("JoyconInputBridge requires the Joycon control scheme in PlayerControls.", this);
            enabled = false;
            return;
        }

        m_previousScheme = m_playerInput.currentControlScheme;
        m_previousAutoSwitch = m_playerInput.neverAutoSwitchControlSchemes;
        m_previousDevices = new InputDevice[m_playerInput.devices.Count];
        for (int i = 0; i < m_previousDevices.Length; i++)
            m_previousDevices[i] = m_playerInput.devices[i];

        m_source = source;
        Owners.Add(source, this);
        // Suppress automatic scheme switching before adding the virtual device.
        m_playerInput.neverAutoSwitchControlSchemes = true;
        m_virtualPad = InputSystem.AddDevice<Gamepad>("JoyconVirtualGamepad");
        PairDevices();
    }

    private bool IsPaired(InputDevice device)
    {
        foreach (var paired in m_playerInput.devices)
            if (paired == device) return true;
        return false;
    }

    private void PairDevices()
    {
        var devices = new List<InputDevice> { m_virtualPad };
        // Keep existing keyboard/mouse ownership; in single-player also allow
        // them when the player initially started in the Gamepad scheme.
        if (Keyboard.current != null && (IsPaired(Keyboard.current) || PlayerInput.all.Count == 1))
            devices.Add(Keyboard.current);
        if (Mouse.current != null && (IsPaired(Mouse.current) || PlayerInput.all.Count == 1))
            devices.Add(Mouse.current);
        m_playerInput.SwitchCurrentControlScheme(ControlScheme, devices.ToArray());
    }

    private void Release()
    {
        if (m_source != null) Owners.Remove(m_source);
        m_source = null;
        if (m_virtualPad == null) return;

        // Removing a device clears its held state as well as its pairing.
        if (m_virtualPad.added) InputSystem.RemoveDevice(m_virtualPad);
        m_virtualPad = null;
        if (m_playerInput != null)
        {
            if (m_playerInput.isActiveAndEnabled && m_playerInput.user.valid)
            {
                var devices = new List<InputDevice>();
                foreach (var device in m_previousDevices)
                    if (device.added) devices.Add(device);
                // A player starting with no physical gamepad may have no saved
                // devices. Restore keyboard/mouse rather than an empty scheme.
                if (devices.Count == 0 && Keyboard.current != null && Mouse.current != null)
                    m_playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current, Mouse.current);
                else if (!string.IsNullOrEmpty(m_previousScheme))
                    m_playerInput.SwitchCurrentControlScheme(m_previousScheme, devices.ToArray());
                else
                    m_playerInput.SwitchCurrentControlScheme(devices.ToArray());
            }
            m_playerInput.neverAutoSwitchControlSchemes = m_previousAutoSwitch;
        }
        m_previousDevices = null;
    }
}
