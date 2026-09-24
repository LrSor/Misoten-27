using UnityEngine;
using UnityEngine.InputSystem;

public class DevelopmentPlayerInputManager : MonoBehaviour
{
    public static DevelopmentPlayerInputManager Instance { get; private set; }

    [Header("Players")]
    [SerializeField]
    private PlayerInput[] players;

    [Header("Development")]
    [SerializeField]
    private int defaultPlayerIndex = 0;

    [SerializeField]
    private string keyboardMouseScheme = "KeyboardMouse";

    private PlayerInput currentPlayer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 念のため全員停止
        foreach (PlayerInput player in players)
        {
            if (player == null)
            {
                continue;
            }

            player.enabled = false;
        }

        // 最初はPlayer01
        SelectPlayer(defaultPlayerIndex);
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectPlayer(0);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectPlayer(1);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectPlayer(2);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SelectPlayer(3);
        }
    }

    public void SelectPlayer(int index)
    {
        if (index < 0 || index >= players.Length)
        {
            return;
        }

        PlayerInput nextPlayer = players[index];

        if (nextPlayer == null)
        {
            return;
        }

        // 同じPlayerなら何もしない
        if (currentPlayer == nextPlayer)
        {
            return;
        }

        // ------------------------------
        // 今まで操作していたPlayerを解除
        // ------------------------------

        if (currentPlayer != null)
        {
            if (currentPlayer.user.valid)
            {
                currentPlayer.user.UnpairDevices();
            }

            currentPlayer.enabled = false;
        }

        // ------------------------------
        // 新しいPlayerを有効化
        // ------------------------------

        currentPlayer = nextPlayer;

        currentPlayer.enabled = true;

        // Keyboard + Mouseを明示的に割り当て
        if (Keyboard.current != null &&
            Mouse.current != null)
        {
            currentPlayer.SwitchCurrentControlScheme(
                keyboardMouseScheme,
                Keyboard.current,
                Mouse.current
            );
        }

        Debug.Log(
            $"操作Player変更 : Player {index + 1}"
        );
    }
}
