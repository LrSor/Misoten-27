using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sawada.Cordex
{
    [DefaultExecutionOrder(-10000)]
    public sealed class NetworkTestSession : MonoBehaviour
    {
        const string Protocol = "SawadaNetworkTest/1";
        const float Timeout = 10f;
        [SerializeField] GameObject playerPrefab;
        [Serializable] public class PlayerState
        {
            public int id;
            public float x, z, yaw;
        }
        [Serializable] public class Message
        {
            public string kind, protocol, reason;
            public int id, tick;
            public float x, z;
            public bool sprint;
            public PlayerState[] players;
        }
        sealed class Connection
        {
            public TcpPeer peer;
            public int id;
            public float lastReceive, lastInput, closeAt;
            public Vector2 input;
            public bool sprint;
        }
        readonly List<Connection> connections = new List<Connection>();
        readonly Dictionary<int, PlayerState> states = new Dictionary<int, PlayerState>();
        readonly Dictionary<int, GameObject> avatars = new Dictionary<int, GameObject>();
        readonly Color[] colors = { Color.cyan, new Color(1f,.4f,.2f), Color.green, Color.magenta };
        TcpListener listener;
        TcpPeer server;
        TcpClient connectingSocket;
        Task connectTask;
        Camera view;
        Camera lobbyView;
        TestPlayerCameraLookController localLook;
        Transform inactivePlayers;
        string ip = "127.0.0.1", portText = "7777", status = "Choose Host or Client";
        int localId, tick, receivedTick;
        bool hosting, connecting, testMotion;
        float started, lastReceive, sendAt, snapshotAt, connectStarted, exitAfter;
        Vector2 localInput;
        bool localSprint;
        StreamWriter trace;

        void Awake()
        {
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
            testMotion = args.Contains("--net-test-motion");
            float.TryParse(Arg("--net-exit-after", "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out exitAfter);
            string tracePath = Arg("--net-trace");
            if (!string.IsNullOrEmpty(tracePath))
            {
                try { trace = new StreamWriter(tracePath) { AutoFlush = true }; }
                catch (Exception e) { Debug.LogWarning("Trace: " + e.Message); }
            }
            if (Arg("--net-mode") == "host") StartHost();
            else if (Arg("--net-mode") == "client") StartClient();
        }

        bool ReadPort(out int port)
        {
            if (int.TryParse(portText, out port) && port > 0 && port <= 65535) return true;
            SetStatus("Invalid port: enter 1-65535");
            return false;
        }

        public void StartHost()
        {
            if (hosting || connecting || server != null || !ReadPort(out int port)) return;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(8);
                hosting = true;
                localId = 1;
                states.Add(1, SpawnState(1));
                SetStatus("Host listening on 0.0.0.0:" + port + " (TCP)");
            }
            catch (Exception e) { StopSession("Host failed: " + e.Message); }
        }

        public void StartClient()
        {
            if (hosting || connecting || server != null || !ReadPort(out int port)) return;
            if (!IPAddress.TryParse(ip.Trim(), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            { SetStatus("Invalid IPv4 address"); return; }
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

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (exitAfter > 0 && now - started >= exitAfter) { Application.Quit(); return; }
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
                        Send(server, new Message { kind = "hello", protocol = Protocol });
                        SetStatus("Connected; waiting for Host approval");
                    }
                }
                else if (now - connectStarted > Timeout) StopSession("Connect timeout (10 seconds)");
            }
            ReadLocalInput(now);
            if (hosting) PollHost(now);
            else if (server != null) PollClient(now);
            UpdateAvatars();
        }

        void ReadLocalInput(float now)
        {
            localInput = Vector2.zero;
            localSprint = false;
            if (localId == 0) return;
            if (testMotion)
            {
                // Explicit verification mode only: alternate direction every 2 seconds.
                float sign = ((int)((now - started) / 2) % 2 == 0) ? 1 : -1;
                localInput = localId % 2 == 0 ? new Vector2(sign, 0) : new Vector2(0, sign);
                return;
            }
            if (!Application.isFocused) return;
            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                localInput = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                localSprint = k.leftShiftKey.isPressed;
            }
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > .04f)
                localInput = Gamepad.current.leftStick.ReadValue();
            localInput = Vector2.ClampMagnitude(localInput, 1);
            if (view != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude > .001f)
                {
                    Vector3 direction = Vector3.Cross(Vector3.up, forward) * localInput.x + forward * localInput.y;
                    localInput = new Vector2(direction.x, direction.z);
                }
            }
        }

        static PlayerState SpawnState(int id) => new PlayerState { id = id, x = (id - 2.5f) * 3, z = 0 };
        static void Send(TcpPeer peer, Message message) => peer.Send(JsonUtility.ToJson(message));
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
                for (int i = 0; i < 8 && listener.Pending(); i++)
                {
                    var socket = listener.AcceptTcpClient();
                    if (connections.Count >= 8) { socket.Close(); continue; }
                    connections.Add(new Connection { peer = new TcpPeer(socket), lastReceive = now });
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
                        int id = Enumerable.Range(2, 3).FirstOrDefault(n => !states.ContainsKey(n));
                        if (id == 0) { Reject(c, "Room full: maximum 4 players including Host", now); break; }
                        c.id = id;
                        states.Add(id, SpawnState(id));
                        Send(c.peer, new Message { kind = "welcome", id = id, protocol = Protocol });
                        Debug.Log("[NetworkTest] JOIN P" + id);
                    }
                    else if (m.kind == "input" && Finite(m.x) && Finite(m.z) && Mathf.Abs(m.x) <= 1.01f && Mathf.Abs(m.z) <= 1.01f)
                    {
                        // Ownership comes from the socket, never a client-supplied player ID.
                        c.input = Vector2.ClampMagnitude(new Vector2(m.x, m.z), 1);
                        c.sprint = m.sprint;
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
            if (now >= snapshotAt)
            {
                snapshotAt = now + .05f;
                var m = new Message { kind = "state", tick = ++tick, players = states.Values.OrderBy(p => p.id).ToArray() };
                foreach (var c in connections) if (c.id > 0 && c.closeAt == 0) Send(c.peer, m);
                Trace(m);
            }
        }

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
                else if (m.kind == "state" && localId > 0 && m.players != null && m.players.Length <= 4)
                {
                    if (m.players.Any(p => p == null || p.id < 1 || p.id > 4 || !Finite(p.x) || !Finite(p.z) || !Finite(p.yaw)) ||
                        m.players.Select(p => p.id).Distinct().Count() != m.players.Length)
                    { StopSession("Invalid Host state"); return; }
                    states.Clear();
                    foreach (var p in m.players) states.Add(p.id, p);
                    receivedTick = m.tick;
                    Trace(m);
                }
                else { StopSession("Unexpected Host message / protocol mismatch"); return; }
            }
            if (server == null) return;
            if (server.Closed) { StopSession("Host disconnected: " + server.Error); return; }
            if (now - lastReceive > Timeout) { StopSession("Host timeout (10 seconds)"); return; }
            if (localId > 0 && now >= sendAt)
            {
                sendAt = now + .05f;
                Send(server, new Message { kind = "input", x = localInput.x, z = localInput.y, sprint = localSprint });
            }
        }

        void FixedUpdate()
        {
            if (!hosting) return;
            MovePlayer(1, localInput, localSprint);
            foreach (var c in connections)
                if (c.id > 0 && c.closeAt == 0)
                    MovePlayer(c.id, Time.realtimeSinceStartup - c.lastInput < .4f ? c.input : Vector2.zero, c.sprint);
        }

        void MovePlayer(int id, Vector2 input, bool sprint)
        {
            if (!states.TryGetValue(id, out var p)) return;
            float speed = sprint ? 10 : 5;
            p.x = Mathf.Clamp(p.x + input.x * speed * Time.fixedDeltaTime, -20, 20);
            p.z = Mathf.Clamp(p.z + input.y * speed * Time.fixedDeltaTime, -20, 20);
            if (input.sqrMagnitude > .001f) p.yaw = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        }

        void UpdateAvatars()
        {
            foreach (int id in avatars.Keys.Where(id => !states.ContainsKey(id)).ToArray())
            { Destroy(avatars[id]); avatars.Remove(id); }
            foreach (var p in states.Values)
            {
                if (!avatars.TryGetValue(p.id, out var avatar))
                {
                    if (playerPrefab == null) { StopSession("Player prefab reference is missing"); return; }
                    avatar = CreateAvatar(p.id);
                    foreach (var r in avatar.GetComponentsInChildren<Renderer>(true))
                    {
                        var block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        block.SetColor("_BaseColor", colors[p.id - 1]);
                        block.SetColor("_Color", colors[p.id - 1]);
                        r.SetPropertyBlock(block);
                    }
                    avatar.name = "Network Player " + p.id;
                    avatar.transform.SetParent(transform, false);
                    avatar.transform.position = new Vector3(p.x, playerPrefab.transform.position.y, p.z);
                    avatar.SetActive(true);
                    avatars.Add(p.id, avatar);
                    Debug.Log($"[NetworkTest] VIEW P{p.id} owned={p.id == localId} cameras={avatar.GetComponentsInChildren<Camera>().Count(c => c.enabled)} listeners={avatar.GetComponentsInChildren<AudioListener>().Count(a => a.enabled)}");
                }
                // Same authoritative transform on every participant; no local prediction.
                avatar.transform.position = new Vector3(p.x, playerPrefab.transform.position.y, p.z);
                var body = avatar.transform.Find("PlayerBody");
                if (body != null) body.rotation = Quaternion.Euler(0, p.yaw, 0);
            }
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
                    script is TestPlayerCameraLookController;
                bool renderScript = script is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData;
                script.enabled = renderScript || (owned && cameraScript);
            }
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
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

        void Trace(Message message)
        {
            if (trace == null) return;
            try { trace.WriteLine(JsonUtility.ToJson(message)); }
            catch (Exception e) { Debug.LogWarning("Trace disabled: " + e.Message); trace.Dispose(); trace = null; }
        }

        public void StopSession(string reason = "Disconnected by user")
        {
            listener?.Stop(); listener = null;
            server?.Dispose(); server = null;
            connectingSocket?.Close(); connectingSocket = null;
            if (connectTask != null)
            {
                // Observe canceled connection faults, including those completing after teardown.
                _ = connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                connectTask = null;
            }
            foreach (var c in connections) c.peer.Dispose();
            connections.Clear(); states.Clear();
            foreach (var avatar in avatars.Values) { avatar.SetActive(false); Destroy(avatar); }
            avatars.Clear();
            localLook = null;
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
            GUILayout.BeginArea(new Rect(12, 12, 440, 245), GUI.skin.box);
            GUILayout.Label("NETWORK TEST | Host + 3 Clients | TCP");
            bool idle = !hosting && !connecting && server == null;
            GUI.enabled = idle;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host IPv4", GUILayout.Width(70));
            ip = GUILayout.TextField(ip, 45, GUILayout.Width(190));
            GUILayout.Label("Port", GUILayout.Width(35));
            portText = GUILayout.TextField(portText, 5, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host", GUILayout.Height(30))) StartHost();
            if (GUILayout.Button("Connect Client", GUILayout.Height(30))) StartClient();
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(status);
            GUILayout.Label($"Players: {states.Count}/4   You: {(localId == 0 ? "-" : "P" + localId)}   Role: {(hosting ? "Host" : localId > 0 ? "Client" : "Offline")}   Tick: {(hosting ? tick : receivedTick)}");
            GUILayout.Label("WASD / left stick: move   Left Shift: run\nClick a window to control it. Unfocused input stops.\nHost binds all IPv4 interfaces; Client uses the IP above.");
            if (!idle && GUILayout.Button("Disconnect / Stop Host")) StopSession();
            if (testMotion) GUILayout.Label("AUTOMATED MOVEMENT TEST MODE");
            GUILayout.EndArea();
            if (view == null) return;
            foreach (var p in states.Values)
            {
                Vector3 point = view.WorldToScreenPoint(new Vector3(p.x, 3, p.z));
                if (point.z <= 0) continue;
                GUI.color = colors[p.id - 1];
                GUI.Label(new Rect(point.x - 40, Screen.height - point.y, 130, 28), "P" + p.id + (p.id == localId ? " (YOU)" : ""));
            }
            GUI.color = Color.white;
        }

        void OnDestroy()
        {
            StopSession("Session closed");
            trace?.Dispose(); trace = null;
        }
    }
}
