using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    public sealed partial class NetworkTestSession
    {
        public enum MiniGamePhase { Closed, Countdown, Playing, Result }

        [Serializable] public sealed class MiniGamePose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool active;
        }

        [Serializable] public sealed class MiniGamePacket
        {
            public string operation;
            public int game, session, player, sequence, score, stars, part, parts;
            public MiniGamePhase phase;
            public float remaining;
            public bool clear;
            public MiniGameNetworkInput input;
            public MiniGamePose[] poses;
        }

        sealed class MiniGameSlot
        {
            public MiniGameSystem system;
            public MiniGame game;
            public MiniGameZone[] zones;
            public Transform[] transforms;
            public NetworkMiniGameSettings settings;
            public MiniGamePacket state;
            public int lastInputSequence, lastStateSequence;
            public float deadline, inputAt, sendAt;
            public MiniGameNetworkInput input = MiniGameNetworkInput.Neutral;
            public bool activated;
            public MiniGamePacket pending;
            public readonly List<MiniGamePose> pendingPoses = new List<MiniGamePose>();
            public int pendingPart;
        }

        static readonly List<NetworkTestSession> miniGameSessions = new List<NetworkTestSession>();
        readonly List<MiniGameSlot> miniGames = new List<MiniGameSlot>();
        readonly Dictionary<Collider, bool> miniGameColliders = new Dictionary<Collider, bool>();
        readonly Dictionary<Rigidbody, bool> miniGameBodies = new Dictionary<Rigidbody, bool>();
        readonly Dictionary<int, float> miniGameRequestTimes = new Dictionary<int, float>();
        readonly Dictionary<MiniGame, int> appliedMiniGameResults = new Dictionary<MiniGame, int>();
        readonly Dictionary<int, bool> miniGameFrozenPlayers = new Dictionary<int, bool>();
        readonly Dictionary<StateMachine, bool> miniGameMovementStates = new Dictionary<StateMachine, bool>();
        string miniGameCatalog = "";
        int nextMiniGameSession, localMiniGameSequence;
        float miniGameInputAt, miniGameRequestAt, keyboardSwing;
        bool previousMiniGameButton, miniGameViewActive;
        Camera miniGamePreviousView;
        public event Action<MiniGamePacket> MiniGameStateChanged;
        public MiniGamePacket LocalMiniGame => miniGames.Select(s => s.state)
            .FirstOrDefault(s => s != null && s.player == localId && s.phase != MiniGamePhase.Closed);
        string SessionCatalog => world.Catalog + ":" + miniGameCatalog;

        public int GetMiniGameId(MiniGameSystem system) => miniGames.FindIndex(s => s.system == system) + 1;

        public static bool HasMiniGameNetworking(Scene scene)
            => miniGameSessions.Any(s => s != null && s.isActiveAndEnabled && s.gameObject.scene == scene);

        string MiniGameHierarchy(Transform item) => item.parent == null
            ? item.GetSiblingIndex() + ":" + item.name : MiniGameHierarchy(item.parent) + "/" + item.GetSiblingIndex() + ":" + item.name;

        void InitializeMiniGames()
        {
            miniGameSessions.RemoveAll(s => s == null);
            miniGameSessions.Add(this);
            var scene = gameObject.scene;
            var zones = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MiniGameZone>(true)).ToArray();
            foreach (var system in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MiniGameSystem>(true))
                .OrderBy(s => MiniGameHierarchy(s.transform), StringComparer.Ordinal))
            {
                if (system.Game == null) continue;
                var transforms = system.Game.NetworkObjects.Where(o => o != null)
                    .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                    .Concat(system.GetComponentsInChildren<SensorObject>(true).SelectMany(o => o.GetComponentsInChildren<Transform>(true)))
                    .Distinct().ToArray();
                // Bound snapshot size; do not silently truncate gameplay objects.
                if (transforms.Length > 128) { Debug.LogError("[MiniGame] Too many transforms: " + system.name); continue; }
                miniGames.Add(new MiniGameSlot { system = system, game = system.Game,
                    zones = zones.Where(z => z.System == system).ToArray(), transforms = transforms,
                    settings = system.GetComponent<NetworkMiniGameSettings>() });
            }
            string signature = string.Join("\n", miniGames.Select(s => MiniGameHierarchy(s.system.transform)
                + ":" + s.game.GetType().FullName + ":" + string.Join(",", s.transforms.Select(t => MiniGameHierarchy(t)))
                + ":" + string.Join(",", s.zones.Select(z => MiniGameHierarchy(z.transform)))));
            using (var hash = SHA256.Create()) miniGameCatalog = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(signature)));
        }

        bool IsMiniGamePlayer(int player) => player > 0 && miniGames.Any(s => s.state != null
            && s.state.player == player && s.state.phase != MiniGamePhase.Closed);

        bool NearMiniGame(MiniGameSlot slot, int player)
        {
            if (!TryGetPlayerPosition(player, out var position)) return false;
            return slot.zones.Any(z => z != null && z.isActiveAndEnabled
                && Vector3.Distance(position, z.transform.position) <= 3f);
        }

        /// <summary>Queue an entry request; acceptance arrives through MiniGameStateChanged.</summary>
        public bool RequestMiniGame(int gameId)
        {
            if (localId <= 0 || gameId < 1 || gameId > miniGames.Count || IsMiniGamePlayer(localId)
                || Time.realtimeSinceStartup < miniGameRequestAt) return false;
            miniGameRequestAt = Time.realtimeSinceStartup + .3f;
            var request = new MiniGamePacket { operation = "start", game = gameId };
            if (hosting) return HandleMiniGameRequest(localId, request, Time.realtimeSinceStartup);
            if (server == null) return false;
            Send(server, new Message { kind = "minigame", miniGame = request });
            return true;
        }

        public void CancelNetworkMiniGame()
        {
            var state = LocalMiniGame;
            if (state == null) return;
            var request = new MiniGamePacket { operation = "cancel", game = state.game, session = state.session };
            if (hosting) HandleMiniGameRequest(localId, request, Time.realtimeSinceStartup);
            else if (server != null) Send(server, new Message { kind = "minigame", miniGame = request });
        }

        bool HandleMiniGameRequest(int player, MiniGamePacket packet, float now)
        {
            if (!hosting || packet == null || packet.game < 1 || packet.game > miniGames.Count) return false;
            var slot = miniGames[packet.game - 1];
            if (packet.operation == "start")
            {
                if (miniGameRequestTimes.TryGetValue(player, out float previous) && now - previous < .25f) return true;
                miniGameRequestTimes[player] = now;
                if (IsMiniGamePlayer(player) || ranking.IsCollecting || !slot.system.isActiveAndEnabled
                    || (slot.state != null && slot.state.phase != MiniGamePhase.Closed) || !NearMiniGame(slot, player)) return true;
                slot.game.ConfigureNetwork(true, true);
                slot.input = MiniGameNetworkInput.Neutral;
                slot.game.SetNetworkInput(slot.input);
                slot.lastInputSequence = 0;
                slot.activated = false;
                slot.inputAt = now;
                slot.deadline = Time.time + Mathf.Max(1f, slot.system.CountdownSeconds);
                slot.state = new MiniGamePacket { operation = "state", game = packet.game,
                    session = ++nextMiniGameSession, player = player, phase = MiniGamePhase.Countdown };
                PublishMiniGame(slot);
                return true;
            }
            if (packet.operation != "input" && packet.operation != "cancel") return false;
            if (slot.state == null || slot.state.session != packet.session || slot.state.player != player
                || slot.state.phase == MiniGamePhase.Closed) return true; // Old session / non-owner: never execute it.
            if (packet.operation == "cancel") { CloseMiniGame(slot); return true; }
            if (!packet.input.IsValid() || packet.sequence <= 0) return false;
            if (packet.sequence <= slot.lastInputSequence || now - slot.inputAt < .01f) return true;
            slot.lastInputSequence = packet.sequence;
            // Preserve a button release until the gameplay Update has consumed it.
            bool release = slot.input.release || (!slot.input.button && packet.input.release)
                || (slot.input.button && !packet.input.button);
            slot.input = packet.input;
            slot.input.release = release;
            slot.inputAt = now;
            return true;
        }

        void UpdateMiniGameInput(float now)
        {
            if (localId <= 0) return;
            var manager = FindObjectsByType<JoyconManager>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.scene == gameObject.scene);
            var device = manager != null && !manager.GetIsKeyboardPlay() && JoyconManager.Instance != null
                ? localJoyconBridge != null ? localJoyconBridge.Source
                    : JoyconManager.Instance.j?.FirstOrDefault(j => j != null && j.state >= Joycon.state_.ATTACHED) : null;
            if (device != null && device.state < Joycon.state_.ATTACHED) device = null;
            var keyboard = Keyboard.current;
            bool start = keyboard != null && keyboard.zKey.wasPressedThisFrame;
            if (device != null) start |= device.GetButtonDown(Joycon.Button.SHOULDER_2);
            var state = LocalMiniGame;
            if (state == null)
            {
                keyboardSwing = 0f;
                previousMiniGameButton = false;
                if (start && TryGetPlayerPosition(localId, out var position))
                {
                    int nearest = Enumerable.Range(1, miniGames.Count).Where(i => NearMiniGame(miniGames[i - 1], localId))
                        .OrderBy(i => miniGames[i - 1].zones.Min(z => (z.transform.position - position).sqrMagnitude)).FirstOrDefault();
                    if (nearest > 0) RequestMiniGame(nearest);
                }
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { CancelNetworkMiniGame(); return; }
            if (now < miniGameInputAt) return;
            miniGameInputAt = now + .02f;
            var input = MiniGameNetworkInput.Neutral;
            if (device != null)
            {
                input.acceleration = device.GetAccel(); input.gyro = device.GetGyro();
                input.orientation = device.GetVector(); input.isLeft = device.isLeft;
                input.button = device.GetButton(Joycon.Button.SHOULDER_1);
                if (device.GetButtonDown(Joycon.Button.DPAD_DOWN)) device.Recenter();
            }
            else if (keyboard != null)
            {
                keyboardSwing = Mathf.MoveTowards(keyboardSwing, keyboard.zKey.isPressed ? -2f : 0f, .024f);
                input.acceleration = new Vector3(keyboard.wKey.isPressed ? 2f : keyboard.sKey.isPressed ? -2f : 0f,
                    keyboardSwing, keyboard.dKey.isPressed ? 2f : keyboard.aKey.isPressed ? -2f : 0f);
                input.gyro = new Vector3(0f, 0f, keyboard.dKey.isPressed ? 4f : keyboard.aKey.isPressed ? -4f : 0f);
                input.orientation = Quaternion.Euler((keyboard.upArrowKey.isPressed ? -30f : 0f)
                    + (keyboard.downArrowKey.isPressed ? 30f : 0f), (keyboard.rightArrowKey.isPressed ? 30f : 0f)
                    + (keyboard.leftArrowKey.isPressed ? -30f : 0f), 0f);
                input.button = keyboard.spaceKey.isPressed;
            }
            input.release = previousMiniGameButton && !input.button;
            previousMiniGameButton = input.button;
            // Invalid device samples (e.g. during reconnect) must not disconnect the room.
            if (!input.IsValid()) input = MiniGameNetworkInput.Neutral;
            var packet = new MiniGamePacket { operation = "input", game = state.game, session = state.session,
                sequence = ++localMiniGameSequence, input = input };
            if (hosting) HandleMiniGameRequest(localId, packet, now);
            else if (server != null) Send(server, new Message { kind = "minigame", miniGame = packet });
        }

        void UpdateNetworkMiniGames(float now)
        {
            foreach (var slot in miniGames)
            {
                if (localId > 0) ConfigureMiniGamePhysics(slot, hosting);
                var state = slot.state;
                if (!hosting || state == null || state.phase == MiniGamePhase.Closed) continue;
                if (!states.ContainsKey(state.player) || !slot.system.isActiveAndEnabled) { CloseMiniGame(slot); continue; }
                if (state.phase == MiniGamePhase.Countdown && Time.time >= slot.deadline)
                {
                    slot.game.Activate();
                    slot.activated = true;
                    state.phase = MiniGamePhase.Playing;
                    slot.deadline = Time.time + (slot.settings != null ? Mathf.Max(1f, slot.settings.fallbackTimeLimit) : 10f);
                }
                if (state.phase == MiniGamePhase.Playing)
                {
                    // Results are read on the next session Update, after game/physics callbacks have set the score.
                    if (slot.game.GetIsFinish()) FinishNetworkMiniGame(slot, slot.game.GetIsGameClear());
                    else if (slot.game.NetworkRemainingSeconds < 0f && Time.time >= slot.deadline)
                        FinishNetworkMiniGame(slot, false);
                    else if (now - slot.inputAt > 3f) { CloseMiniGame(slot); continue; }
                    else
                    {
                        slot.game.SetNetworkInput(now - slot.inputAt <= .25f ? slot.input : MiniGameNetworkInput.Neutral);
                        slot.input.release = false;
                    }
                }
                if (state.phase == MiniGamePhase.Result && Time.time >= slot.deadline) { CloseMiniGame(slot); continue; }
                if (now >= slot.sendAt) { slot.sendAt = now + .05f; PublishMiniGame(slot); }
            }
            foreach (var pair in bodies)
            {
                bool frozen = hosting && IsMiniGamePlayer(pair.Key);
                if (frozen && !miniGameFrozenPlayers.ContainsKey(pair.Key))
                {
                    miniGameFrozenPlayers[pair.Key] = pair.Value.isKinematic;
                    pair.Value.linearVelocity = Vector3.zero;
                    pair.Value.angularVelocity = Vector3.zero;
                    pair.Value.isKinematic = true;
                    if (movers.TryGetValue(pair.Key, out var mover))
                    {
                        var machine = mover.GetComponent<StateMachine>();
                        machine.ChangeState<Player_Idle>();
                        miniGameMovementStates[machine] = machine.enabled;
                        machine.enabled = false;
                        mover.EndSprintCameraEffect();
                    }
                }
                else if (!frozen && miniGameFrozenPlayers.TryGetValue(pair.Key, out bool kinematic))
                {
                    pair.Value.isKinematic = kinematic; miniGameFrozenPlayers.Remove(pair.Key);
                    if (movers.TryGetValue(pair.Key, out var mover))
                    {
                        var machine = mover.GetComponent<StateMachine>();
                        if (miniGameMovementStates.TryGetValue(machine, out bool wasEnabled)) machine.enabled = wasEnabled;
                        miniGameMovementStates.Remove(machine);
                    }
                }
            }
            // Retry when a result arrives before the avatar's first state snapshot.
            foreach (var slot in miniGames.Where(s => s.state != null && s.state.phase == MiniGamePhase.Result))
                ApplyMiniGameResult(slot);
        }

        void FinishNetworkMiniGame(MiniGameSlot slot, bool clear)
        {
            var state = slot.state;
            state.score = clear ? Mathf.Max(0, slot.game.GetScore()) : 0;
            state.clear = clear;
            state.stars = slot.settings != null ? slot.settings.Evaluate(state.score, clear)
                : !clear ? 0 : state.score >= 150 ? 3 : state.score >= 100 ? 2 : 1;
            state.phase = MiniGamePhase.Result;
            slot.deadline = Time.time + (slot.settings != null ? Mathf.Max(.1f, slot.settings.resultSeconds) : 2f);
            // Capture the final pose before Deactivate resets the axe/counters.
            state.poses = CaptureMiniGamePoses(slot);
            slot.game.Deactivate(); slot.activated = false;
            PublishMiniGame(slot);
        }

        MiniGamePose[] CaptureMiniGamePoses(MiniGameSlot slot) => slot.transforms.Select(t => new MiniGamePose
        { position = t.localPosition, rotation = t.localRotation, scale = t.localScale, active = t.gameObject.activeSelf }).ToArray();

        void PublishMiniGame(MiniGameSlot slot)
        {
            var state = slot.state;
            state.sequence++;
            state.remaining = state.phase == MiniGamePhase.Playing && slot.game.NetworkRemainingSeconds >= 0f
                ? slot.game.NetworkRemainingSeconds : Mathf.Max(0f, slot.deadline - Time.time);
            if (state.phase != MiniGamePhase.Result) state.poses = CaptureMiniGamePoses(slot);
            state.part = 0;
            state.parts = Math.Max(1, (state.poses.Length + 11) / 12);
            ApplyMiniGameResult(slot);
            PresentMiniGame(slot);
            MiniGameStateChanged?.Invoke(JsonUtility.FromJson<MiniGamePacket>(JsonUtility.ToJson(state)));
            // TcpPeer/Relay have an 8 KB frame limit. Complete snapshots are reassembled before applying them.
            for (int part = 0; part < state.parts; part++)
            {
                var fragment = new MiniGamePacket { operation = "state", game = state.game, session = state.session,
                    player = state.player, sequence = state.sequence, score = state.score, stars = state.stars,
                    phase = state.phase, remaining = state.remaining, clear = state.clear, part = part, parts = state.parts,
                    poses = state.poses.Skip(part * 12).Take(12).ToArray() };
                var message = new Message { kind = "minigame", miniGame = fragment };
                foreach (var c in connections) if (c.id > 0 && c.closeAt == 0) Send(c.peer, message);
            }
        }

        bool ReceiveMiniGameState(MiniGamePacket packet)
        {
            if (packet == null || packet.operation != "state" || packet.game < 1 || packet.game > miniGames.Count
                || packet.player < 1 || packet.player > 4 || packet.session <= 0 || packet.sequence <= 0
                || packet.phase < MiniGamePhase.Closed || packet.phase > MiniGamePhase.Result
                || !Finite(packet.remaining) || packet.remaining < 0f || packet.score < 0 || packet.stars < 0 || packet.stars > 3) return false;
            var slot = miniGames[packet.game - 1];
            if (packet.parts != Math.Max(1, (slot.transforms.Length + 11) / 12) || packet.part < 0 || packet.part >= packet.parts
                || packet.poses == null || packet.poses.Length != Math.Min(12, slot.transforms.Length - packet.part * 12)
                || packet.poses.Any(p => p == null
                || !ValidMiniGamePose(p))) return false;
            if (slot.state != null && (packet.session < slot.state.session
                || (packet.session == slot.state.session && packet.sequence <= slot.lastStateSequence))) return true;
            if (packet.part == 0)
            {
                if (slot.pendingPart != 0) return false;
                slot.pending = packet; slot.pendingPoses.Clear();
            }
            if (slot.pending == null || packet.part != slot.pendingPart || packet.session != slot.pending.session
                || packet.sequence != slot.pending.sequence || packet.player != slot.pending.player
                || packet.phase != slot.pending.phase || packet.score != slot.pending.score || packet.stars != slot.pending.stars
                || packet.remaining != slot.pending.remaining || packet.clear != slot.pending.clear) return false;
            slot.pendingPoses.AddRange(packet.poses);
            if (++slot.pendingPart < packet.parts) return true;
            packet = slot.pending;
            packet.poses = slot.pendingPoses.ToArray();
            slot.pendingPart = 0; slot.pending = null;
            slot.lastStateSequence = packet.sequence;
            slot.state = packet;
            for (int i = 0; i < slot.transforms.Length; i++)
            {
                var t = slot.transforms[i]; var pose = packet.poses[i];
                t.localPosition = pose.position; t.localRotation = pose.rotation; t.localScale = pose.scale;
                t.gameObject.SetActive(pose.active);
            }
            ApplyMiniGameResult(slot);
            PresentMiniGame(slot);
            MiniGameStateChanged?.Invoke(packet);
            return true;
        }

        static bool ValidMiniGamePose(MiniGamePose pose)
        {
            bool Vector(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z) && p.sqrMagnitude < 1e12f;
            var q = pose.rotation;
            float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return Vector(pose.position) && Vector(pose.scale) && Finite(length) && length >= .9f && length <= 1.1f;
        }

        void ApplyMiniGameResult(MiniGameSlot slot)
        {
            var state = slot.state;
            if (state.phase != MiniGamePhase.Result || !avatars.TryGetValue(state.player, out var avatar)) return;
            // Host stores all participants; Client only mirrors its own Host-confirmed result.
            if (!hosting && state.player != localId) return;
            if (appliedMiniGameResults.TryGetValue(slot.game, out int previous) && previous >= state.session) return;
            var reference = avatar.GetComponent<PlayerReferenceController>();
            var source = reference != null ? reference.PlayerScore : avatar.GetComponentInChildren<PlayerScore>(true);
            if (source == null) return;
            source.SetMiniGameScore(slot.game, state.stars);
            appliedMiniGameResults[slot.game] = state.session;
        }

        void PresentMiniGame(MiniGameSlot slot)
        {
            var state = slot.state;
            bool visible = state != null && state.player == localId && state.phase != MiniGamePhase.Closed;
            // Do not hide another participant's currently visible camera.
            slot.system.PresentNetworkGame(visible, state != null ? (int)state.phase : 0,
                state != null ? state.remaining : 0f, state != null ? state.score : 0, state != null && state.clear);
            if (visible)
            {
                if (!miniGameViewActive) { miniGamePreviousView = view; miniGameViewActive = true; }
                if (miniGamePreviousView != null) miniGamePreviousView.enabled = false;
            }
            else if (state != null && state.player == localId && miniGameViewActive && !IsMiniGamePlayer(localId))
                RestoreMiniGameView();
        }

        void RestoreMiniGameView()
        {
            if (miniGamePreviousView != null) miniGamePreviousView.enabled = true;
            miniGamePreviousView = null; miniGameViewActive = false;
        }

        void ConfigureMiniGamePhysics(MiniGameSlot slot, bool authority)
        {
            slot.game.ConfigureNetwork(true, authority);
            foreach (var t in slot.transforms)
            {
                foreach (var collider in t.GetComponents<Collider>())
                {
                    if (!miniGameColliders.ContainsKey(collider)) miniGameColliders.Add(collider, collider.enabled);
                    collider.enabled = authority && miniGameColliders[collider];
                }
                foreach (var body in t.GetComponents<Rigidbody>())
                {
                    if (!miniGameBodies.ContainsKey(body)) miniGameBodies.Add(body, body.isKinematic);
                    body.isKinematic = !authority || miniGameBodies[body];
                }
            }
        }

        void CloseMiniGame(MiniGameSlot slot)
        {
            if (slot.activated) { slot.game.Deactivate(); slot.activated = false; }
            slot.game.SetNetworkInput(MiniGameNetworkInput.Neutral);
            slot.input = MiniGameNetworkInput.Neutral;
            slot.state.phase = MiniGamePhase.Closed;
            slot.deadline = Time.time;
            PublishMiniGame(slot);
        }

        void RemoveMiniGamePlayer(int player)
        {
            foreach (var slot in miniGames.Where(s => s.state != null && s.state.player == player
                && s.state.phase != MiniGamePhase.Closed)) CloseMiniGame(slot);
            miniGameRequestTimes.Remove(player);
        }

        void ResetNetworkMiniGames()
        {
            foreach (var slot in miniGames)
            {
                if (slot.activated) slot.game.Deactivate();
                slot.activated = false; slot.state = null;
                slot.game.ConfigureNetwork(false, false);
                slot.system.PresentNetworkGame(false, 0, 0f, 0, false);
                slot.lastInputSequence = slot.lastStateSequence = 0;
                slot.pendingPart = 0; slot.pending = null; slot.pendingPoses.Clear();
            }
            foreach (var pair in miniGameColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in miniGameBodies) if (pair.Key != null) pair.Key.isKinematic = pair.Value;
            miniGameColliders.Clear(); miniGameBodies.Clear(); miniGameRequestTimes.Clear(); appliedMiniGameResults.Clear();
            nextMiniGameSession = localMiniGameSequence = 0;
            miniGameInputAt = miniGameRequestAt = keyboardSwing = 0f;
            previousMiniGameButton = false;
            foreach (var pair in miniGameFrozenPlayers)
                if (bodies.TryGetValue(pair.Key, out var body) && body != null) body.isKinematic = pair.Value;
            miniGameFrozenPlayers.Clear();
            foreach (var pair in miniGameMovementStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            miniGameMovementStates.Clear();
            RestoreMiniGameView();
        }

        void DrawNetworkMiniGame()
        {
            var state = LocalMiniGame;
            if (state == null) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            string label = state.phase == MiniGamePhase.Result
                ? (state.clear ? "CLEAR!  " + new string('*', state.stars) : "TIME UP")
                : state.phase == MiniGamePhase.Countdown ? "READY  " + Mathf.CeilToInt(state.remaining)
                : "TIME  " + Mathf.CeilToInt(state.remaining);
            GUI.Box(new Rect(Screen.width / 2f - 150, 16, 300, 60), label, style);
            GUI.Label(new Rect(Screen.width / 2f - 150, 80, 350, 30), "ESC: Cancel | Z: Swing | D/A: Rotate | W/S: Shake");
        }
    }
}
