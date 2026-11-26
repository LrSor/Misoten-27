using Sawada.Cordex;
using UnityEngine;
using UnityEngine.UI;

//==================================================
// ゲームシーンの最初に出すロビー画面
//
//   ・インターネット（ルームコード）と LAN（LAN コード / IP）を切り替えられる
//   ・「ルームを作る」→ Host になる。コードが出るので他の人に伝える
//   ・コードを入れて「参加」→ Client になる
//   ・Host が「ゲーム開始」を押すと、全員のカウントダウンが始まる（GAME_SYNC）
//   ・開始されたらロビー画面は消える
//
// 通信は澤田さんの NetworkTestSession をそのまま使っている
// （NetworkTestSession.SuzukiLobby.cs の窓口から呼んでいる）
// UI は Suzuki/ゲームサイクルを今のシーンに追加 で自動で作られる
//==================================================
public class GAME_LOBBY : MonoBehaviour
{
    // プレイヤー 1 人分の枠
    [System.Serializable]
    public class SLOT
    {
        public Image m_back;
        public Image m_strip;
        public Text m_nameText;
        public Text m_roleText;
        public GameObject m_youBadge;
    }

    [Tooltip("空欄ならシーンから自動で探す")]
    [SerializeField] private NetworkTestSession m_session;
    [SerializeField] private GAME_SYNC m_sync;

    [Header("画面")]
    [SerializeField] private GameObject m_panel;

    [Header("接続方法の切り替え")]
    [SerializeField] private Button m_internetTab;
    [SerializeField] private Button m_lanTab;

    [Header("ルーム作成・参加")]
    [SerializeField] private GameObject m_entryGroup;
    [SerializeField] private Button m_createButton;
    [SerializeField] private InputField m_codeInput;
    [SerializeField] private Button m_joinButton;

    [Header("接続後")]
    [SerializeField] private GameObject m_roomGroup;
    [SerializeField] private Text m_codeText;
    [SerializeField] private Button m_copyButton;
    [SerializeField] private Button m_nextIpButton;
    [SerializeField] private Text m_playersText;
    [SerializeField] private Button m_startButton;
    [SerializeField] private Text m_waitText;
    [SerializeField] private Button m_leaveButton;
    [SerializeField] private Text m_leaveText;

    [Header("プレイヤー一覧（P1 ～ P4）")]
    [SerializeField] private SLOT[] m_slots = new SLOT[4];

    [Tooltip("プレイヤーごとの色（P1 ～ P4）")]
    [SerializeField] private Color[] m_playerColors =
    {
        new Color(1f, 0.35f, 0.35f),
        new Color(0.35f, 0.6f, 1f),
        new Color(0.4f, 0.9f, 0.45f),
        new Color(1f, 0.85f, 0.25f)
    };

    [SerializeField] private Color m_slotEmptyColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color m_slotFilledColor = new Color(1f, 1f, 1f, 0.16f);

    [Header("状態")]
    [SerializeField] private Text m_statusText;

    [Header("色")]
    [SerializeField] private Color m_tabOnColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color m_tabOffColor = new Color(0.55f, 0.55f, 0.55f);

    private bool m_isInternet = true;
    private string m_copyText = "";

    void Awake()
    {
        if (m_session == null) m_session = FindFirstObjectByType<NetworkTestSession>();
        if (m_sync == null) m_sync = FindFirstObjectByType<GAME_SYNC>();

        AddClick(m_internetTab, () => SetMode(true));
        AddClick(m_lanTab, () => SetMode(false));
        AddClick(m_createButton, OnCreate);
        AddClick(m_joinButton, OnJoin);
        AddClick(m_copyButton, OnCopy);
        AddClick(m_nextIpButton, () => { if (m_session != null) m_session.SuzukiNextHostIp(); });
        AddClick(m_startButton, OnStart);
        AddClick(m_leaveButton, () => { if (m_session != null) m_session.SuzukiLeave(); });

        if (m_codeInput != null)
        {
            m_codeInput.onEndEdit.AddListener(_ =>
            {
                if (Input_EnterPressed()) OnJoin();
            });
        }

        SetMode(true);
    }

    void Update()
    {
        if (m_session == null)
        {
            SetActiveSafe(m_panel, false);
            return;
        }

        // 開始されたらロビーを閉じる
        bool isStarted = m_sync != null && m_sync.IsStarted;
        SetActiveSafe(m_panel, !isStarted);

        if (isStarted)
        {
            return;
        }

        // ロビー中はマウスで操作できるようにする
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        bool isBusy = m_session.SuzukiIsBusy;
        bool isConnected = m_session.SuzukiLocalId > 0;
        bool isHost = m_sync != null && m_sync.IsHost;

        // ルーム作成・参加（まだ何もしていないとき）
        SetActiveSafe(m_entryGroup, !isBusy);
        SetInteractable(m_internetTab, !isBusy);
        SetInteractable(m_lanTab, !isBusy);

        // 接続後
        SetActiveSafe(m_roomGroup, isBusy);

        if (isBusy)
        {
            UpdateRoom(isConnected, isHost);
        }

        UpdateSlots(isConnected);

        SetText(m_statusText, m_session.SuzukiStatus);
    }

    // P1 ～ P4 の枠（参加している人を色付きで出す。P1 がいつも Host）
    void UpdateSlots(bool isConnected)
    {
        if (m_slots == null)
        {
            return;
        }

        for (int i = 0; i < m_slots.Length; i++)
        {
            SLOT slot = m_slots[i];

            if (slot == null)
            {
                continue;
            }

            int id = i + 1;
            bool isIn = isConnected && m_session.SuzukiHasPlayer(id);
            bool isYou = isConnected && m_session.SuzukiLocalId == id;
            Color color = i < m_playerColors.Length ? m_playerColors[i] : Color.white;

            if (slot.m_back != null)
            {
                slot.m_back.color = isIn ? m_slotFilledColor : m_slotEmptyColor;
            }

            if (slot.m_strip != null)
            {
                slot.m_strip.color = isIn ? color : new Color(color.r, color.g, color.b, 0.15f);
            }

            if (slot.m_nameText != null)
            {
                SetText(slot.m_nameText, "P" + id);
                slot.m_nameText.color = isIn ? color : new Color(1f, 1f, 1f, 0.25f);
            }

            if (slot.m_roleText != null)
            {
                string role = !isIn ? "募集中…" : id == 1 ? "Host" : "参加中";
                SetText(slot.m_roleText, role);
                slot.m_roleText.color = isIn ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }

            SetActiveSafe(slot.m_youBadge, isYou);
        }
    }

    void UpdateRoom(bool isConnected, bool isHost)
    {
        // コード（Host のときだけ）
        string code = "";
        string ipLine = "";

        if (isHost)
        {
            if (m_isInternet)
            {
                code = m_session.SuzukiRoomCode;
                SetText(m_codeText, code.Length > 0
                    ? "ルームコード\n<size=72><b>" + code + "</b></size>"
                    : "ルームを準備中…");
            }
            else
            {
                code = m_session.SuzukiLanCode;
                ipLine = m_session.SuzukiHostIp;
                SetText(m_codeText, code.Length > 0
                    ? "LANコード\n<size=48><b>" + code + "</b></size>\nIP：" + ipLine
                    : "この PC の IP が見つかりません（Wi-Fi / 有線を確認）");
            }
        }
        else
        {
            SetText(m_codeText, isConnected ? "ルームに参加しました" : "接続中…");
        }

        m_copyText = code;
        SetActiveSafe(m_copyButton, isHost && code.Length > 0);
        SetActiveSafe(m_nextIpButton, isHost && !m_isInternet && ipLine.Length > 0);

        // 人数
        SetText(m_playersText, isConnected
            ? "参加人数  " + m_session.SuzukiPlayerCount + " / 4"
            : "");

        // 開始ボタン（Host だけ）
        SetActiveSafe(m_startButton, isHost && isConnected);
        SetActiveSafe(m_waitText, !isHost && isConnected);

        SetText(m_leaveText, isConnected ? (isHost ? "ルームを閉じる" : "退出する") : "キャンセル");
    }

    //==================================================
    // ボタン
    //==================================================

    void SetMode(bool isInternet)
    {
        m_isInternet = isInternet;

        SetTabColor(m_internetTab, isInternet);
        SetTabColor(m_lanTab, !isInternet);

        if (m_codeInput != null)
        {
            Text placeholder = m_codeInput.placeholder as Text;

            if (placeholder != null)
            {
                placeholder.text = isInternet
                    ? "ルームコードを入力"
                    : "LANコード または IP（例 192.168.0.10）";
            }
        }
    }

    void OnCreate()
    {
        if (m_session == null) return;

        if (m_isInternet) m_session.SuzukiCreateInternetRoom();
        else m_session.SuzukiCreateLanRoom();
    }

    void OnJoin()
    {
        if (m_session == null || m_session.SuzukiIsBusy) return;

        string code = m_codeInput != null ? m_codeInput.text.Trim() : "";

        if (code.Length == 0) return;

        if (m_isInternet) m_session.SuzukiJoinInternetRoom(code);
        else m_session.SuzukiJoinLan(code);
    }

    void OnCopy()
    {
        if (m_copyText.Length > 0)
        {
            GUIUtility.systemCopyBuffer = m_copyText;
        }
    }

    void OnStart()
    {
        if (m_sync != null)
        {
            m_sync.HostStart();
        }
    }

    //==================================================
    // 補助
    //==================================================

    static bool Input_EnterPressed()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;

        return keyboard != null &&
               (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
    }

    void SetTabColor(Button tab, bool isOn)
    {
        if (tab == null) return;

        Image image = tab.targetGraphic as Image;

        if (image != null)
        {
            image.color = isOn ? m_tabOnColor : m_tabOffColor;
        }
    }

    static void AddClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    static void SetText(Text text, string value)
    {
        if (text != null && text.text != value) text.text = value;
    }

    static void SetInteractable(Button button, bool value)
    {
        if (button != null) button.interactable = value;
    }

    static void SetActiveSafe(Component target, bool value)
    {
        if (target != null && target.gameObject.activeSelf != value) target.gameObject.SetActive(value);
    }

    static void SetActiveSafe(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value) target.SetActive(value);
    }
}
