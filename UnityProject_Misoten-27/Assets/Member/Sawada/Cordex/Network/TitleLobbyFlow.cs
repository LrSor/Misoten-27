using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sawada.Cordex
{
    // Owns the transport until every participant has loaded the game. No reconnect is needed.
    public sealed partial class TitleLobbyFlow : MonoBehaviour
    {
        public const string TitleScene = "Title_Kari";
        public const string LobbyScene = "PlayerLobby";
        public const string GameScene = "Game_11_26_02";
        const string Protocol = "SawadaTitleLobby/3";
        const float Timeout = 10f, LoadTimeout = 90f;
        static TitleLobbyFlow instance;
        internal static bool WaitingForGame => instance != null && instance.loading;
        public static TitleLobbyFlow Current => instance;
        public int PlayerNumber => localId;
        public string RoomCode => code;
        public bool IsInRoom => inRoom;
        public bool IsLoading => loading;
        public bool IsLeaving => leaving;
        public string Status => status;
        public bool IsPlayerPresent(int id) => members.Contains(id);
        public bool IsPlayerPrepared(int id) => preparedMembers.Contains(id);

        [Serializable] sealed class Packet
        {
            public string kind, protocol, reason, catalog;
            public int id, revision;
            public bool prepared;
            public int[] players, preparedPlayers, lobbyPlayers;
        }
        sealed class Guest
        {
            public INetworkTestPeer peer;
            public int id;
            public float received, closeAt;
            public bool ready;
            public bool prepared;
            public string catalog;
            public int admissionOrder;
            public bool inLobby, resultAcknowledged;
        }
        readonly List<Guest> guests = new List<Guest>();
        readonly Dictionary<Canvas, bool> titleCanvases = new Dictionary<Canvas, bool>();
        TcpListener listener;
        TcpClient socket;
        Task connectTask;
        RelayTestTransport relay;
        INetworkTestPeer server;
        IPAddress[] addresses = Array.Empty<IPAddress>();
        int addressIndex, generation, localId;
        int[] members = Array.Empty<int>();
        int[] preparedMembers = Array.Empty<int>();
        int rosterRevision;
        int nextAdmission;
        bool hostPrepared, leaving;
        float leaveAt;
        bool internet, menu, busy, host, inRoom, loading, localReady, handedOff;
        string code = "", input = "", portInput = "7777", status = "";
        float began, lastReceive, heartbeat;
        NetworkTestSession game;
        Func<bool> gameGate;
        AsyncOperation sceneLoad;
        string afterLoadError;
        string titleMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            if (instance != null) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            SceneManager.sceneLoaded += SceneLoaded;
            gameGate = () => !loading;
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TitleScene)
            {
                titleCanvases.Clear();
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                        titleCanvases[canvas] = canvas.enabled;
                    foreach (var button in root.GetComponentsInChildren<Button>(true))
                        if (button.name == "ButtonNewGame")
                        {
                            button.onClick = new Button.ButtonClickedEvent();
                            button.onClick.AddListener(OpenMenu);
                        }
                }
                if (!busy)
                {
                    handedOff = false; game = null;
                    menu = titleMessage != null;
                    if (menu) status = titleMessage;
                    titleMessage = null;
                    ShowTitle(!menu);
                }
            }
            else if (scene.name == LobbyScene && busy)
            {
                ArrivedInLobby();
            }
            else if (scene.name == ResultScene && busy)
            {
                if (!host) Send(server, new Packet { kind = "result-ready" });
            }
            else if (scene.name == LobbyScene && !busy)
            {
                menu = true;
                status = "No active room. Return to Title to create or join a room.";
            }
            else if (scene.name == GameScene && loading)
            {
                game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkTestSession>())
                    .SingleOrDefault();
                if (game == null || !game.isActiveAndEnabled || !game.LobbyWorldReady)
                { Fail("The game scene could not initialize."); return; }
                GAME_MANAGER.StartGate = gameGate;
                localReady = true;
                Debug.Log("[TitleLobby] LOADED P" + localId);
                // This is the final lobby packet from a Client. No heartbeat may follow it:
                // Host will hand the same peer to the gameplay parser after receiving it.
                if (!host) Send(server, new Packet { kind = "loaded", catalog = game.LobbyCatalog });
            }
        }

        public void OpenMenu()
        {
            if (busy) return;
            menu = true;
            status = "Create a room as Host, or enter a room code as Client.";
            ShowTitle(false);
        }

        void ShowTitle(bool visible)
        {
            foreach (var item in titleCanvases)
                if (item.Key != null) item.Key.enabled = visible && item.Value;
        }

        void SetStatus(string text)
        {
            status = text;
            Debug.Log("[TitleLobby] " + text);
        }

        void FindAddresses()
        {
            addresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address)
                .Where(a => LanRoomCode.CanEncodeAddress(a) && !IPAddress.IsLoopback(a))
                .Distinct().OrderBy(a => a.ToString(), StringComparer.Ordinal).ToArray();
            if (addresses.Length == 0) addresses = new[] { IPAddress.Loopback };
            addressIndex = 0;
        }

        void CreateRoom()
        {
            if (busy) return;
            if (!internet && (!int.TryParse(portInput, out int p) || p < 1 || p > 65535))
            { SetStatus("Port must be between 1 and 65535."); return; }
            Begin(true);
            try
            {
                if (internet) { StartRelay(true, ""); return; }
                FindAddresses();
                listener = new TcpListener(IPAddress.Any, int.Parse(portInput));
                listener.Start(8);
                code = LanRoomCode.Encode(addresses[addressIndex], int.Parse(portInput));
                EnterRoom(1);
            }
            catch (Exception e) { Fail("Could not create room: " + e.Message); }
        }

        void JoinRoom()
        {
            if (busy) return;
            string text = input.Trim().ToUpperInvariant();
            if (internet)
            {
                if (text.Length == 0 || text.Length > 16 || text.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))))
                { SetStatus("Enter the Internet room code from the Host."); return; }
                Begin(false);
                code = text;
                StartRelay(false, text);
                return;
            }
            if (!LanRoomCode.TryDecode(text, out var address, out int port))
            { SetStatus("Enter the full LAN-XXXX-XXXX-XXXX room code."); return; }
            Begin(false);
            code = text;
            try
            {
                socket = new TcpClient(AddressFamily.InterNetwork);
                connectTask = socket.ConnectAsync(address, port);
            }
            catch (Exception e) { Fail("Could not connect: " + e.Message); }
        }

        void Begin(bool asHost)
        {
            busy = true; host = asHost; handedOff = false;
            began = lastReceive = Time.realtimeSinceStartup;
            SetStatus(asHost ? "Creating room..." : "Connecting to room...");
        }

        async void StartRelay(bool asHost, string roomCode)
        {
            int attempt = ++generation;
            var transport = new RelayTestTransport();
            relay = transport;
            try
            {
                if (asHost) await transport.StartHostAsync();
                else await transport.StartClientAsync(roomCode);
                if (this == null || attempt != generation) transport.Dispose();
            }
            catch (Exception e)
            {
                transport.Dispose();
                if (this != null && attempt == generation) Fail("Internet room failed: " + e.Message);
            }
        }

        void EnterRoom(int id)
        {
            localId = id; inRoom = true;
            if (host) members = new[] { 1 };
            SetStatus(host ? "Room ready. Waiting for Clients." : "Joined. Waiting for the Host to start.");
            Debug.Log("[TitleLobby] ROOM " + code + " P" + id);
            if (SceneManager.GetActiveScene().name != LobbyScene) LoadScene(LobbyScene);
        }

        void LoadScene(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Fail("Scene is missing from the build: " + scene); return; }
            sceneLoad = SceneManager.LoadSceneAsync(scene);
        }

        void Update()
        {
            if (afterLoadError != null)
            {
                if (sceneLoad != null && !sceneLoad.isDone) return;
                string reason = afterLoadError;
                afterLoadError = null;
                titleMessage = reason;
                menu = false;
                LoadScene(TitleScene);
                SetStatus(reason);
                return;
            }
            if (!busy) return;
            float now = Time.realtimeSinceStartup;
            try
            {
                if (leaving)
                {
                    relay?.Tick();
                    if (now >= leaveAt) ReturnToTitle("Left the room.");
                    return;
                }
                if (relay != null)
                {
                    relay.Tick();
                    if (relay.Error != null) { Fail(relay.Error); return; }
                    if (host && !inRoom && relay.Ready) { code = relay.JoinCode; EnterRoom(1); }
                    if (!host && server == null && relay.ClientConnected)
                    { server = relay.Client; SayHello(); }
                }
                if (connectTask != null && connectTask.IsCompleted)
                {
                    if (connectTask.IsFaulted || connectTask.IsCanceled)
                    { Fail("Room not found / connection failed: " + connectTask.Exception?.GetBaseException().Message); return; }
                    server = new TcpPeer(socket); socket = null; connectTask = null;
                    SayHello();
                }
                if (host) PollHost(now);
                else PollClient(now);
                if (!busy) return;
                if (host && inRoom && !loading && hostPrepared &&
                    guests.Where(g => g.id > 0 && g.closeAt == 0).All(g => g.prepared && g.inLobby) &&
                    SceneManager.GetActiveScene().name == LobbyScene && (sceneLoad == null || sceneLoad.isDone)) StartGame();
                if (loading)
                {
                    if (now - began > LoadTimeout) { Fail("Game loading timed out. Please create the room again."); return; }
                    if (host && localReady && guests.All(g => g.ready && g.id > 0 && g.closeAt == 0))
                    {
                        if (guests.Any(g => g.catalog != game.LobbyCatalog))
                        { Fail("Game data differs. All players must use the same build."); return; }
                        foreach (var guest in guests) Send(guest.peer, new Packet { kind = "play" });
                        Handoff();
                        return;
                    }
                }
                else if ((!inRoom && now - began > (internet ? 45f : Timeout)) ||
                    (!host && server != null && now - lastReceive > Timeout))
                { Fail("Connection timed out. Check the room code, network and Host."); return; }

                if (now >= heartbeat)
                {
                    heartbeat = now + .5f;
                    if (host)
                    {
                        BroadcastRoom();
                    }
                    else if (server != null && !(loading && localReady)) Send(server, new Packet { kind = "ping" });
                }
            }
            catch (Exception e) { Fail("Connection failed: " + e.Message); }
        }

        void SayHello()
        {
            lastReceive = Time.realtimeSinceStartup;
            Send(server, new Packet { kind = "hello", protocol = Protocol });
        }

        static void Send(INetworkTestPeer peer, Packet packet) => peer.Send(JsonUtility.ToJson(packet));

        void PollHost(float now)
        {
            for (int i = 0; i < 8; i++)
            {
                INetworkTestPeer peer;
                if (relay != null) { if (!relay.TryAccept(out peer)) break; }
                else { if (listener == null || !listener.Pending()) break; peer = new TcpPeer(listener.AcceptTcpClient()); }
                if (loading || !atLobby || guests.Count >= 8) { peer.Dispose(); continue; }
                guests.Add(new Guest { peer = peer, received = now });
            }
            foreach (var guest in guests.ToArray())
            {
                for (int i = 0; i < 32 && guest.closeAt == 0 && guest.peer.TryRead(out string json); i++)
                {
                    var packet = JsonUtility.FromJson<Packet>(json);
                    if (packet == null) { Reject(guest, "Invalid lobby message.", now); break; }
                    if (returningFromGame && !guest.resultAcknowledged &&
                        (packet.kind == "input" || packet.kind == "ranking-score" || packet.kind == "ranking-request" ||
                         packet.kind == "world-action" || packet.kind == "minigame"))
                    { guest.received = now; continue; }
                    if (guest.id == 0)
                    {
                        if (packet.kind != "hello" || packet.protocol != Protocol)
                        { Reject(guest, "Please use the same Title / Lobby build.", now); break; }
                        if (guests.Count(g => g.id > 0 && g.closeAt == 0) >= 3)
                        { Reject(guest, "Room full (maximum 4 players).", now); break; }
                        guest.id = guests.Count(g => g.id > 0 && g.closeAt == 0) + 2;
                        guest.admissionOrder = ++nextAdmission;
                        guest.inLobby = false;
                        RosterChanged();
                        Send(guest.peer, new Packet { kind = "welcome", id = guest.id, revision = rosterRevision });
                        BroadcastRoom();
                        Debug.Log("[TitleLobby] JOIN P" + guest.id);
                    }
                    else if (packet.kind == "leave" && !loading)
                    { guest.peer.Dispose(); break; }
                    else if (packet.kind == "prepared" && !loading)
                    {
                        if (guest.inLobby && packet.revision == rosterRevision) guest.prepared = packet.prepared;
                        BroadcastRoom();
                    }
                    else if (packet.kind == "result-ready" && returningFromGame)
                    { guest.resultAcknowledged = true; }
                    else if (packet.kind == "lobby-arrived" && !loading)
                    { guest.inLobby = true; BroadcastRoom(); }
                    else if (packet.kind == "loaded" && loading && !guest.ready && !string.IsNullOrEmpty(packet.catalog))
                    { guest.ready = true; guest.catalog = packet.catalog; }
                    else if (packet.kind != "ping" || guest.ready)
                    { Reject(guest, "Unexpected lobby message.", now); break; }
                    guest.received = now;
                }
                if (guest.peer.Closed || (guest.closeAt > 0 && now >= guest.closeAt) ||
                    now - guest.received > (loading ? LoadTimeout : guest.id == 0 ? 5f : Timeout))
                {
                    if (loading) { Fail("A player disconnected while loading. Please create the room again."); return; }
                    guest.peer.Dispose(); guests.Remove(guest);
                    Debug.Log("[TitleLobby] LEAVE P" + guest.id);
                    if (guest.id > 0) { RosterChanged(); BroadcastRoom(); }
                }
            }
        }

        void Reject(Guest guest, string reason, float now)
        {
            Send(guest.peer, new Packet { kind = "reject", reason = reason });
            guest.closeAt = now + .5f;
        }

        void PollClient(float now)
        {
            for (int i = 0; i < 32 && server != null && server.TryRead(out string json); i++)
            {
                var packet = JsonUtility.FromJson<Packet>(json);
                if (packet == null) { Fail("Invalid Host message."); return; }
                lastReceive = now;
                if (packet.kind == "reject") { Fail(packet.reason); return; }
                if (packet.kind == "closed") { ReturnToTitle("Host closed the room."); return; }
                if (packet.kind == "welcome" && !inRoom && packet.id >= 2 && packet.id <= 4)
                { rosterRevision = packet.revision; EnterRoom(packet.id); }
                else if (packet.kind == "room" && inRoom && packet.players != null && packet.players.Length <= 4 &&
                    packet.id >= 2 && packet.id <= 4 && packet.players.Contains(packet.id) && packet.revision >= rosterRevision)
                {
                    localId = packet.id; rosterRevision = packet.revision;
                    members = packet.players;
                    preparedMembers = packet.preparedPlayers ?? Array.Empty<int>();
                    lobbyMembers = packet.lobbyPlayers ?? Array.Empty<int>();
                }
                else if (packet.kind == "load" && inRoom && !loading) BeginGameLoad();
                else if (packet.kind == "play" && loading && localReady) { Handoff(); return; }
                else { Fail("Unexpected Host message. Please use the same build."); return; }
            }
            if (server != null && server.Closed) ReturnToTitle("Host disconnected. Please join another room.");
        }

        void StartGame()
        {
            if (!host || !inRoom || loading || leaving || !hostPrepared ||
                guests.Any(g => g.id > 0 && g.closeAt == 0 && (!g.prepared || !g.inLobby))) return;
            // Unapproved peers are not part of the loading barrier.
            foreach (var guest in guests.Where(g => g.id == 0 || g.closeAt > 0).ToArray())
            { guest.peer.Dispose(); guests.Remove(guest); }
            listener?.Stop(); listener = null;
            foreach (var guest in guests) Send(guest.peer, new Packet { kind = "load" });
            BeginGameLoad();
        }

        void BeginGameLoad()
        {
            atLobby = returningFromGame = false;
            ResultEntries = Array.Empty<ScoreRankingCoordinator.Entry>();
            loading = true; localReady = false; began = Time.realtimeSinceStartup;
            SetStatus("Loading game... Waiting for all players.");
            LoadScene(GameScene);
        }

        void Handoff()
        {
            if (game == null || !game.LobbyWorldReady) { Fail("Game initialization failed."); return; }
            game.AdoptLobbyConnection(host, internet, localId, relay, server,
                guests.Select(g => g.peer).ToArray(), guests.Select(g => g.id).ToArray());
            handedOff = true;
            relay = null; server = null; guests.Clear();
            busy = inRoom = loading = menu = false;
            if (GAME_MANAGER.StartGate == gameGate) GAME_MANAGER.StartGate = null;
            if (host && GAME_SYNC.Instance != null) GAME_SYNC.Instance.HostStart();
            Debug.Log("[TitleLobby] GAME START P" + localId);
        }

        void Fail(string reason)
        {
            bool returning = loading || (sceneLoad != null && !sceneLoad.isDone) || SceneManager.GetActiveScene().name != TitleScene;
            CloseRoom();
            SetStatus(reason);
            menu = true;
            if (returning) afterLoadError = reason;
        }

        void CloseRoom()
        {
            generation++;
            listener?.Stop(); listener = null;
            socket?.Close(); socket = null;
            if (connectTask != null)
                _ = connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            connectTask = null;
            server?.Dispose(); server = null;
            foreach (var guest in guests) guest.peer.Dispose();
            guests.Clear(); relay?.Dispose(); relay = null;
            busy = host = inRoom = loading = localReady = hostPrepared = leaving = false;
            localId = 0; members = Array.Empty<int>(); code = "";
            preparedMembers = Array.Empty<int>(); rosterRevision = 0;
            lobbyMembers = Array.Empty<int>(); atLobby = returningFromGame = false;
            if (GAME_MANAGER.StartGate == gameGate) GAME_MANAGER.StartGate = null;
        }

        void OnGUI()
        {
            if ((!menu && !busy) || handedOff) return;
            if (SceneManager.GetActiveScene().name == ResultScene) return;
            if (SceneManager.GetActiveScene().name == LobbyScene && PlayerLobbyView.Active != null) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            float scale = Mathf.Min(Screen.width / 900f, Screen.height / 650f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 900 * scale) / 2,
                (Screen.height - 650 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 42 };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 23, fixedHeight = 42 };
            GUI.Box(new Rect(160, 30, 580, 590), GUIContent.none);
            GUILayout.BeginArea(new Rect(180, 45, 540, 560));
            GUILayout.Label(loading ? "Loading Game" : inRoom ? "Player Lobby" : "Multiplayer", new GUIStyle(label) { fontSize = 28 });
            GUILayout.Space(15);
            if (!inRoom)
            {
                GUI.enabled = !busy;
                internet = GUILayout.Toolbar(internet ? 1 : 0, new[] { "LAN (Default)", "Internet" }, button) == 1;
                GUILayout.Space(12);
                if (!internet)
                {
                    GUILayout.Label("Host port", label);
                    portInput = GUILayout.TextField(portInput, 5, field);
                }
                if (GUILayout.Button("Host - Create Room", button)) CreateRoom();
                GUILayout.Space(12);
                GUILayout.Label(internet ? "Internet room code" : "LAN room code (LAN-XXXX-XXXX-XXXX)", label);
                input = GUILayout.TextField(input, 40, field);
                if (GUILayout.Button("Client - Join Room", button)) JoinRoom();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label((internet ? "Internet" : "LAN") + " | " + (host ? "Host" : "Client") + " | You: P" + localId, label);
                GUILayout.Label("Room Code", label);
                GUILayout.TextField(code, field);
                if (GUILayout.Button("Copy Room Code", button)) CopyRoomCode();
                if (host && !internet && !loading && addresses.Length > 1 && GUILayout.Button("Next LAN Address", button))
                {
                    addressIndex = (addressIndex + 1) % addresses.Length;
                    code = LanRoomCode.Encode(addresses[addressIndex], int.Parse(portInput));
                }
                GUILayout.Label("Players: " + members.Length + " / 4   " + string.Join("  ", members.Select(id => "P" + id)), label);
                GUILayout.Space(10);
                GUI.enabled = !loading && !leaving;
                if (GUILayout.Button(IsPlayerPrepared(localId) ? "Cancel Ready" : "Ready", button)) TogglePrepared();
                GUI.enabled = true;
            }
            GUILayout.Space(12);
            GUILayout.Label(status, label);
            GUILayout.FlexibleSpace();
            GUI.enabled = !loading && (sceneLoad == null || sceneLoad.isDone);
            if (GUILayout.Button(busy ? "Leave Room / Cancel" : "Back to Title", button))
            {
                if (busy) LeaveRoom();
                else if (SceneManager.GetActiveScene().name != TitleScene) Fail("Returned to Title.");
                else { menu = false; ShowTitle(true); }
            }
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            CloseRoom(); instance = null;
        }

        void RosterChanged()
        {
            int id = 2;
            foreach (var guest in guests.Where(g => g.id > 0 && g.closeAt == 0).OrderBy(g => g.admissionOrder))
            { guest.id = id++; guest.prepared = false; }
            hostPrepared = false;
            rosterRevision++;
            SetStatus("Players changed. Please confirm Ready again.");
        }

        void BroadcastRoom()
        {
            var active = guests.Where(g => g.id > 0 && g.closeAt == 0).OrderBy(g => g.id).ToArray();
            members = new[] { 1 }.Concat(active.Select(g => g.id)).ToArray();
            preparedMembers = (hostPrepared ? new[] { 1 } : Array.Empty<int>())
                .Concat(active.Where(g => g.prepared).Select(g => g.id)).ToArray();
            lobbyMembers = (atLobby ? new[] { 1 } : Array.Empty<int>()).Concat(active.Where(g => g.inLobby).Select(g => g.id)).ToArray();
            foreach (var guest in active)
                Send(guest.peer, new Packet { kind = "room", id = guest.id, revision = rosterRevision,
                    players = members, preparedPlayers = preparedMembers, lobbyPlayers = lobbyMembers });
        }

        public void CopyRoomCode()
        {
            if (!inRoom) return;
            GUIUtility.systemCopyBuffer = code;
            SetStatus("Room code copied.");
        }

        public void TogglePrepared()
        {
            if (!inRoom || loading || leaving || SceneManager.GetActiveScene().name != LobbyScene ||
                (sceneLoad != null && !sceneLoad.isDone)) return;
            bool prepared = !IsPlayerPrepared(localId);
            if (host) { hostPrepared = prepared; BroadcastRoom(); }
            else Send(server, new Packet { kind = "prepared", revision = rosterRevision, prepared = prepared });
        }

        public void LeaveRoom()
        {
            if (leaving || loading) return;
            if (!inRoom) { ReturnToTitle("Returned to Title."); return; }
            leaving = true; leaveAt = Time.realtimeSinceStartup + .25f;
            if (host)
                foreach (var guest in guests.Where(g => g.id > 0 && g.closeAt == 0))
                    Send(guest.peer, new Packet { kind = "closed" });
            else if (server != null) Send(server, new Packet { kind = "leave" });
        }

        void ReturnToTitle(string reason)
        {
            CloseRoom();
            menu = false; titleMessage = null;
            SetStatus(reason);
            if (sceneLoad != null && !sceneLoad.isDone) afterLoadError = reason;
            else if (SceneManager.GetActiveScene().name != TitleScene) LoadScene(TitleScene);
            else ShowTitle(true);
        }
    }
}
