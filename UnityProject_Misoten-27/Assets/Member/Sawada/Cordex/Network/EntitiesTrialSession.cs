using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sawada.Cordex
{
    [DefaultExecutionOrder(-9000)]
    public sealed class EntitiesTrialSession : MonoBehaviour
    {
        public static EntitiesTrialSession Active { get; private set; }
        public World ClientWorld { get; private set; }
        public bool IsHost => serverWorld != null && serverWorld.IsCreated;
        World serverWorld;
        NetworkWorld map;
        GameObject playerPrefab;
        Transform staging;
        Camera lobbyCamera, view;
        PlayerInput playerInput;
        TestPlayerCameraLookController look;
        JoyconInputBridge joycon;
        readonly Dictionary<int, Avatar> avatars = new Dictionary<int, Avatar>();
        readonly HashSet<int> seenAvatars = new HashSet<int>();
        readonly List<int> removedAvatars = new List<int>(4);
        NetworkRenderPerformance renderPerformance;
        readonly SortedDictionary<int, string[]> worldFragments = new SortedDictionary<int, string[]>();
        int outgoingWorldFrame;
        string address = "127.0.0.1", portText = "7777", code = "", status = "Netcode for Entities movement trial";
        bool internet, busy, automated, gameplayTest, corrected;
        int generation, localId;
        uint sprintPress;
        NetworkTick lastPredictionTick;
        Vector3 lastPredictionPosition, pendingCorrection;
        int correctionCount;
        float started, worldAt, traceAt, exitAfter, connectionStarted;
        StreamWriter trace;
        public int PlayerCount => avatars.Count;
        sealed class Avatar
        {
            public GameObject Root;
            public Transform Body;
            public Vector3 Offset;
            public Vector3 PreviousPosition, PreviousVelocity;
            public bool Ready;
        }

        public void Configure(GameObject prefab, NetworkWorld world, Transform inactive, Camera lobby)
        {
            playerPrefab = prefab; map = world; staging = inactive; lobbyCamera = view = lobby;
        }

        void Start()
        {
            if (Active != null && Active != this) { enabled = false; return; }
            Active = this;
            started = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs();
            renderPerformance = new NetworkRenderPerformance(args);
            renderPerformance.RegisterCamera(lobbyCamera);
            string Arg(string key, string fallback = "")
            { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
            address = Arg("--net-ip", address); portText = Arg("--net-port", portText); code = Arg("--net-code");
            automated = args.Contains("--net-test-motion"); gameplayTest = args.Contains("--net-test-gameplay");
            float.TryParse(Arg("--net-exit-after", "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out exitAfter);
            string path = Arg("--net-trace");
            if (path.Length > 0)
            {
                try { trace = new StreamWriter(path) { AutoFlush = true }; }
                catch (Exception e) { Debug.LogWarning("[EntitiesTrial] Trace: " + e.Message); }
            }
            string mode = Arg("--net-mode");
            if (mode == "host") StartLan(true);
            else if (mode == "client") StartLan(false);
            else if (mode == "relay-host") { internet = true; StartRelay(true); }
            else if (mode == "relay-client") { internet = true; StartRelay(false); }
        }

        void StartLan(bool host)
        {
            if (busy || ClientWorld != null) return;
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            { status = "Port must be 1-65535"; return; }
            if (!NetworkEndpoint.TryParse(address, port, out var endpoint))
            { status = "Enter the Host IPv4 address"; return; }
            try { CreateWorlds(host, NetworkEndpoint.AnyIpv4.WithPort(port), endpoint, null); }
            catch (Exception e) { Stop(e.Message); Debug.LogException(e); }
        }

        async void StartRelay(bool host)
        {
            if (busy || ClientWorld != null) return;
            busy = true;
            int attempt = ++generation;
            status = "Connecting to Unity Relay...";
            try
            {
                await InitializeServices();
                if (attempt != generation || this == null) return;
                RelayServerData data;
                if (host)
                {
                    var allocation = await RelayService.Instance.CreateAllocationAsync(3);
                    if (attempt != generation || this == null) return;
                    code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    data = allocation.ToRelayServerData("dtls");
                }
                else
                {
                    var allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim());
                    data = allocation.ToRelayServerData("dtls");
                }
                if (attempt != generation || this == null) return;
                CreateWorlds(host, NetworkEndpoint.AnyIpv4, data.Endpoint, data);
                if (host) Debug.Log("[EntitiesTrial] ROOM CODE " + code);
            }
            catch (Exception e)
            { if (attempt == generation && this != null) { Stop(e.Message); Debug.LogException(e); } }
            finally { if (attempt == generation) busy = false; }
        }

        static async Task InitializeServices()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("entities" + Guid.NewGuid().ToString("N").Substring(0, 12)));
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Force UDP for both local and remote clients. Thus standalone Host tests exercise the real
        // command/snapshot path too. Relay uses Netcode's own reliable RPC and unreliable command pipelines.
        sealed class Drivers : INetworkStreamDriverConstructor
        {
            public RelayServerData? Relay;
            public void CreateClientDriver(World world, ref NetworkDriverStore store, NetDebug debug)
            {
                var settings = DefaultDriverBuilder.GetNetworkClientSettings();
                if (Relay.HasValue) { var relay = Relay.Value; settings.WithRelayParameters(ref relay); }
                DefaultDriverBuilder.RegisterClientUdpDriver(world, ref store, debug, settings);
            }
            public void CreateServerDriver(World world, ref NetworkDriverStore store, NetDebug debug)
            {
                var settings = DefaultDriverBuilder.GetNetworkServerSettings();
                if (Relay.HasValue)
                    DefaultDriverBuilder.RegisterServerUdpDriver(world, ref store, debug, settings);
                if (Relay.HasValue) { var relay = Relay.Value; settings.WithRelayParameters(ref relay); }
                DefaultDriverBuilder.RegisterServerUdpDriver(world, ref store, debug, settings);
            }
        }

        void CreateWorlds(bool host, NetworkEndpoint listen, NetworkEndpoint connect, RelayServerData? relay)
        {
            var previous = NetworkStreamReceiveSystem.DriverConstructor;
            try
            {
                NetworkStreamReceiveSystem.DriverConstructor = new Drivers { Relay = relay };
                if (host)
                {
                    serverWorld = ClientServerBootstrap.CreateServerWorld("Sawada Trial Server");
                    using var query = serverWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                    var driver = query.GetSingletonRW<NetworkStreamDriver>();
                    if (!driver.ValueRW.Listen(listen)) throw new InvalidOperationException("Host could not bind UDP port");
                }
                // The host's own client uses local UDP, bypassing Relay but sharing the server driver.
                NetworkStreamReceiveSystem.DriverConstructor = new Drivers { Relay = host ? null : relay };
                ClientWorld = ClientServerBootstrap.CreateClientWorld("Sawada Trial Client");
                using var clientQuery = ClientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                var clientDriver = clientQuery.GetSingletonRW<NetworkStreamDriver>();
                if (host)
                {
                    using var serverQuery = serverWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                    var endpoint = serverQuery.GetSingleton<NetworkStreamDriver>().GetLocalEndPoint();
                    connect = NetworkEndpoint.LoopbackIpv4.WithPort(endpoint.Port);
                }
                clientDriver.ValueRW.Connect(ClientWorld.EntityManager, connect);
            }
            finally { NetworkStreamReceiveSystem.DriverConstructor = previous; }
            connectionStarted = Time.realtimeSinceStartup;
            localId = 0; sprintPress = 0; corrected = false;
            map.SetHost(host);
            status = host ? "Host started (UDP)" : "Connecting (UDP)...";
        }

        void Update()
        {
            renderPerformance?.Tick(Time.unscaledDeltaTime);
            float now = Time.realtimeSinceStartup;
            if (exitAfter > 0 && now - started >= exitAfter) { Application.Quit(); return; }
            if (ClientWorld == null) return;
            try
            {
                PollClient();
                if (IsHost) PollServer(now);
                if (localId == 0 && now - connectionStarted > 15) { Stop("Connection timed out"); return; }
            }
            catch (Exception e) { Stop(e.Message); Debug.LogException(e); }
        }

        static void SendRpc<T>(EntityManager em, T payload, Entity target = default) where T : unmanaged, IRpcCommand
        {
            var rpc = em.CreateEntity();
            em.AddComponentData(rpc, payload);
            em.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = target });
        }

        void PollClient()
        {
            var em = ClientWorld.EntityManager;
            using var connectionQuery = em.CreateEntityQuery(typeof(NetworkId));
            if (connectionQuery.IsEmptyIgnoreFilter)
            {
                if (localId != 0) Stop("Host disconnected");
                return;
            }
            var connection = connectionQuery.GetSingletonEntity();
            localId = em.GetComponentData<NetworkId>(connection).Value;
            if (!em.HasComponent<TrialJoined>(connection))
            {
                em.AddComponent<TrialJoined>(connection);
                em.AddComponent<NetworkStreamInGame>(connection);
                SendRpc(em, new TrialJoinRpc { Catalog = map.Catalog }, connection);
                status = "Connected. You are P" + localId;
            }
            using var rpcs = em.CreateEntityQuery(typeof(TrialWorldRpc), typeof(ReceiveRpcCommandRequest));
            using var messages = rpcs.ToEntityArray(Allocator.Temp);
            foreach (var entity in messages)
            {
                var fragment = em.GetComponentData<TrialWorldRpc>(entity);
                if (fragment.Frame <= 0 || fragment.Parts == 0 || fragment.Parts > 16 || fragment.Part >= fragment.Parts)
                    throw new InvalidOperationException("Invalid world fragment");
                if (!worldFragments.TryGetValue(fragment.Frame, out var parts))
                    worldFragments.Add(fragment.Frame, parts = new string[fragment.Parts]);
                if (parts.Length != fragment.Parts) throw new InvalidOperationException("World fragment count changed");
                parts[fragment.Part] = fragment.Json.ToString();
                em.DestroyEntity(entity);
            }
            if (worldFragments.Count > 64) throw new InvalidOperationException("World fragment queue overflow");
            while (worldFragments.Count > 0)
            {
                var frame = worldFragments.First();
                if (frame.Value.Any(part => part == null)) break;
                var message = JsonUtility.FromJson<NetworkTestSession.Message>(string.Concat(frame.Value));
                if (message.kind == "world") map.Receive(message);
                worldFragments.Remove(frame.Key);
            }
        }

        void PollServer(float now)
        {
            var em = serverWorld.EntityManager;
            using var prefabQuery = em.CreateEntityQuery(typeof(TrialPrefab));
            if (prefabQuery.IsEmptyIgnoreFilter) return;
            using var joinQuery = em.CreateEntityQuery(typeof(TrialJoinRpc), typeof(ReceiveRpcCommandRequest));
            using var joins = joinQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in joins)
            {
                var source = em.GetComponentData<ReceiveRpcCommandRequest>(entity).SourceConnection;
                using var players = em.CreateEntityQuery(typeof(TrialMotion));
                if (em.Exists(source) && em.HasComponent<NetworkId>(source) && !em.HasComponent<NetworkStreamInGame>(source))
                {
                    if (em.GetComponentData<TrialJoinRpc>(entity).Catalog.ToString() != map.Catalog || players.CalculateEntityCount() >= 4)
                        em.AddComponentData(source, new NetworkStreamRequestDisconnect());
                    else
                    {
                        int id = em.GetComponentData<NetworkId>(source).Value;
                        var player = em.Instantiate(prefabQuery.GetSingleton<TrialPrefab>().Value);
                        em.SetComponentData(player, new GhostOwner { NetworkId = id });
                        em.SetComponentData(player, new TrialMotion { Position = new float3((id - 2.5f) * 3, 1.72f, 0) });
                        em.AddComponent<NetworkStreamInGame>(source);
                        var linked = em.HasBuffer<LinkedEntityGroup>(source) ? em.GetBuffer<LinkedEntityGroup>(source) : em.AddBuffer<LinkedEntityGroup>(source);
                        if (linked.Length == 0) linked.Add(source);
                        linked.Add(player);
                        Debug.Log("[EntitiesTrial] SPAWN P" + id);
                    }
                }
                em.DestroyEntity(entity);
            }
            map.TickHost();
            if (now >= worldAt)
            {
                worldAt = now + .1f;
                foreach (var message in map.Capture())
                {
                    string json = JsonUtility.ToJson(message);
                    if (System.Text.Encoding.UTF8.GetByteCount(json) > 4093) throw new InvalidOperationException("World RPC exceeds trial payload limit");
                    int frame = ++outgoingWorldFrame;
                    int parts = (json.Length + 399) / 400;
                    for (int part = 0; part < parts; part++)
                        SendRpc(em, new TrialWorldRpc { Frame = frame, Part = (ushort)part, Parts = (ushort)parts,
                            Json = json.Substring(part * 400, Math.Min(400, json.Length - part * 400)) });
                }
            }
            if (!corrected && Environment.GetCommandLineArgs().Contains("--net-test-correction") && now - started > 5)
            {
                using var query = em.CreateEntityQuery(typeof(TrialMotion));
                using var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                { var motion = em.GetComponentData<TrialMotion>(entity); motion.Position.x += 3; em.SetComponentData(entity, motion); }
                corrected = true;
                Debug.Log("[EntitiesTrial] AUTHORITATIVE CORRECTION +3m");
            }
        }

        public TrialInput ReadInput()
        {
            if (automated || gameplayTest)
            {
                float time = Time.realtimeSinceStartup - started;
                float sign = ((int)(time / 2) % 2 == 0) ? 1 : -1;
                return new TrialInput { Move = new float2(sign, 0), Forward = new float2(sign, 0), SprintPress = gameplayTest && time >= 2 ? 1u : 0u };
            }
            var command = new TrialInput { SprintPress = sprintPress, Forward = new float2(0, 1) };
            if (!Application.isFocused || playerInput == null) return command;
            var move = playerInput.actions.FindAction("Move", false);
            Vector2 input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            Vector3 forward = view != null ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 direction = Vector3.ClampMagnitude(Vector3.Cross(Vector3.up, forward) * input.x + forward * input.y, 1);
            command.Move = new float2(direction.x, direction.z);
            command.Forward = new float2(forward.x, forward.z);
            command.SprintPress = sprintPress;
            return command;
        }

        // Compare the same full tick before/after Netcode replay. Normal forward movement never
        // adds visual lag. This history affects presentation only, not the authoritative motor.
        public void RecordPrediction(int id, NetworkTick tick, Vector3 position)
        {
            if (id != localId) return;
            if (lastPredictionTick.IsValid && tick == lastPredictionTick)
            {
                Vector3 correction = lastPredictionPosition - position;
                if (correction.sqrMagnitude > .000001f)
                { pendingCorrection += correction; correctionCount++; }
                lastPredictionPosition = position;
            }
            else if (!lastPredictionTick.IsValid || tick.IsNewerThan(lastPredictionTick))
            { lastPredictionTick = tick; lastPredictionPosition = position; }
        }

        void LateUpdate()
        {
            if (ClientWorld == null || !ClientWorld.IsCreated) return;
            if (Application.isFocused && playerInput != null && playerInput.actions.FindAction("Sprint", false)?.WasPressedThisFrame() == true) sprintPress++;
            var em = ClientWorld.EntityManager;
            using var query = em.CreateEntityQuery(typeof(TrialMotion), typeof(GhostOwner));
            using var entities = query.ToEntityArray(Allocator.Temp);
            seenAvatars.Clear();
            foreach (var entity in entities)
            {
                int id = em.GetComponentData<GhostOwner>(entity).NetworkId;
                seenAvatars.Add(id);
                var motion = em.GetComponentData<TrialMotion>(entity);
                if (!avatars.TryGetValue(id, out var avatar)) avatars.Add(id, avatar = CreateAvatar(id));
                Vector3 position = motion.Position;
                // Simulation snaps to the authoritative replay result. Only presentation keeps an offset.
                if (id == localId)
                {
                    var correction = pendingCorrection;
                    // A clock catch-up may skip our cached full tick. Preserve a large correction
                    // visually in that case too, while ordinary motion still follows immediately.
                    var displacement = position - avatar.PreviousPosition;
                    var expected = avatar.PreviousVelocity * Time.unscaledDeltaTime;
                    if (avatar.Ready && correction.magnitude < .75f && (displacement - expected).magnitude > .75f)
                        correction += expected - displacement;
                    if (correction.magnitude < 4) avatar.Offset += correction;
                    else avatar.Offset = Vector3.zero;
                    pendingCorrection = Vector3.zero;
                }
                avatar.Offset *= Mathf.Exp(-20 * Time.unscaledDeltaTime);
                avatar.Body.SetPositionAndRotation(position + avatar.Offset, Quaternion.Euler(0, motion.Yaw, 0));
                avatar.PreviousPosition = position; avatar.PreviousVelocity = motion.Velocity; avatar.Ready = true;
                if (Time.realtimeSinceStartup >= traceAt)
                    trace?.WriteLine(JsonUtility.ToJson(new Sample { role = "client", id = id, owner = localId, time = Time.realtimeSinceStartup - started,
                        x = position.x, y = position.y, z = position.z, sprint = motion.Sprinting,
                        corrections = id == localId ? correctionCount : 0, visualOffset = avatar.Offset.magnitude }));
            }
            removedAvatars.Clear();
            foreach (int id in avatars.Keys)
                if (!seenAvatars.Contains(id)) removedAvatars.Add(id);
            foreach (int id in removedAvatars)
            { Destroy(avatars[id].Root); avatars.Remove(id); }
            if (Time.realtimeSinceStartup >= traceAt)
            {
                if (IsHost)
                {
                    using var serverQuery = serverWorld.EntityManager.CreateEntityQuery(typeof(TrialMotion), typeof(GhostOwner));
                    using var serverEntities = serverQuery.ToEntityArray(Allocator.Temp);
                    foreach (var entity in serverEntities)
                    {
                        var motion = serverWorld.EntityManager.GetComponentData<TrialMotion>(entity);
                        trace?.WriteLine(JsonUtility.ToJson(new Sample { role = "server", id = serverWorld.EntityManager.GetComponentData<GhostOwner>(entity).NetworkId,
                            time = Time.realtimeSinceStartup - started, x = motion.Position.x, y = motion.Position.y, z = motion.Position.z, sprint = motion.Sprinting }));
                    }
                }
                traceAt = Time.realtimeSinceStartup + .1f;
            }
            if (look != null) look.Look();
        }

        [Serializable] sealed class Sample { public string role; public int id, owner, corrections; public float time, x, y, z, visualOffset; public bool sprint; }

        Avatar CreateAvatar(int id)
        {
            bool owned = id == localId;
            var copy = Instantiate(playerPrefab, staging);
            copy.SetActive(false);
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                script.enabled = script is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData || (owned &&
                    (script is Unity.Cinemachine.CinemachineCamera || script is Unity.Cinemachine.CinemachineThirdPersonFollow ||
                     script is Unity.Cinemachine.CinemachineBrain || script is TestPlayerCameraLookController));
            foreach (var brain in copy.GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true))
            { brain.UpdateMethod = Unity.Cinemachine.CinemachineBrain.UpdateMethods.LateUpdate; brain.BlendUpdateMethod = Unity.Cinemachine.CinemachineBrain.BrainUpdateMethods.LateUpdate; }
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None; }
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var camera in copy.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = owned && camera.name == "PlayerCamera";
                if (camera.enabled)
                {
                    camera.targetDisplay = 0; camera.targetTexture = null; view = camera;
                    renderPerformance?.RegisterCamera(camera);
                    if (lobbyCamera != null) lobbyCamera.enabled = false;
                }
            }
            foreach (var listener in copy.GetComponentsInChildren<AudioListener>(true)) listener.enabled = owned && listener.name == "PlayerCamera";
            var bodyTransform = copy.GetComponentInChildren<Rigidbody>(true).transform;
            var cameraLook = copy.GetComponentInChildren<TestPlayerCameraLookController>(true);
            if (cameraLook != null) { cameraLook.ConfigureNetworkSplines(GameObject.Find("Splines")?.transform); if (owned) look = cameraLook; }
            if (owned)
            {
                playerInput = copy.GetComponent<PlayerInput>();
                if (playerInput != null) { playerInput.defaultControlScheme = ""; playerInput.enabled = true; }
                var manager = FindObjectsByType<JoyconManager>(FindObjectsSortMode.None).FirstOrDefault(m => m.gameObject.scene == gameObject.scene);
                joycon = manager != null ? manager.GetComponent<JoyconInputBridge>() : null;
                if (joycon != null) joycon.SetPlayerInput(playerInput);
            }
            copy.name = "Entities Trial P" + id;
            copy.transform.SetParent(transform, true);
            copy.SetActive(true);
            return new Avatar { Root = copy, Body = bodyTransform };
        }

        public bool TryGetPosition(int id, out Vector3 position)
        {
            var world = IsHost ? serverWorld : ClientWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(typeof(TrialMotion), typeof(GhostOwner));
                using var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                    if (em.GetComponentData<GhostOwner>(entity).NetworkId == id)
                    { position = em.GetComponentData<TrialMotion>(entity).Position; return true; }
            }
            position = default; return false;
        }

        public void Stop(string reason = "Disconnected")
        {
            generation++; busy = false;
            if (view != lobbyCamera) renderPerformance?.UnregisterCamera(view);
            if (joycon != null) joycon.SetPlayerInput(null);
            joycon = null; playerInput = null; look = null;
            foreach (var avatar in avatars.Values) { avatar.Root.SetActive(false); Destroy(avatar.Root); }
            avatars.Clear();
            worldFragments.Clear(); outgoingWorldFrame = 0;
            lastPredictionTick = default; pendingCorrection = Vector3.zero; correctionCount = 0;
            DisposeWorld(ClientWorld); ClientWorld = null;
            DisposeWorld(serverWorld); serverWorld = null;
            map?.Reset(); localId = 0;
            view = lobbyCamera; if (lobbyCamera != null) lobbyCamera.enabled = true;
            status = reason;
            Debug.Log("[EntitiesTrial] " + reason);
        }

        static void DisposeWorld(World world)
        {
            if (world == null || !world.IsCreated) return;
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
            world.Dispose();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 450, 460));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("NETCODE FOR ENTITIES | MOVEMENT TRIAL");
            GUI.enabled = !busy && ClientWorld == null;
            internet = GUILayout.Toolbar(internet ? 1 : 0, new[] { "LAN / UDP", "Unity Relay" }) == 1;
            if (internet) { GUILayout.Label("Room code"); code = GUILayout.TextField(code, 16); }
            else { GUILayout.Label("Host IPv4 / UDP port"); address = GUILayout.TextField(address, 45); portText = GUILayout.TextField(portText, 5); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host")) { if (internet) StartRelay(true); else StartLan(true); }
            if (GUILayout.Button("Join Client")) { if (internet) StartRelay(false); else StartLan(false); }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(status);
            GUILayout.Label($"Players: {avatars.Count}/4  You: P{localId}  Simulation: 50Hz / snapshots: 25Hz");
            GUILayout.Label("Prediction corrections: " + correctionCount);
            if (renderPerformance != null)
            {
                GUI.enabled = renderPerformance.CanConfigure;
                bool lightweight = GUILayout.Toggle(renderPerformance.Lightweight, "Lightweight graphics (reduce rendering load)");
                if (lightweight != renderPerformance.Lightweight) renderPerformance.SetLightweight(lightweight);
                GUI.enabled = true;
                GUILayout.Label(renderPerformance.Status);
            }
            GUILayout.Label("WASD / left stick: Move | Shift: Sprint\nTrial: static map collision. Equipment / theft / dynamic contacts pending migration.");
            if ((busy || ClientWorld != null) && GUILayout.Button("Stop / Disconnect")) Stop();
            GUILayout.EndVertical(); GUILayout.EndArea();
        }

        void OnDestroy()
        {
            Stop("Session closed");
            renderPerformance?.Dispose(); renderPerformance = null;
            trace?.Dispose(); trace = null;
            if (Active == this) Active = null;
        }
    }
}
