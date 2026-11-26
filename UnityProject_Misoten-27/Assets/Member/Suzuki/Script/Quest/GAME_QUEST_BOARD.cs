using System.Collections.Generic;
using System.Linq;
using Sawada.Cordex;
using UnityEngine;

//==================================================
// 通信シーン用のクエスト
//
// ・シーンにある MiniGameZone（ミニゲームゾーン）のうち、いくつかに「クエスト」を出す
// ・クエストが出ているゾーンだけ、近づいて ZL でミニゲームを始められる
//   （出ていないゾーンは MiniGameZone を止めるので始められない）
// ・ミニゲームをクリアすると、そのクエストは消えて、別のゾーンに次のクエストが出る
// ・どのゾーンに出すかは Host が決めて、全員に同じものが見える
//   （澤田さんの NetworkWorldObject で、ゾーンの ON/OFF を数字 1 つにまとめて送る）
// ・クエストはプレイ中（GAME_MANAGER が PLAYING）だけ出る
//
// ※ 子オブジェクトを持たせないこと（Client では子のスクリプトが止められてしまう）
//   目印（光の柱・文字）は、ゲーム開始時に別のオブジェクトとして自動で作る
//==================================================
public sealed class GAME_QUEST_BOARD : NetworkWorldObject
{
    // ミニゲームごとのクエストの見た目
    [System.Serializable]
    public class QUEST_INFO
    {
        public MiniGameSystem m_system;
        public string m_title = "クエスト";
        public string m_gameName = "ミニゲーム";
        public Color m_color = new Color(1f, 0.8f, 0.2f);
    }

    [Tooltip("同時に出しておくクエストの数")]
    [SerializeField, Min(1)] private int m_activeCount = 3;

    [SerializeField] private QUEST_INFO[] m_quests = new QUEST_INFO[0];

    [Header("目印")]
    [SerializeField] private float m_beamHeight = 30f;
    [SerializeField] private float m_beamWidth = 0.5f;
    [SerializeField] private float m_labelHeight = 4.5f;

    [Tooltip("ゾーンからこの距離まで近づくと「ZLで開始」と出す（澤田さんの通信は 3m）")]
    [SerializeField] private float m_startDistance = 3f;

    private NetworkTestSession m_session;
    private MiniGameZone[] m_zones = new MiniGameZone[0];
    private MARKER[] m_markers = new MARKER[0];

    // ゾーンごとのクエストの ON/OFF（1 ビット = 1 ゾーン）
    private int m_mask;
    private int m_lastCleared = -1;

    // Host：ミニゲームの回ごとに、どのゾーンで始めたか
    private readonly Dictionary<int, int> m_sessionZones = new Dictionary<int, int>();
    private readonly HashSet<int> m_finishedSessions = new HashSet<int>();

    private bool IsHost
    {
        get { return World != null && World.IsHost; }
    }

    protected override int Value
    {
        get { return m_mask; }
        set { m_mask = value; }
    }

    // 外から見る用
    public int ActiveCount
    {
        get { return CountBits(m_mask); }
    }

    //==================================================

    void Start()
    {
        m_session = FindFirstObjectByType<NetworkTestSession>();

        // 全員の PC で同じ順番になるように、ヒエラルキーの場所で並べる
        m_zones = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MiniGameZone>(true))
            .OrderBy(zone => HierarchyKey(zone.transform), System.StringComparer.Ordinal)
            .Take(31)
            .ToArray();

        CreateMarkers();

        if (m_session != null)
        {
            m_session.MiniGameStateChanged += OnMiniGameStateChanged;
        }

        ApplyMask();
    }

    void OnDestroy()
    {
        if (m_session != null)
        {
            m_session.MiniGameStateChanged -= OnMiniGameStateChanged;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (IsHost)
        {
            UpdateHost();
        }
        else if (World == null || m_session == null || m_session.SuzukiLocalId == 0)
        {
            // 通信していないときは出さない
            m_mask = 0;
        }

        ApplyMask();
        UpdateMarkers();
    }

    //==================================================
    // Host：クエストを出す・消す
    //==================================================

    void UpdateHost()
    {
        bool isPlaying =
            GAME_MANAGER.Instance == null || GAME_MANAGER.Instance.IsPlaying;

        if (!isPlaying)
        {
            m_mask = 0;
            return;
        }

        int target = Mathf.Min(m_activeCount, m_zones.Length);

        while (CountBits(m_mask) < target)
        {
            int zone = PickZone();

            if (zone < 0)
            {
                break;
            }

            m_mask |= 1 << zone;
        }
    }

    // まだクエストが出ていないゾーンから、ランダムに 1 つ選ぶ（さっきクリアした所はなるべく避ける）
    int PickZone()
    {
        List<int> candidates = new List<int>();

        for (int i = 0; i < m_zones.Length; i++)
        {
            if ((m_mask & (1 << i)) == 0 &&
                m_zones[i] != null &&
                m_zones[i].System != null &&
                i != m_lastCleared)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0 &&
            m_lastCleared >= 0 &&
            (m_mask & (1 << m_lastCleared)) == 0)
        {
            candidates.Add(m_lastCleared);
        }

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : -1;
    }

    // ミニゲームの状態が変わった（Host で全員分届く）
    void OnMiniGameStateChanged(NetworkTestSession.MiniGamePacket packet)
    {
        if (!IsHost || packet == null)
        {
            return;
        }

        // 始まった：どのクエスト（ゾーン）で始めたかを覚える
        if (packet.m_phase == NetworkTestSession.MiniGamePhase.Countdown &&
            !m_sessionZones.ContainsKey(packet.m_session))
        {
            m_sessionZones[packet.m_session] = FindPlayerZone(packet.m_player, packet.m_game);
        }

        // クリアした：そのクエストを消す（次のクエストは UpdateHost で出る）
        if (packet.m_phase == NetworkTestSession.MiniGamePhase.Result &&
            packet.m_clear &&
            m_finishedSessions.Add(packet.m_session) &&
            m_sessionZones.TryGetValue(packet.m_session, out int zone) &&
            zone >= 0)
        {
            m_mask &= ~(1 << zone);
            m_lastCleared = zone;
        }
    }

    // プレイヤーにいちばん近い、クエストが出ているゾーン（そのミニゲームのもの）
    int FindPlayerZone(int player, int gameId)
    {
        if (m_session == null ||
            !m_session.TryGetPlayerPosition(player, out Vector3 position))
        {
            return -1;
        }

        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < m_zones.Length; i++)
        {
            if ((m_mask & (1 << i)) == 0 ||
                m_zones[i] == null ||
                m_session.GetMiniGameId(m_zones[i].System) != gameId)
            {
                continue;
            }

            float distance = (m_zones[i].transform.position - position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    //==================================================
    // 全員：ゾーンの ON/OFF と目印
    //==================================================

    void ApplyMask()
    {
        for (int i = 0; i < m_zones.Length; i++)
        {
            bool isOn = (m_mask & (1 << i)) != 0;

            // MiniGameZone を止めると、澤田さんの通信でもそのゾーンでは始められなくなる
            if (m_zones[i] != null && m_zones[i].enabled != isOn)
            {
                m_zones[i].enabled = isOn;
            }

            if (i < m_markers.Length && m_markers[i].m_root.activeSelf != isOn)
            {
                m_markers[i].m_root.SetActive(isOn);
            }
        }
    }

    void UpdateMarkers()
    {
        Camera view = FindView();

        bool hasPlayer = false;
        Vector3 playerPosition = Vector3.zero;

        if (m_session != null && m_session.SuzukiLocalId > 0)
        {
            hasPlayer = m_session.TryGetPlayerPosition(m_session.SuzukiLocalId, out playerPosition);
        }

        float time = Time.time;

        for (int i = 0; i < m_markers.Length; i++)
        {
            MARKER marker = m_markers[i];

            if (!marker.m_root.activeSelf)
            {
                continue;
            }

            // 「！」を上下にふわふわ回す
            marker.m_mark.transform.localPosition =
                new Vector3(0f, m_labelHeight + 1.6f + Mathf.Sin(time * 2.5f + i) * 0.25f, 0f);

            // 近くにいるときだけ「ZL で開始」
            bool isNear = hasPlayer &&
                          Vector3.Distance(playerPosition, m_zones[i].transform.position) <= m_startDistance;

            marker.m_label.text = isNear
                ? marker.m_title + "\n<color=#ffe066>ZL で開始！</color>"
                : marker.m_title;

            // 文字はカメラの方を向ける
            if (view != null)
            {
                Quaternion face = Quaternion.LookRotation(marker.m_label.transform.position - view.transform.position);
                marker.m_label.transform.rotation = face;
                marker.m_mark.transform.rotation = face;
            }
        }
    }

    static Camera FindView()
    {
        foreach (Camera camera in Camera.allCameras)
        {
            if (camera.enabled && camera.targetTexture == null)
            {
                return camera;
            }
        }

        return null;
    }

    //==================================================
    // 目印を作る（光の柱・足元の輪・「！」・クエスト名）
    //==================================================

    class MARKER
    {
        public GameObject m_root;
        public TextMesh m_label;
        public TextMesh m_mark;
        public string m_title;
    }

    void CreateMarkers()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        // このオブジェクトの子にはしない（子のスクリプト・当たり判定は Client で止められるため）
        Transform parent = new GameObject("QuestMarkers (自動作成)").transform;

        m_markers = new MARKER[m_zones.Length];

        for (int i = 0; i < m_zones.Length; i++)
        {
            QUEST_INFO info = FindInfo(m_zones[i].System);
            Color color = info != null ? info.m_color : new Color(1f, 0.8f, 0.2f);

            GameObject root = new GameObject("QuestMarker_" + m_zones[i].name);
            root.transform.SetParent(parent, false);
            root.transform.position = m_zones[i].transform.position;

            Material material = unlit != null ? new Material(unlit) : null;

            if (material != null)
            {
                material.color = color;
            }

            // 光の柱（遠くからでも場所が分かるように）
            CreatePart(root.transform, PrimitiveType.Cylinder, "Beam",
                new Vector3(0f, m_beamHeight * 0.5f, 0f),
                new Vector3(m_beamWidth, m_beamHeight * 0.5f, m_beamWidth), material);

            // 足元の輪（この中に入ると始められる）
            CreatePart(root.transform, PrimitiveType.Cylinder, "Ring",
                new Vector3(0f, 0.03f, 0f),
                new Vector3(m_startDistance * 2f, 0.02f, m_startDistance * 2f), material);

            string title = info != null
                ? info.m_title + "\n<size=60>（" + info.m_gameName + "）</size>"
                : "クエスト\n<size=60>（" + m_zones[i].System?.name + "）</size>";

            TextMesh label = CreateText(root.transform, "Label", title, 80, Color.white, font);
            label.transform.localPosition = new Vector3(0f, m_labelHeight, 0f);

            TextMesh mark = CreateText(root.transform, "Mark", "！", 200, color, font);

            root.SetActive(false);

            m_markers[i] = new MARKER
            {
                m_root = root,
                m_label = label,
                m_mark = mark,
                m_title = title
            };
        }
    }

    static void CreatePart(Transform parent, PrimitiveType type, string name,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;

        // 当たり判定はいらない
        Collider collider = part.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    static TextMesh CreateText(Transform parent, string name, string text, int size, Color color, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = size;
        mesh.characterSize = 0.05f;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        mesh.richText = true;

        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return mesh;
    }

    QUEST_INFO FindInfo(MiniGameSystem system)
    {
        if (system == null || m_quests == null)
        {
            return null;
        }

        foreach (QUEST_INFO info in m_quests)
        {
            if (info != null && info.m_system == system)
            {
                return info;
            }
        }

        return null;
    }

    //==================================================
    // 補助
    //==================================================

    static int CountBits(int value)
    {
        int count = 0;

        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    static string HierarchyKey(Transform t)
    {
        return t.parent == null
            ? t.GetSiblingIndex() + ":" + t.name
            : HierarchyKey(t.parent) + "/" + t.GetSiblingIndex() + ":" + t.name;
    }
}
