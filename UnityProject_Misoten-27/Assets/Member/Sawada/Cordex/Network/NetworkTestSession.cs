using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sawada.Cordex
{
    [DefaultExecutionOrder(-10000)]
    public sealed class NetworkTestSession : MonoBehaviour
    {
        const string Protocol = "SawadaNetworkTest/6";
        const float Timeout = 10f;
        const float NetworkInterval = .05f;
        const float FastSnapshotInterval = 1f / 30f;
        const float RemoteInterpolationDelay = .1f;
        const float DefaultOwnerInterpolationDelay = .05f;
        const int MaxMoveBatch = 16;
        const int MaxQueuedMoves = 8;
        [SerializeField] GameObject playerPrefab;
        [SerializeField] BaseArmorSO[] initialArmorSlots = Array.Empty<BaseArmorSO>();
        [SerializeField] GameObject[] worldPrefabs = Array.Empty<GameObject>();
        NetworkWorld world;
        float worldSendAt, localWorldActionAt;
        [Serializable] public class PlayerState
        {
            public int id;
            public float x, y, z, yaw;
            public Vector3 velocity;
            public int appliedMoveSequence;
            public bool sprinting;
            public int tackleHitSequence;
            public int selectedArmor;
            public int[] armor;
        }
        [Serializable] public class Message
        {
            public string kind, protocol, reason, catalog, action;
            public int id, tick;
            public int request, target, part, parts;
            public bool accepted;
            public NetworkWorld.State[] world;
            public int firstMoveSequence;
            public WalkCommand[] moves;
            public float time;
            public int armorStep, swapPress;
            public PlayerState[] players;
        }
        [Serializable] public struct WalkCommand
        {
            public Vector2 move, forward;
            public float sprintSteer;
            public bool sprint;
            public int sprintPress;
        }

        struct MoveCommand
        {
            public int Sequence;
            public WalkCommand Input;
        }

        sealed class Connection
        {
            public INetworkTestPeer peer;
            public int id;
            public float lastReceive, lastInput, closeAt;
            public Vector2 input;
            public float sprintSteer;
            public readonly Queue<MoveCommand> moves = new Queue<MoveCommand>(MaxQueuedMoves);
            public int lastReceivedMove, lastAppliedMove, lastReceivedSprintPress;
            public bool sprint;
            public Vector2 forward = Vector2.up;
            public int sprintPress, appliedSprintPress, armorStep, appliedArmorStep, swapPress, appliedSwapPress;
            public int lastRequest;
            public float actionAt;
        }
        readonly List<Connection> connections = new List<Connection>();
        readonly Dictionary<int, PlayerState> states = new Dictionary<int, PlayerState>();
        readonly Dictionary<int, GameObject> avatars = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Rigidbody> bodies = new Dictionary<int, Rigidbody>();
        readonly Dictionary<int, PlayerMoveController> movers = new Dictionary<int, PlayerMoveController>();
        readonly Dictionary<int, Inventory> inventories = new Dictionary<int, Inventory>();
        readonly Dictionary<int, PlayerSwapController> swappers = new Dictionary<int, PlayerSwapController>();
        FieldArmorMachine[] fieldArmors;
        BaseArmorSO[] armorCatalog;
        readonly List<Message> snapshots = new List<Message>();
        float remoteRenderTime, ownerRenderTime;
        float ownerInterpolationDelay = DefaultOwnerInterpolationDelay;
        float snapshotReceivedAt;
        bool renderTimeReady;
        TcpListener listener;
        INetworkTestPeer server;
        RelayTestTransport relay;
        bool internetMode = true, relayStarting, relayConnecting, roomAnnounced;
        int relayGeneration;
        float relayStarted;
        string joinCodeInput = "";
        string lanCodeInput = "", hostLanCode = "";
        bool Busy => hosting || connecting || server != null || relay != null;
        TcpClient connectingSocket;
        Task connectTask;
        Camera view;
        Camera lobbyView;
        TestPlayerCameraLookController localLook;
        PlayerInput localPlayerInput;
        JoyconInputBridge localJoyconBridge;
        Transform inactivePlayers;
        string ip = "", portText = "7777", status = "Create an online room or enter a room code to join";
        IPAddress[] localAddresses = Array.Empty<IPAddress>();
        int lanAddressIndex;
        int localId, tick, receivedTick;
        bool hosting, connecting, testMotion, testGameplay, testPickup;
        float started, lastReceive, sendAt, snapshotAt, connectStarted, exitAfter;
        Vector2 localInput;
        float localSprintSteer;
        bool localSprint;
        Vector2 localForward = Vector2.up;
        int localSprintPress, appliedLocalSprintPress, localArmorStep, appliedLocalArmorStep;
        int localSwapPress, appliedLocalSwapPress;
        StreamWriter trace;
        readonly List<WalkCommand> unsentMoves = new List<WalkCommand>(MaxMoveBatch);
        LocalWalkPrediction walkPrediction;
        int moveSequence, reconciledTick, appliedMoveSequence;
        bool disablePrediction, tracePrediction;
        bool ownerPoseReady, ownerPosePredicted, ownerPoseTransition;
        bool bufferedInput, fastSnapshots, testInputEdges, sampledMoving;
        int unsentEdgeSequence;

        [Serializable] sealed class PredictionSample
        {
            public string kind = "prediction";
            public float time;
            public float ownerRenderTime, remoteRenderTime, newestSnapshotTime, snapshotAge;
            public Vector2 input;
            public Vector3 position;
            public int sequence, appliedSequence;
            public bool predicted;
        }

        [Serializable] sealed class InputSendSample
        {
            public string kind = "input-sent";
            public float time;
            public int firstSequence, count, edgeSequence, bytes;
        }

        void Awake()
        {
            string HierarchyKey(Transform item) => item.parent == null ? item.name
                : HierarchyKey(item.parent) + "/" + item.GetSiblingIndex() + ":" + item.name;
            fieldArmors = FindObjectsByType<FieldArmorMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(f => f.gameObject.scene == gameObject.scene)
                .OrderBy(f => HierarchyKey(f.transform), StringComparer.Ordinal).ToArray();
            armorCatalog = initialArmorSlots.Concat(fieldArmors.Select(f => f.GetArmorSO()))
                .Where(a => a != null).Distinct().ToArray();
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            if (!Application.isEditor) Screen.SetResolution(900, 650, FullScreenMode.Windowed);
            // The map's single-player cameras must not compete with the owned camera.
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            foreach (var audio in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) audio.enabled = false;
            var staging = new GameObject("Inactive network players");
            staging.transform.SetParent(transform, false);
            staging.SetActive(false);
            inactivePlayers = staging.transform;
            try { world = new NetworkWorld(this, worldPrefabs, inactivePlayers); }
            catch (Exception e) { Debug.LogException(e); enabled = false; return; }
            var sourceCamera = playerPrefab != null
                ? playerPrefab.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == "PlayerCamera") : null;
            if (sourceCamera != null)
            {
                var lobby = Instantiate(sourceCamera.gameObject, inactivePlayers);
                lobby.SetActive(false);
                foreach (var script in lobby.GetComponents<MonoBehaviour>()) script.enabled = false;
                lobbyView = lobby.GetComponent<Camera>();
                lobbyView.targetDisplay = 0;
                lobbyView.targetTexture = null;
                lobbyView.enabled = true;
                lobby.transform.SetParent(transform, false);
                lobby.transform.SetPositionAndRotation(sourceCamera.transform.position,
                    sourceCamera.transform.rotation);
                lobby.SetActive(true);
                view = lobbyView;
            }
            started = Time.realtimeSinceStartup;
        }

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string key, string fallback = "")
            {
                int i = Array.IndexOf(args, key);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }
            ip = Arg("--net-ip", ip);
            portText = Arg("--net-port", portText);
            RefreshLocalAddresses();
            testMotion = args.Contains("--net-test-motion");
            testGameplay = args.Contains("--net-test-gameplay");
            testPickup = args.Contains("--net-test-pickup");
            disablePrediction = args.Contains("--net-disable-prediction");
            tracePrediction = args.Contains("--net-trace-prediction");
            if (Arg("--net-owner-interpolation-ms") == "100") ownerInterpolationDelay = RemoteInterpolationDelay;
            bufferedInput = args.Contains("--net-buffer-input");
            fastSnapshots = Arg("--net-snapshot-rate") == "30";
            testInputEdges = args.Contains("--net-test-input-edges");
            float.TryParse(Arg("--net-exit-after", "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out exitAfter);
            string tracePath = Arg("--net-trace");
            if (!string.IsNullOrEmpty(tracePath))
            {
                try { trace = new StreamWriter(tracePath) { AutoFlush = true }; }
                catch (Exception e) { Debug.LogWarning("Trace: " + e.Message); }
            }
            joinCodeInput = Arg("--net-code", joinCodeInput);
            lanCodeInput = Arg("--net-lan-code", lanCodeInput);
            string mode = Arg("--net-mode");
            if (mode == "host") { internetMode = false; StartHost(); }
            else if (mode == "client")
            {
                internetMode = false;
                if (lanCodeInput.Length > 0) StartLanCodeClient();
                else StartClient();
            }
            else if (mode == "relay-host") StartRelayHost();
            else if (mode == "relay-client") StartRelayClient();
        }

        bool ReadPort(out int port)
        {
            if (int.TryParse(portText, out port) && port > 0 && port <= 65535) return true;
            SetStatus("Invalid port: enter 1-65535");
            return false;
        }

        void RefreshLocalAddresses()
        {
            try
            {
                // List candidates rather than choosing a VPN/virtual adapter as the Host address.
                localAddresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => LanRoomCode.CanEncodeAddress(a) && !IPAddress.IsLoopback(a))
                    .Distinct().OrderBy(a => a.ToString(), StringComparer.Ordinal).ToArray();
            }
            catch (Exception e)
            {
                localAddresses = Array.Empty<IPAddress>();
                Debug.LogWarning("[NetworkTest] IPv4 lookup: " + e.Message);
            }
            lanAddressIndex = 0;
            UpdateHostLanCode();
        }

        void UpdateHostLanCode()
        {
            hostLanCode = hosting && listener != null && localAddresses.Length > 0
                ? LanRoomCode.Encode(localAddresses[lanAddressIndex], ((IPEndPoint)listener.LocalEndpoint).Port)
                : "";
        }

        public void StartHost()
        {
            if (Busy || !ReadPort(out int port)) return;
            try
            {
                RefreshLocalAddresses();
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(8);
                hosting = true;
                world.SetHost(true);
                localId = 1;
                states.Add(1, SpawnState(1));
                UpdateHostLanCode();
                SetStatus("LAN Host ready on TCP port " + port + ". Share your LAN code with Clients.");
            }
            catch (Exception e) { StopSession("Host failed: " + e.Message); }
        }

        public void StartClient()
        {
            if (Busy || !ReadPort(out int port)) return;
            if (string.IsNullOrWhiteSpace(ip))
            { SetStatus("Enter the Host PC's LAN IPv4 (example: 192.168.1.20)"); return; }
            if (!IPAddress.TryParse(ip.Trim(), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            { SetStatus("Invalid IPv4 address"); return; }
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast))
            { SetStatus("Enter a Host IPv4 address, not 0.0.0.0 or 255.255.255.255"); return; }
            ConnectLan(address, port);
        }

        void StartLanCodeClient()
        {
            if (Busy) return;
            if (!LanRoomCode.TryDecode(lanCodeInput, out var address, out int port))
            { SetStatus("Invalid LAN code. Paste the full LAN-XXXX-XXXX-XXXX code from the Host."); return; }
            ConnectLan(address, port);
        }

        void ConnectLan(IPAddress address, int port)
        {
            connectingSocket = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                connectTask = connectingSocket.ConnectAsync(address, port);
                connecting = true;
                connectStarted = Time.realtimeSinceStartup;
                SetStatus("Connecting to " + address + ":" + port);
            }
            catch (Exception e) { StopSession("Connect failed: " + e.Message); }
        }

        public void StartRelayHost() => StartRelay(true);
        public void StartRelayClient() => StartRelay(false);

        async void StartRelay(bool asHost)
        {
            if (Busy) return;
            string code = joinCodeInput.Trim().ToUpperInvariant();
            if (!asHost && (code.Length == 0 || code.Length > 16
                || code.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))))
            { SetStatus("Enter the room code provided by the Host (letters / digits)."); return; }
            int generation = ++relayGeneration;
            var transport = new RelayTestTransport();
            relay = transport;
            relayStarting = true;
            relayStarted = Time.realtimeSinceStartup;
            SetStatus(asHost ? "Creating an online room..." : "Joining online room...");
            try
            {
                if (asHost) await transport.StartHostAsync();
                else await transport.StartClientAsync(code);
                // A canceled / superseded request must never revive a stopped session.
                if (this == null || generation != relayGeneration) { transport.Dispose(); return; }
                relayStarting = false;
                relayStarted = Time.realtimeSinceStartup;
                if (asHost)
                {
                    hosting = true;
                    world.SetHost(true);
                    localId = 1;
                    states.Add(1, SpawnState(1));
                    SetStatus("Room created; connecting to Relay...");
                }
                else
                {
                    relayConnecting = true;
                    SetStatus("Connecting to the room Host...");
                }
            }
            catch (Exception e)
            {
                transport.Dispose();
                if (this != null && generation == relayGeneration)
                    StopSession("Online connection failed: " + e.Message
                        + " Check internet access and Unity Cloud Authentication / Relay configuration.");
            }
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (exitAfter > 0 && now - started >= exitAfter) { Application.Quit(); return; }
            if (relay != null)
            {
                try { relay.Tick(); }
                catch (Exception e) { StopSession("Relay failed: " + e.Message); return; }
                if (relay.Error != null) { StopSession(relay.Error); return; }
                if (hosting && !roomAnnounced && relay.Ready)
                {
                    roomAnnounced = true;
                    SetStatus("Online room ready. Share the room code with other players.");
                    Debug.Log("[NetworkTest] ROOM CODE " + relay.JoinCode);
                }
                if (relayConnecting && relay.ClientConnected)
                {
                    server = relay.Client;
                    relayConnecting = false;
                    lastReceive = now;
                    Send(server, new Message { kind = "hello", protocol = Protocol, catalog = world.Catalog });
                    SetStatus("Connected; waiting for Host approval");
                }
                if ((relayStarting || relayConnecting || (hosting && !roomAnnounced)) && now - relayStarted > 45f)
                { StopSession("Online connection timeout. Check internet access and create / join the room again."); return; }
                if (relayConnecting && relay.Client != null && relay.Client.Closed)
                { StopSession("Could not reach the room Host. Create / join the room again."); return; }
            }
            if (connecting)
            {
                if (connectTask.IsCompleted)
                {
                    if (connectTask.IsFaulted || connectTask.IsCanceled)
                        StopSession("Connect failed: " + (connectTask.Exception?.GetBaseException().Message ?? "canceled"));
                    else
                    {
                        server = new TcpPeer(connectingSocket);
                        connectingSocket = null;
                        connecting = false;
                        lastReceive = now;
                        Send(server, new Message { kind = "hello", protocol = Protocol, catalog = world.Catalog });
                        SetStatus("Connected; waiting for Host approval");
                    }
                }
                else if (now - connectStarted > Timeout)
                    StopSession("Connect timeout: check Host IP / port, same LAN and Host firewall (TCP).");
            }
            ReadLocalInput(now);
            if (hosting) world.TickHost();
            if (hosting) PollHost(now);
            else if (server != null) PollClient(now);
            UpdateAvatars();
            // PositionConstraint evaluates before LateUpdate. Feed it the current rendered pose.
            RenderClientAvatars(now);
        }

        void ReadLocalInput(float now)
        {
            localInput = Vector2.zero;
            localSprintSteer = 0f;
            localSprint = false;
            if (localId == 0) return;
            if (testPickup)
            {
                float elapsed = now - started;
                localSwapPress = elapsed >= 4f ? 2 : elapsed >= 1f ? 1 : 0;
                localArmorStep = elapsed >= 6f ? 2 : elapsed >= 3f ? 1 : 0;
                return;
            }
            if (testMotion)
            {
                // Explicit verification mode only: alternate direction every 2 seconds.
                float sign = ((int)((now - started) / 2) % 2 == 0) ? 1 : -1;
                localInput = localId % 2 == 0 ? new Vector2(sign, 0) : new Vector2(0, sign);
                return;
            }
            if (testInputEdges)
            {
                const float period = .73f;
                float elapsed = now - started;
                int cycle = Mathf.FloorToInt(elapsed / period);
                if (elapsed - cycle * period < .34f)
                    localInput = cycle % 2 == 0 ? Vector2.right : Vector2.left;
                return;
            }
            if (testGameplay)
            {
                float elapsed = now - started;
                localInput = localId % 2 == 0 ? Vector2.right : Vector2.up;
                localForward = localInput;
                localSprint = elapsed >= 1f && elapsed < 1.1f;
                localSprintPress = elapsed >= 1f ? 1 : 0;
                localArmorStep = elapsed >= 9f ? 2 : elapsed >= 7f ? 1 : 0;
                return;
            }
            if (!Application.isFocused) return;
            if (localPlayerInput != null && localPlayerInput.isActiveAndEnabled)
            {
                var actions = localPlayerInput.actions;
                localInput = actions.FindAction("Move", true).ReadValue<Vector2>();
                var sprint = actions.FindAction("Sprint", true);
                localSprint = sprint.IsPressed();
                if (sprint.WasPressedThisFrame()) localSprintPress++;
                if (actions.FindAction("Swap", true).WasPressedThisFrame()) localSwapPress++;
            }
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.fKey.wasPressedThisFrame && TryGetPlayerPosition(localId, out var position))
                {
                    var door = world.Nearest(position);
                    if (door != null) door.RequestInteraction();
                }
                if (k.leftArrowKey.wasPressedThisFrame) localArmorStep--;
                else if (k.rightArrowKey.wasPressedThisFrame) localArmorStep++;
            }
            localInput = Vector2.ClampMagnitude(localInput, 1);
            localSprintSteer = localInput.x;
            if (avatars.TryGetValue(localId, out var ownedAvatar))
            {
                var reference = ownedAvatar.transform.Find("CameraTarget");
                if (reference != null)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude > .001f) localForward = new Vector2(forward.x, forward.z);
                }
            }
            if (view != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude > .001f)
                {
                    if (localLook != null &&
                        localLook.TryGetMovementForward(localInput, forward, out Vector3 movementForward))
                        forward = movementForward;

                    Vector3 direction = Vector3.Cross(Vector3.up, forward) * localInput.x + forward * localInput.y;
                    localInput = new Vector2(direction.x, direction.z);
                }
            }
        }

        PlayerState SpawnState(int id)
        {
            var body = playerPrefab != null ? playerPrefab.GetComponentInChildren<Rigidbody>(true) : null;
            if (testPickup && fieldArmors.Length > 0)
            {
                // Explicit test mode only: start overlapping a field item to test the real trigger/E path.
                Vector3 position = fieldArmors[(id - 1) % fieldArmors.Length].transform.position + Vector3.up * 1.2f;
                return new PlayerState { id = id, x = position.x, y = position.y, z = position.z };
            }
            // Prefab assets have no simulated pose in the Editor. Read the saved Transform,
            // otherwise Rigidbody.position can be zero and spawn the capsule inside the floor.
            return new PlayerState { id = id, x = (id - 2.5f) * 3,
                y = body != null ? body.transform.position.y : 1.72f, z = 0 };
        }
        static void Send(INetworkTestPeer peer, Message message) => peer.Send(JsonUtility.ToJson(message));
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        Message Decode(string json)
        {
            try { return JsonUtility.FromJson<Message>(json); }
            catch { return null; }
        }

        void PollHost(float now)
        {
            try
            {
                // Bound work per frame even under a burst of connection attempts.
                for (int i = 0; i < 8; i++)
                {
                    INetworkTestPeer peer;
                    if (relay != null)
                    {
                        if (!relay.TryAccept(out peer)) break;
                    }
                    else
                    {
                        if (listener == null || !listener.Pending()) break;
                        peer = new TcpPeer(listener.AcceptTcpClient());
                    }
                    if (connections.Count >= 8) { peer.Dispose(); continue; }
                    connections.Add(new Connection { peer = peer, lastReceive = now });
                }
            }
            catch (Exception e) { StopSession("Listener failed: " + e.Message); return; }
            foreach (var c in connections.ToArray())
            {
                while (c.closeAt == 0 && c.peer.TryRead(out string json))
                {
                    var m = Decode(json);
                    if (m == null) { Reject(c, "Invalid message", now); break; }
                    if (c.id == 0)
                    {
                        if (m.kind != "hello" || m.protocol != Protocol)
                        { Reject(c, "Protocol mismatch: " + Protocol + " required", now); break; }
                        if (m.catalog != world.Catalog)
                        { Reject(c, "World catalog mismatch: use the same map and build", now); break; }
                        int id = Enumerable.Range(2, 3).FirstOrDefault(n => !states.ContainsKey(n));
                        if (id == 0) { Reject(c, "Room full: maximum 4 players including Host", now); break; }
                        c.id = id;
                        states.Add(id, SpawnState(id));
                        Send(c.peer, new Message { kind = "welcome", id = id, protocol = Protocol });
                        Debug.Log("[NetworkTest] JOIN P" + id);
                    }
                    else if (m.kind == "world-action" && m.request > c.lastRequest
                        && m.target > 0 && (m.action == "interact" || m.action == "delete"))
                    {
                        c.lastRequest = m.request;
                        bool allowed = now >= c.actionAt;
                        c.actionAt = now + .2f;
                        bool accepted = allowed && world.HandleAction(c.id, m.target, m.action);
                        Send(c.peer, new Message { kind = "world-result", request = m.request, target = m.target,
                            accepted = accepted, reason = accepted ? "" : "Out of range, unavailable or not permitted" });
                    }
                    else if (m.kind == "input" && ValidMoveBatch(c, m)
                        && m.swapPress >= c.swapPress && (long)m.swapPress - c.swapPress <= 32
                        && Math.Abs((long)m.armorStep - c.armorStep) <= 32)
                    {
                        // Ownership comes from the socket, never a client-supplied player ID.
                        for (int i = 0; i < m.moves.Length; i++)
                        {
                            if (c.moves.Count == MaxQueuedMoves) c.moves.Dequeue();
                            c.lastReceivedMove = m.firstMoveSequence + i;
                            c.lastReceivedSprintPress = m.moves[i].sprintPress;
                            c.moves.Enqueue(new MoveCommand { Sequence = c.lastReceivedMove, Input = m.moves[i] });
                        }
                        c.armorStep = m.armorStep;
                        c.swapPress = m.swapPress;
                        c.lastInput = now;
                    }
                    else { Reject(c, "Invalid input message", now); break; }
                    c.lastReceive = now;
                }
                if (c.peer.Closed || (c.closeAt > 0 && now >= c.closeAt) || now - c.lastReceive > (c.id == 0 ? 5 : Timeout))
                {
                    c.peer.Dispose();
                    connections.Remove(c);
                    if (c.id != 0) { states.Remove(c.id); Debug.Log("[NetworkTest] LEAVE P" + c.id); }
                }
            }
            UpdateAvatars();
            if (!hosting) return;
            ApplyArmorSelection(1, localArmorStep, ref appliedLocalArmorStep);
            foreach (var c in connections)
                if (c.id > 0 && c.closeAt == 0) ApplyArmorSelection(c.id, c.armorStep, ref c.appliedArmorStep);
            FeedSwap(1, localSwapPress, ref appliedLocalSwapPress);
            foreach (var c in connections)
                if (c.id > 0 && c.closeAt == 0) FeedSwap(c.id, c.swapPress, ref c.appliedSwapPress);
            // Read the simulated pose after physics, never the interpolated render transform.
            foreach (var pair in bodies)
            {
                if (!states.TryGetValue(pair.Key, out var state)) continue;
                Vector3 position = pair.Value.position;
                state.x = position.x; state.y = position.y; state.z = position.z;
                state.yaw = pair.Value.rotation.eulerAngles.y;
                state.velocity = pair.Value.linearVelocity;
                state.sprinting = movers[pair.Key].GetComponent<StateMachine>().IsInState<Player_Sprint>();
                state.tackleHitSequence = movers[pair.Key].TackleHitSequence;
                var inventory = inventories[pair.Key];
                state.selectedArmor = inventory.CurrentArmorIndex;
                state.armor = Enumerable.Range(0, inventory.ArmorSlotCount)
                    .Select(i => Array.IndexOf(armorCatalog, inventory.GetArmorAt(i)) + 1).ToArray();
                // Null is always encoded as 0, even if an initial slot is empty.
                for (int i = 0; i < state.armor.Length; i++)
                    if (inventory.GetArmorAt(i) == null) state.armor[i] = 0;
            }
            if (now >= snapshotAt)
            {
                if (fastSnapshots)
                {
                    // Keep the 30 Hz cadence through small frame timing variations without sending bursts.
                    snapshotAt = snapshotAt <= 0 || now - snapshotAt >= FastSnapshotInterval
                        ? now + FastSnapshotInterval : snapshotAt + FastSnapshotInterval;
                }
                else snapshotAt = now + NetworkInterval;
                foreach (var c in connections)
                    if (states.TryGetValue(c.id, out var state)) state.appliedMoveSequence = c.lastAppliedMove;
                var m = new Message { kind = "state", tick = ++tick, time = Time.fixedUnscaledTime,
                    players = states.Values.OrderBy(p => p.id).ToArray() };
                foreach (var c in connections) if (c.id > 0 && c.closeAt == 0) Send(c.peer, m);
                Trace(m);
            }
            if (now >= worldSendAt)
            {
                worldSendAt = now + .1f;
                foreach (var message in world.Capture())
                {
                    foreach (var c in connections) if (c.id > 0 && c.closeAt == 0) Send(c.peer, message);
                    Trace(message);
                }
            }
        }

        static bool ValidMoveBatch(Connection connection, Message message)
        {
            if (message.moves == null || message.moves.Length == 0 || message.moves.Length > MaxMoveBatch ||
                message.firstMoveSequence <= connection.lastReceivedMove || (long)message.firstMoveSequence + message.moves.Length - 1 > int.MaxValue)
                return false;
            int sprintPress = connection.lastReceivedSprintPress;
            foreach (var command in message.moves)
            {
                if (!ValidInputVector(command.move) || !ValidInputVector(command.forward) ||
                    !Finite(command.sprintSteer) || Mathf.Abs(command.sprintSteer) > 1.01f ||
                    command.sprintPress < sprintPress || (long)command.sprintPress - sprintPress > 32) return false;
                sprintPress = command.sprintPress;
            }
            return true;
        }

        static bool ValidInputVector(Vector2 value)
            => Finite(value.x) && Finite(value.y) && Mathf.Abs(value.x) <= 1.01f && Mathf.Abs(value.y) <= 1.01f;

        void Reject(Connection c, string reason, float now)
        {
            Send(c.peer, new Message { kind = "reject", reason = reason });
            c.closeAt = now + .5f; // Allow the queued rejection to reach the peer before closing.
            Debug.LogWarning("[NetworkTest] REJECT " + reason);
        }

        void PollClient(float now)
        {
            while (server != null && server.TryRead(out string json))
            {
                var m = Decode(json);
                if (m == null) { StopSession("Invalid Host message"); return; }
                lastReceive = now;
                if (m.kind == "reject") { StopSession("Connection rejected: " + m.reason); return; }
                if (m.kind == "welcome" && m.protocol == Protocol && m.id >= 2 && m.id <= 4 && localId == 0)
                { localId = m.id; SetStatus("Client connected as P" + localId); }
                else if (m.kind == "world" && localId > 0)
                {
                    if (!world.Receive(m)) { StopSession("Invalid Host world state"); return; }
                    Trace(m);
                }
                else if (m.kind == "world-result" && localId > 0)
                {
                    WorldActionCompleted?.Invoke(m.request, m.accepted, m.reason);
                }
                else if (m.kind == "state" && localId > 0 && m.players != null && m.players.Length <= 4)
                {
                    if (!Finite(m.time) || m.tick <= receivedTick ||
                        (snapshots.Count > 0 && m.time < snapshots[snapshots.Count - 1].time) ||
                        m.players.Any(p => p == null || p.id < 1 || p.id > 4 || !Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.yaw)
                            || !Finite(p.velocity.x) || !Finite(p.velocity.y) || !Finite(p.velocity.z) || p.appliedMoveSequence < 0 || p.tackleHitSequence < 0
                            || (p.id == localId && (p.appliedMoveSequence < appliedMoveSequence || p.appliedMoveSequence > moveSequence))
                            || p.armor == null || p.armor.Length == 0 || p.armor.Length > 8
                            || p.selectedArmor < 0 || p.selectedArmor >= p.armor.Length
                            || p.armor.Any(a => a < 0 || a > armorCatalog.Length)) ||
                        m.players.Select(p => p.id).Distinct().Count() != m.players.Length)
                    { StopSession("Invalid Host state"); return; }
                    states.Clear();
                    foreach (var p in m.players) states.Add(p.id, p);
                    if (states.TryGetValue(localId, out var owner)) appliedMoveSequence = owner.appliedMoveSequence;
                    receivedTick = m.tick;
                    snapshots.Add(m);
                    snapshotReceivedAt = now;
                    if (snapshots.Count > 32) snapshots.RemoveAt(0);
                    if (!renderTimeReady)
                    {
                        remoteRenderTime = m.time - RemoteInterpolationDelay;
                        ownerRenderTime = m.time - ownerInterpolationDelay;
                        renderTimeReady = true;
                    }
                    Trace(m);
                }
                else { StopSession("Unexpected Host message / protocol mismatch"); return; }
            }
            if (server == null) return;
            if (server.Closed) { StopSession("Host disconnected: " + server.Error); return; }
            if (now - lastReceive > Timeout) { StopSession("Host timeout (10 seconds)"); return; }
            if (localId > 0 && unsentMoves.Count > 0 && (now >= sendAt || (!bufferedInput && unsentEdgeSequence != 0)))
            {
                sendAt = now + NetworkInterval;
                int firstSequence = moveSequence - unsentMoves.Count + 1;
                var message = new Message { kind = "input", firstMoveSequence = firstSequence,
                    moves = unsentMoves.ToArray(), armorStep = localArmorStep, swapPress = localSwapPress };
                string payload = JsonUtility.ToJson(message);
                server.Send(payload);
                if (tracePrediction && trace != null)
                    Trace(new InputSendSample { time = now - started, firstSequence = firstSequence, count = unsentMoves.Count,
                        edgeSequence = unsentEdgeSequence, bytes = System.Text.Encoding.UTF8.GetByteCount(payload) });
                unsentMoves.Clear();
                unsentEdgeSequence = 0;
            }
        }

        void FixedUpdate()
        {
            if (!hosting)
            {
                if (localId == 0 || server == null) return;
                if (unsentMoves.Count == MaxMoveBatch) unsentMoves.RemoveAt(0);
                unsentMoves.Add(new WalkCommand { move = localInput, forward = localForward,
                    sprint = localSprint, sprintPress = localSprintPress, sprintSteer = localSprintSteer });
                moveSequence++;
                walkPrediction?.Record(moveSequence, localInput, Time.fixedDeltaTime);
                // Flush the normal numbered batch as soon as it contains a start or stop tick.
                // Analog movement within the same active state stays on the regular cadence.
                bool moving = movers.TryGetValue(localId, out var mover)
                    ? mover.IsMoveInput(localInput) : localInput != Vector2.zero;
                if (moving != sampledMoving) unsentEdgeSequence = moveSequence;
                sampledMoving = moving;
                return;
            }
            FeedMovement(1, localInput, localSprint || localSprintPress != appliedLocalSprintPress,
                localForward, localSprintSteer);
            appliedLocalSprintPress = localSprintPress;
            foreach (var c in connections)
                if (c.id > 0 && c.closeAt == 0)
                {
                    bool fresh = Time.realtimeSinceStartup - c.lastInput < .4f;
                    if (!fresh) { c.moves.Clear(); c.input = Vector2.zero; c.sprint = false; }
                    else if (c.moves.Count > 0)
                    {
                        var move = c.moves.Dequeue();
                        c.input = Vector2.ClampMagnitude(move.Input.move, 1);
                        c.forward = move.Input.forward.normalized;
                        c.sprint = move.Input.sprint;
                        c.sprintSteer = move.Input.sprintSteer;
                        c.sprintPress = move.Input.sprintPress;
                        c.lastAppliedMove = move.Sequence;
                    }
                    FeedMovement(c.id, fresh ? c.input : Vector2.zero,
                        fresh && (c.sprint || c.sprintPress != c.appliedSprintPress), c.forward,
                        fresh ? c.sprintSteer : 0f);
                    c.appliedSprintPress = c.sprintPress;
                }
        }

        void FeedMovement(int id, Vector2 input, bool sprint, Vector2 forward, float sprintSteer)
        {
            if (!movers.TryGetValue(id, out var mover)) return;
            mover.SetNetworkInput(input, sprint, new Vector3(forward.x, 0, forward.y), sprintSteer);
            var movementState = mover.GetComponent<StateMachine>();
            if (mover.HasMoveInput && movementState.IsInState<Player_Idle>()) movementState.ChangeState<Player_Walk>();
        }

        int worldRequest;
        public bool IsHost => hosting;
        public event Action<int, bool, string> WorldActionCompleted;
        public NetworkWorldObject ServerSpawn(int prefabId, Vector3 position, Quaternion rotation)
            => world?.ServerSpawn(prefabId, position, rotation);
        public bool ServerDespawn(NetworkWorldObject item) => world != null && world.ServerDespawn(item);

        internal bool TryGetPlayerPosition(int id, out Vector3 position)
        {
            if (bodies.TryGetValue(id, out var body) && body != null)
            { position = body.position; return true; }
            position = default; return false;
        }

        public bool RequestWorldAction(int targetId, string action)
        {
            if (localId == 0 || world == null || (action != "interact" && action != "delete")
                || Time.realtimeSinceStartup < localWorldActionAt) return false;
            localWorldActionAt = Time.realtimeSinceStartup + .2f;
            int request = ++worldRequest;
            if (hosting)
            {
                bool accepted = world.HandleAction(localId, targetId, action);
                WorldActionCompleted?.Invoke(request, accepted, accepted ? "" : "Out of range, unavailable or not permitted");
                return true;
            }
            if (server == null) return false;
            Send(server, new Message { kind = "world-action", request = request, target = targetId, action = action });
            return true; // Queued, not yet accepted. Host replies through WorldActionCompleted.
        }

        void ApplyArmorSelection(int id, int step, ref int appliedStep)
        {
            if (!inventories.TryGetValue(id, out var inventory)) return;
            int delta = (int)Math.Max(-32L, Math.Min(32L, (long)step - appliedStep));
            if (inventory.ArmorSlotCount > 0)
                for (int i = 0; i < Math.Abs(delta); i++)
                    if (delta < 0) inventory.ArmorLeftShift(); else inventory.ArmorRightShift();
            appliedStep = step;
        }

        void FeedSwap(int id, int press, ref int appliedPress)
        {
            if (!swappers.TryGetValue(id, out var swapper)) return;
            swapper.SetNetworkSwapInput(press != appliedPress);
            appliedPress = press;
        }

        void UpdateAvatars()
        {
            foreach (int id in avatars.Keys.Where(id => !states.ContainsKey(id)).ToArray())
            {
                if (id == localId && localJoyconBridge != null)
                    localJoyconBridge.SetPlayerInput(null);
                if (id == localId)
                {
                    walkPrediction = null;
                    ownerPoseReady = ownerPoseTransition = false;
                }
                avatars[id].SetActive(false);
                Destroy(avatars[id]); avatars.Remove(id); bodies.Remove(id); movers.Remove(id); inventories.Remove(id); swappers.Remove(id);
            }
            foreach (var p in states.Values)
            {
                if (!avatars.TryGetValue(p.id, out var avatar))
                {
                    if (playerPrefab == null) { StopSession("Player prefab reference is missing"); return; }
                    avatar = CreateAvatar(p.id);
                    var physicsBody = avatar.GetComponentInChildren<Rigidbody>(true);
                    if (physicsBody == null)
                    {
                        Destroy(avatar);
                        StopSession("Player prefab requires a Rigidbody"); return;
                    }
                    avatar.name = "Network Player " + p.id;
                    avatar.transform.SetParent(transform, false);
                    // The prefab's Rigidbody is on PlayerBody, not its root.
                    avatar.transform.position += new Vector3(p.x, p.y, p.z) - physicsBody.transform.position;
                    physicsBody.position = new Vector3(p.x, p.y, p.z);
                    physicsBody.rotation = Quaternion.Euler(0, p.yaw, 0);
                    bodies.Add(p.id, physicsBody);
                    var mover = physicsBody.GetComponent<PlayerMoveController>();
                    var inventory = avatar.GetComponentInChildren<Inventory>(true);
                    if (mover == null || inventory == null)
                    {
                        Destroy(avatar); StopSession("Player movement or inventory reference is missing"); return;
                    }
                    movers.Add(p.id, mover);
                    inventories.Add(p.id, inventory);
                    var swapper = avatar.GetComponentInChildren<PlayerSwapController>(true);
                    if (swapper == null) { Destroy(avatar); StopSession("Player swap reference is missing"); return; }
                    swappers.Add(p.id, swapper);
                    swapper.SetNetworkSwapInput(false);
                    if (p.id == localId)
                    {
                        var display = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .FirstOrDefault(c => c.gameObject.scene == gameObject.scene && c.name == "PlayerDisplay");
                        if (display != null)
                        {
                            // 本人のスコア管理にも、インベントリ UI と同じ Canvas を設定します。
                            var reference = avatar.GetComponent<PlayerReferenceController>();
                            if (reference != null && reference.PlayerScore != null)
                                reference.PlayerScore.SetPlayerCanvas(display);
                            else Debug.LogWarning("[NetworkTest] PlayerReferenceController または PlayerScore が未設定です", avatar);
                            inventory.GetComponent<PlayerInventoryUI>().ConfigureNetworkDisplay(display);
                        }
                        else Debug.LogWarning("[NetworkTest] PlayerDisplay canvas is missing");
                    }
                    mover.SetNetworkInput(Vector2.zero, false, Vector3.forward, 0f);
                    // Seed before Awake binds camera effects, so joining does not replay an old hit.
                    mover.SetNetworkTackleHitSequence(p.tackleHitSequence);
                    inventory.ConfigureNetworkInventory(initialArmorSlots);
                    avatar.SetActive(true);
                    avatars.Add(p.id, avatar);
                    if (!hosting && p.id == localId && !disablePrediction &&
                        physicsBody.TryGetComponent<CapsuleCollider>(out var capsule) && capsule.direction == 1 &&
                        physicsBody.TryGetComponent<Player_Walk>(out var walk))
                        walkPrediction = new LocalWalkPrediction(capsule, mover, walk.WalkSpeed);
                    Debug.Log($"[NetworkTest] VIEW P{p.id} owned={p.id == localId} cameras={avatar.GetComponentsInChildren<Camera>().Count(c => c.enabled)} listeners={avatar.GetComponentsInChildren<AudioListener>().Count(a => a.enabled)}");
                }
                // Host bodies are moved exclusively by physics. Clients render buffered poses.
                if (!hosting) movers[p.id].SetNetworkTackleHitSequence(p.tackleHitSequence);
                if (!hosting && p.armor != null)
                    inventories[p.id].ApplyNetworkInventory(p.selectedArmor,
                        p.armor.Select(a => a == 0 ? null : armorCatalog[a - 1]).ToArray());
            }
        }

        void RenderClientAvatars(float now)
        {
            if (hosting || snapshots.Count == 0) return;
            float latest = snapshots[snapshots.Count - 1].time;
            float estimatedHostTime = latest + (now - snapshotReceivedAt);
            remoteRenderTime = AdvanceRenderTime(remoteRenderTime, estimatedHostTime - RemoteInterpolationDelay, latest);
            // A short buffer needs to recover its lead after both clocks reach the newest sample.
            ownerRenderTime = ownerInterpolationDelay == RemoteInterpolationDelay ? remoteRenderTime :
                AdvanceRenderTime(ownerRenderTime, estimatedHostTime - ownerInterpolationDelay, latest,
                    tolerance: .01f, speedAdjustment: .12f);
            // Keep the pair needed by the older clock. Each clock can sample a different pair.
            float oldestRenderTime = Mathf.Min(remoteRenderTime, ownerRenderTime);
            while (snapshots.Count > 2 && snapshots[1].time <= oldestRenderTime) snapshots.RemoveAt(0);
            var remoteBlend = FindSnapshotBlend(remoteRenderTime);
            var ownerBlend = FindSnapshotBlend(ownerRenderTime);
            foreach (var pair in bodies)
            {
                if (pair.Key == localId && walkPrediction != null && states.TryGetValue(localId, out var owner))
                {
                    if (reconciledTick != receivedTick)
                    {
                        walkPrediction.Reconcile(new LocalWalkPrediction.Pose
                        {
                            Position = new Vector3(owner.x, owner.y, owner.z),
                            Velocity = owner.velocity,
                            Rotation = Quaternion.Euler(0, owner.yaw, 0)
                        }, owner.appliedMoveSequence, owner.sprinting);
                        reconciledTick = receivedTick;
                    }
                    if (walkPrediction.CanDisplay && now - snapshotReceivedAt <= .25f)
                    {
                        movers[pair.Key].NetworkSprinting = false;
                        float remainder = Mathf.Clamp((float)(Time.timeAsDouble - Time.fixedTimeAsDouble), 0, Time.fixedDeltaTime);
                        var pose = walkPrediction.Render(localInput, remainder, Time.unscaledDeltaTime);
                        RenderOwnerPose(pair.Value.transform, pose.Position, pose.Rotation, true, now);
                        continue;
                    }
                }
                var blend = pair.Key == localId ? ownerBlend : remoteBlend;
                var a = blend.from.players.FirstOrDefault(p => p.id == pair.Key);
                var b = blend.to.players.FirstOrDefault(p => p.id == pair.Key);
                // A newly joined player has no previous sample yet.
                if (a == null || b == null) continue;
                movers[pair.Key].NetworkSprinting = blend.weight < 1f ? a.sprinting : b.sprinting;
                var position = Vector3.Lerp(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z), blend.weight);
                var rotation = Quaternion.Euler(0, Mathf.LerpAngle(a.yaw, b.yaw, blend.weight), 0);
                if (pair.Key == localId) RenderOwnerPose(pair.Value.transform, position, rotation, false, now);
                else pair.Value.transform.SetPositionAndRotation(position, rotation);
            }
        }

        static float AdvanceRenderTime(float renderTime, float targetTime, float latestSnapshotTime,
            float tolerance = NetworkInterval, float speedAdjustment = .02f)
        {
            float error = targetTime - renderTime;
            if (Mathf.Abs(error) > .5f) renderTime = targetTime;
            else
            {
                // Absorb ordinary arrival jitter without changing playback speed every frame.
                float rate = error > tolerance ? 1f + speedAdjustment : error < -tolerance ? 1f - speedAdjustment : 1f;
                renderTime += Time.unscaledDeltaTime * rate;
            }
            return Mathf.Min(renderTime, latestSnapshotTime);
        }

        (Message from, Message to, float weight) FindSnapshotBlend(float time)
        {
            int index = 0;
            while (index + 2 < snapshots.Count && snapshots[index + 1].time <= time) index++;
            var from = snapshots[index];
            var to = index + 1 < snapshots.Count ? snapshots[index + 1] : from;
            float weight = to.time > from.time ? Mathf.InverseLerp(from.time, to.time, time) : 1;
            return (from, to, weight);
        }

        void RenderOwnerPose(Transform body, Vector3 position, Quaternion rotation, bool predicted, float now)
        {
            // The buffered pose is older than the predicted one. Limit the catch-up only
            // when switching between them so a missing snapshot does not cause a visible jump.
            if (ownerPoseReady && predicted != ownerPosePredicted) ownerPoseTransition = true;
            ownerPoseReady = true;
            ownerPosePredicted = predicted;
            if (ownerPoseTransition)
            {
                var next = Vector3.MoveTowards(body.position, position, 12f * Mathf.Min(Time.unscaledDeltaTime, .025f));
                ownerPoseTransition = (next - position).sqrMagnitude > .000001f;
                position = next;
            }
            body.SetPositionAndRotation(position, rotation);
            TracePrediction(now, position, predicted);
        }

        void TracePrediction(float now, Vector3 position, bool predicted)
        {
            if (!tracePrediction || trace == null) return;
            Trace(new PredictionSample { time = now - started, input = localInput, position = position,
                ownerRenderTime = ownerRenderTime, remoteRenderTime = remoteRenderTime,
                newestSnapshotTime = snapshots[snapshots.Count - 1].time, snapshotAge = now - snapshotReceivedAt,
                sequence = moveSequence, appliedSequence = appliedMoveSequence, predicted = predicted });
        }

        GameObject CreateAvatar(int id)
        {
            bool owned = id == localId;
            // Instantiate under an inactive parent so no single-player OnEnable/Start can run.
            var copy = Instantiate(playerPrefab, inactivePlayers);
            copy.SetActive(false);
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                bool cameraScript = script is Unity.Cinemachine.CinemachineCamera ||
                    script is Unity.Cinemachine.CinemachineThirdPersonFollow ||
                    script is Unity.Cinemachine.CinemachineBrain ||
                    script is TackleCameraEffects ||
                    script is TestPlayerCameraLookController;
                bool renderScript = script is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData;
                bool movementScript = script is PlayerMoveController || script is Player_Idle
                    || script is Player_Walk || script is Player_Sprint
                    || script is PlayerSwapController || script is Player_Swap || script is Player_SwapIdle
                    || (script is StateMachine && (script.GetComponent<PlayerMoveController>() != null
                        || script.GetComponent<PlayerSwapController>() != null));
                script.enabled = renderScript || (owned && (cameraScript || script is JoyconInputBridge))
                    || (hosting && movementScript);
            }
            if (owned && !hosting)
            {
                // Client proxies are rendered each frame, so their camera must follow each frame.
                foreach (var brain in copy.GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true))
                {
                    brain.UpdateMethod = Unity.Cinemachine.CinemachineBrain.UpdateMethods.LateUpdate;
                    brain.BlendUpdateMethod = Unity.Cinemachine.CinemachineBrain.BrainUpdateMethods.LateUpdate;
                }
            }
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = !hosting;
                body.useGravity = hosting;
                body.interpolation = hosting ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                body.collisionDetectionMode = hosting ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            }
            // Preserve prefab colliders on Host. Proxies must not run a second simulation.
            if (!hosting)
                foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var camera in copy.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = owned && camera.name == "PlayerCamera";
                if (!camera.enabled) continue;
                camera.targetDisplay = 0;
                camera.targetTexture = null;
                view = camera;
                if (lobbyView != null) lobbyView.enabled = false;
            }
            foreach (var audio in copy.GetComponentsInChildren<AudioListener>(true))
                audio.enabled = owned && audio.gameObject.name == "PlayerCamera";
            var look = copy.GetComponentInChildren<TestPlayerCameraLookController>(true);
            if (look != null)
            {
                look.ConfigureNetworkSplines(GameObject.Find("Splines")?.transform);
                if (owned) localLook = look;
            }
            // Only the owner needs actions for the prefab's spline camera direction logic.
            var input = copy.GetComponent<PlayerInput>();
            if (input != null && owned)
            {
                input.defaultControlScheme = "";
                input.enabled = true;
                localPlayerInput = input;
                // 本人の生成時だけ、同じ Scene の JoyconManager に入力先を設定します。
                var manager = FindObjectsByType<JoyconManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.gameObject.scene == gameObject.scene);
                localJoyconBridge = manager != null ? manager.GetComponent<JoyconInputBridge>() : null;
                if (localJoyconBridge != null)
                {
                    // Prefab 側にも橋渡し処理がある場合は、Scene 側に一本化します。
                    var prefabBridge = copy.GetComponent<JoyconInputBridge>();
                    if (prefabBridge != null) prefabBridge.enabled = false;
                    localJoyconBridge.SetPlayerInput(input);
                }
            }
            return copy;
        }

        void LateUpdate()
        {
            if (localLook != null) localLook.Look();
        }

        void SetStatus(string text)
        {
            status = text;
            Debug.Log("[NetworkTest] " + text);
        }

        void Trace(object message)
        {
            if (trace == null) return;
            try { trace.WriteLine(JsonUtility.ToJson(message)); }
            catch (Exception e) { Debug.LogWarning("Trace disabled: " + e.Message); trace.Dispose(); trace = null; }
        }

        public void StopSession(string reason = "Disconnected by user")
        {
            relayGeneration++;
            relayStarting = relayConnecting = roomAnnounced = false;
            listener?.Stop(); listener = null;
            hostLanCode = "";
            server?.Dispose(); server = null;
            connectingSocket?.Close(); connectingSocket = null;
            if (connectTask != null)
            {
                // Observe canceled connection faults, including those completing after teardown.
                _ = connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                connectTask = null;
            }
            foreach (var c in connections) c.peer.Dispose();
            relay?.Dispose(); relay = null;
            connections.Clear(); states.Clear();
            world?.Reset();
            worldSendAt = localWorldActionAt = 0; worldRequest = 0;
            // 破棄する前に入力先を解除し、仮想パッドを片付けます。
            if (localJoyconBridge != null) localJoyconBridge.SetPlayerInput(null);
            localJoyconBridge = null;
            foreach (var avatar in avatars.Values) { avatar.SetActive(false); Destroy(avatar); }
            avatars.Clear();
            bodies.Clear(); snapshots.Clear(); renderTimeReady = false;
            walkPrediction = null;
            unsentMoves.Clear();
            unsentEdgeSequence = 0;
            sampledMoving = false;
            moveSequence = reconciledTick = appliedMoveSequence = 0;
            ownerPoseReady = ownerPoseTransition = false;
            movers.Clear(); inventories.Clear();
            swappers.Clear(); localSwapPress = appliedLocalSwapPress = 0;
            localSprintPress = appliedLocalSprintPress = localArmorStep = appliedLocalArmorStep = 0;
            localLook = null;
            localPlayerInput = null;
            view = lobbyView;
            if (lobbyView != null) lobbyView.enabled = true;
            hosting = connecting = false;
            localId = tick = receivedTick = 0;
            sendAt = snapshotAt = 0;
            SetStatus(reason);
        }

        void OnGUI()
        {
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginArea(new Rect(12, 12, 440, Screen.height - 24));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ONLINE / LAN PLAY | Host + 3 Clients");
            bool idle = !Busy;
            GUI.enabled = idle;
            internetMode = GUILayout.Toolbar(internetMode ? 0 : 1, new[] { "Internet (room code)", "LAN (code / IP)" }) == 0;
            if (internetMode)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Room code", GUILayout.Width(80));
                joinCodeInput = GUILayout.TextField(joinCodeInput, 16);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create online room", GUILayout.Height(30))) StartRelayHost();
                if (GUILayout.Button("Join with code", GUILayout.Height(30))) StartRelayClient();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("LAN code", GUILayout.Width(70));
                lanCodeInput = GUILayout.TextField(lanCodeInput, 40);
                if (GUILayout.Button("Join LAN code", GUILayout.Width(115))) StartLanCodeClient();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Host IPv4", GUILayout.Width(70));
                ip = GUILayout.TextField(ip, 45, GUILayout.Width(190));
                GUILayout.Label("Port", GUILayout.Width(35));
                portText = GUILayout.TextField(portText, 5, GUILayout.Width(70));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Host", GUILayout.Height(30))) StartHost();
                if (GUILayout.Button("Connect by IP", GUILayout.Height(30))) StartClient();
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
            if (internetMode)
            {
                if (hosting && relay != null && roomAnnounced)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Your room code", GUILayout.Width(100));
                    GUILayout.TextField(relay.JoinCode);
                    if (GUILayout.Button("Copy", GUILayout.Width(60))) GUIUtility.systemCopyBuffer = relay.JoinCode;
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("Play across different networks using a room code.\nHost must keep the room open. Share the code only with invited players.");
            }
            else
            {
                if (hosting && listener != null)
                {
                    if (hostLanCode.Length > 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Share IPv4", GUILayout.Width(75));
                        GUILayout.Label(localAddresses[lanAddressIndex].ToString());
                        if (localAddresses.Length > 1 && GUILayout.Button($"Next IP ({lanAddressIndex + 1}/{localAddresses.Length})"))
                        {
                            lanAddressIndex = (lanAddressIndex + 1) % localAddresses.Length;
                            UpdateHostLanCode();
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Your LAN code", GUILayout.Width(100));
                        GUILayout.TextField(hostLanCode);
                        if (GUILayout.Button("Copy", GUILayout.Width(60))) GUIUtility.systemCopyBuffer = hostLanCode;
                        GUILayout.EndHorizontal();
                    }
                    else GUILayout.Label("No active IPv4 found. Check Wi-Fi or Ethernet.");
                    if (GUILayout.Button("Refresh this PC's IP addresses")) RefreshLocalAddresses();
                }
                GUILayout.Label("Share the LAN code with players who can reach the selected IPv4. The code includes the Host's TCP port.");
            }
            GUILayout.Label(status);
            GUILayout.Label($"Players: {states.Count}/4   You: {(localId == 0 ? "-" : "P" + localId)}   Role: {(hosting ? "Host" : localId > 0 ? "Client" : "Offline")}   Tick: {(hosting ? tick : receivedTick)}");
            GUILayout.Label("WASD / left stick: move   Left Shift: Sprint\nE: equip overlapping field armor   Arrows: switch slot\nF: interact with nearest network door / spawn point\nClick a window to control it. Unfocused input stops.");
            if (!idle && GUILayout.Button("Cancel / Disconnect / Stop Host")) StopSession();
            if (testMotion) GUILayout.Label("AUTOMATED MOVEMENT TEST MODE");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            StopSession("Session closed");
            trace?.Dispose(); trace = null;
        }
    }
}
