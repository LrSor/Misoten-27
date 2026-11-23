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
            public Vector3 m_position, m_scale;
            public Quaternion m_rotation;
            public bool m_active;
        }

        [Serializable] public sealed class MiniGamePacket
        {
            public string m_operation;
            public int m_game, m_session, m_player, m_sequence, m_score, m_stars, m_part, m_parts;
            public MiniGamePhase m_phase;
            public float m_remaining, m_duration;
            public bool m_clear, m_completed, m_hasTimer;
            public MiniGameNetworkInput m_input;
            public MiniGamePose[] m_poses;
        }

        sealed class MiniGameSlot
        {
            public MiniGameSystem m_system;
            public MiniGame m_game;
            public MiniGameZone[] m_zones;
            public Transform[] m_transforms;
            public NetworkMiniGameSettings m_settings;
            public MiniGamePacket m_state;
            public int m_lastInputSequence, m_lastStateSequence;
            public float m_deadline, m_inputAt, m_sendAt;
            public MiniGameNetworkInput m_input = MiniGameNetworkInput.Neutral;
            public bool m_activated;
            public MiniGamePacket m_pending;
            public readonly List<MiniGamePose> m_pendingPoses = new List<MiniGamePose>();
            public int m_pendingPart;
        }

        static readonly List<NetworkTestSession> m_miniGameSessions = new List<NetworkTestSession>();
        readonly List<MiniGameSlot> m_miniGames = new List<MiniGameSlot>();
        readonly Dictionary<Collider, bool> m_miniGameColliders = new Dictionary<Collider, bool>();
        readonly Dictionary<Rigidbody, bool> m_miniGameBodies = new Dictionary<Rigidbody, bool>();
        readonly Dictionary<int, float> m_miniGameRequestTimes = new Dictionary<int, float>();
        readonly Dictionary<MiniGame, int> m_appliedMiniGameResults = new Dictionary<MiniGame, int>();
        readonly Dictionary<int, bool> m_miniGameFrozenPlayers = new Dictionary<int, bool>();
        readonly Dictionary<StateMachine, bool> m_miniGameMovementStates = new Dictionary<StateMachine, bool>();
        readonly Dictionary<UnityEngine.UI.Text, bool> m_miniGameLegacyTimers = new Dictionary<UnityEngine.UI.Text, bool>();
        string m_miniGameCatalog = "";
        int m_nextMiniGameSession, m_localMiniGameSequence;
        float m_miniGameInputAt, m_miniGameRequestAt, m_keyboardSwing;
        bool m_previousMiniGameButton, m_miniGameViewActive;
        Camera m_miniGamePreviousView;
        NetworkMiniGameUIBridge m_miniGameUI;
        bool UsesMiniGameUI => m_miniGameUI != null && m_miniGameUI.IsReady;
        public int LocalMiniGamePlayerId => localId;
        public PlayerScore LocalMiniGamePlayerScore
        {
            get
            {
                if (localId <= 0 || !avatars.TryGetValue(localId, out var avatar)) return null;
                var reference = avatar.GetComponent<PlayerReferenceController>();
                return reference != null ? reference.PlayerScore : avatar.GetComponentInChildren<PlayerScore>(true);
            }
        }
        public event Action<MiniGamePacket> MiniGameStateChanged;
        public MiniGamePacket LocalMiniGame => m_miniGames.Select(s => s.m_state)
            .FirstOrDefault(s => s != null && s.m_player == localId && s.m_phase != MiniGamePhase.Closed);
        string SessionCatalog => world.Catalog + ":" + m_miniGameCatalog;

        public int GetMiniGameId(MiniGameSystem system) => m_miniGames.FindIndex(s => s.m_system == system) + 1;

        public static bool HasMiniGameNetworking(Scene scene)
            => m_miniGameSessions.Any(s => s != null && s.isActiveAndEnabled && s.gameObject.scene == scene);

        string MiniGameHierarchy(Transform item) => item.parent == null
            ? item.GetSiblingIndex() + ":" + item.name : MiniGameHierarchy(item.parent) + "/" + item.GetSiblingIndex() + ":" + item.name;

        void InitializeMiniGames()
        {
            m_miniGameSessions.RemoveAll(s => s == null);
            m_miniGameSessions.Add(this);
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
                m_miniGames.Add(new MiniGameSlot { m_system = system, m_game = system.Game,
                    m_zones = zones.Where(z => z.System == system).ToArray(), m_transforms = transforms,
                    m_settings = system.GetComponent<NetworkMiniGameSettings>() });
            }
            string signature = string.Join("\n", m_miniGames.Select(s => MiniGameHierarchy(s.m_system.transform)
                + ":" + s.m_game.GetType().FullName + ":" + string.Join(",", s.m_transforms.Select(t => MiniGameHierarchy(t)))
                + ":" + string.Join(",", s.m_zones.Select(z => MiniGameHierarchy(z.transform)))));
            using (var hash = SHA256.Create()) m_miniGameCatalog = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(signature)));
            m_miniGameUI = GetComponent<NetworkMiniGameUIBridge>();
            if (m_miniGameUI == null) m_miniGameUI = gameObject.AddComponent<NetworkMiniGameUIBridge>();
            m_miniGameUI.Configure(this);
        }

        bool IsMiniGamePlayer(int player) => player > 0 && m_miniGames.Any(s => s.m_state != null
            && s.m_state.m_player == player && s.m_state.m_phase != MiniGamePhase.Closed);

        bool NearMiniGame(MiniGameSlot slot, int player)
        {
            if (!TryGetPlayerPosition(player, out var position)) return false;
            return slot.m_zones.Any(z => z != null && z.isActiveAndEnabled
                && Vector3.Distance(position, z.transform.position) <= 3f);
        }

        /// <summary>Queue an entry request; acceptance arrives through MiniGameStateChanged.</summary>
        public bool RequestMiniGame(int gameId)
        {
            if (localId <= 0 || gameId < 1 || gameId > m_miniGames.Count || IsMiniGamePlayer(localId)
                || Time.realtimeSinceStartup < m_miniGameRequestAt) return false;
            m_miniGameRequestAt = Time.realtimeSinceStartup + .3f;
            var request = new MiniGamePacket { m_operation = "start", m_game = gameId };
            if (hosting) return HandleMiniGameRequest(localId, request, Time.realtimeSinceStartup);
            if (server == null) return false;
            Send(server, new Message { kind = "minigame", m_miniGame = request });
            return true;
        }

        public void CancelNetworkMiniGame()
        {
            var state = LocalMiniGame;
            if (state == null) return;
            var request = new MiniGamePacket { m_operation = "cancel", m_game = state.m_game, m_session = state.m_session };
            if (hosting) HandleMiniGameRequest(localId, request, Time.realtimeSinceStartup);
            else if (server != null) Send(server, new Message { kind = "minigame", m_miniGame = request });
        }

        bool HandleMiniGameRequest(int player, MiniGamePacket packet, float now)
        {
            if (!hosting || packet == null || packet.m_game < 1 || packet.m_game > m_miniGames.Count) return false;
            var slot = m_miniGames[packet.m_game - 1];
            if (packet.m_operation == "start")
            {
                if (m_miniGameRequestTimes.TryGetValue(player, out float previous) && now - previous < .25f) return true;
                m_miniGameRequestTimes[player] = now;
                if (IsMiniGamePlayer(player) || ranking.IsCollecting || !slot.m_system.isActiveAndEnabled
                    || (slot.m_state != null && slot.m_state.m_phase != MiniGamePhase.Closed) || !NearMiniGame(slot, player)) return true;
                slot.m_game.ConfigureNetwork(true, true);
                slot.m_input = MiniGameNetworkInput.Neutral;
                slot.m_game.SetNetworkInput(slot.m_input);
                slot.m_lastInputSequence = 0;
                slot.m_activated = false;
                slot.m_inputAt = now;
                float countdownDuration = Mathf.Max(1f, slot.m_system.CountdownSeconds);
                slot.m_deadline = Time.time + countdownDuration;
                slot.m_state = new MiniGamePacket { m_operation = "state", m_game = packet.m_game,
                    m_session = ++m_nextMiniGameSession, m_player = player, m_phase = MiniGamePhase.Countdown, m_duration = countdownDuration };
                PublishMiniGame(slot);
                return true;
            }
            if (packet.m_operation != "input" && packet.m_operation != "cancel") return false;
            if (slot.m_state == null || slot.m_state.m_session != packet.m_session || slot.m_state.m_player != player
                || slot.m_state.m_phase == MiniGamePhase.Closed) return true; // Old session / non-owner: never execute it.
            if (packet.m_operation == "cancel") { CloseMiniGame(slot); return true; }
            if (!packet.m_input.IsValid() || packet.m_sequence <= 0) return false;
            if (packet.m_sequence <= slot.m_lastInputSequence || now - slot.m_inputAt < .01f) return true;
            slot.m_lastInputSequence = packet.m_sequence;
            // Preserve a button release until the gameplay Update has consumed it.
            bool release = slot.m_input.m_release || (!slot.m_input.m_button && packet.m_input.m_release)
                || (slot.m_input.m_button && !packet.m_input.m_button);
            slot.m_input = packet.m_input;
            slot.m_input.m_release = release;
            slot.m_inputAt = now;
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
                m_keyboardSwing = 0f;
                m_previousMiniGameButton = false;
                if (start && TryGetPlayerPosition(localId, out var position))
                {
                    int nearest = Enumerable.Range(1, m_miniGames.Count).Where(i => NearMiniGame(m_miniGames[i - 1], localId))
                        .OrderBy(i => m_miniGames[i - 1].m_zones.Min(z => (z.transform.position - position).sqrMagnitude)).FirstOrDefault();
                    if (nearest > 0) RequestMiniGame(nearest);
                }
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { CancelNetworkMiniGame(); return; }
            if (now < m_miniGameInputAt) return;
            m_miniGameInputAt = now + .02f;
            var input = MiniGameNetworkInput.Neutral;
            if (device != null)
            {
                input.m_acceleration = device.GetAccel(); input.m_gyro = device.GetGyro();
                input.m_orientation = device.GetVector(); input.m_isLeft = device.isLeft;
                input.m_button = device.GetButton(Joycon.Button.SHOULDER_1);
                if (device.GetButtonDown(Joycon.Button.DPAD_DOWN)) device.Recenter();
            }
            else if (keyboard != null)
            {
                m_keyboardSwing = Mathf.MoveTowards(m_keyboardSwing, keyboard.zKey.isPressed ? -2f : 0f, .024f);
                input.m_acceleration = new Vector3(keyboard.wKey.isPressed ? 2f : keyboard.sKey.isPressed ? -2f : 0f,
                    m_keyboardSwing, keyboard.dKey.isPressed ? 2f : keyboard.aKey.isPressed ? -2f : 0f);
                input.m_gyro = new Vector3(0f, 0f, keyboard.dKey.isPressed ? 4f : keyboard.aKey.isPressed ? -4f : 0f);
                input.m_orientation = Quaternion.Euler((keyboard.upArrowKey.isPressed ? -30f : 0f)
                    + (keyboard.downArrowKey.isPressed ? 30f : 0f), (keyboard.rightArrowKey.isPressed ? 30f : 0f)
                    + (keyboard.leftArrowKey.isPressed ? -30f : 0f), 0f);
                input.m_button = keyboard.spaceKey.isPressed;
            }
            input.m_release = m_previousMiniGameButton && !input.m_button;
            m_previousMiniGameButton = input.m_button;
            // Invalid device samples (e.g. during reconnect) must not disconnect the room.
            if (!input.IsValid()) input = MiniGameNetworkInput.Neutral;
            var packet = new MiniGamePacket { m_operation = "input", m_game = state.m_game, m_session = state.m_session,
                m_sequence = ++m_localMiniGameSequence, m_input = input };
            if (hosting) HandleMiniGameRequest(localId, packet, now);
            else if (server != null) Send(server, new Message { kind = "minigame", m_miniGame = packet });
        }

        void UpdateNetworkMiniGames(float now)
        {
            foreach (var slot in m_miniGames)
            {
                if (localId > 0) ConfigureMiniGamePhysics(slot, hosting);
                var state = slot.m_state;
                if (!hosting || state == null || state.m_phase == MiniGamePhase.Closed) continue;
                if (!states.ContainsKey(state.m_player) || !slot.m_system.isActiveAndEnabled) { CloseMiniGame(slot); continue; }
                if (state.m_phase == MiniGamePhase.Countdown && Time.time >= slot.m_deadline)
                {
                    slot.m_game.Activate();
                    slot.m_activated = true;
                    state.m_phase = MiniGamePhase.Playing;
                    state.m_hasTimer = slot.m_game.NetworkRemainingSeconds >= 0f;
                    state.m_duration = state.m_hasTimer ? slot.m_game.NetworkRemainingSeconds : 0f;
                    slot.m_deadline = 0f; // No network-owned gameplay timer; the game decides when it finishes.
                }
                if (state.m_phase == MiniGamePhase.Playing)
                {
                    // Results are read on the next session Update, after game/physics callbacks have set the score.
                    if (slot.m_game.GetIsFinish()) FinishNetworkMiniGame(slot, slot.m_game.GetIsGameClear());
                    else if (now - slot.m_inputAt > 3f) { CloseMiniGame(slot); continue; }
                    else
                    {
                        slot.m_game.SetNetworkInput(now - slot.m_inputAt <= .25f ? slot.m_input : MiniGameNetworkInput.Neutral);
                        slot.m_input.m_release = false;
                    }
                }
                if (state.m_phase == MiniGamePhase.Result && Time.time >= slot.m_deadline) { CloseMiniGame(slot, true); continue; }
                if (now >= slot.m_sendAt) { slot.m_sendAt = now + .05f; PublishMiniGame(slot); }
            }
            foreach (var pair in bodies)
            {
                bool frozen = hosting && IsMiniGamePlayer(pair.Key);
                if (frozen && !m_miniGameFrozenPlayers.ContainsKey(pair.Key))
                {
                    m_miniGameFrozenPlayers[pair.Key] = pair.Value.isKinematic;
                    pair.Value.linearVelocity = Vector3.zero;
                    pair.Value.angularVelocity = Vector3.zero;
                    pair.Value.isKinematic = true;
                    if (movers.TryGetValue(pair.Key, out var mover))
                    {
                        var machine = mover.GetComponent<StateMachine>();
                        machine.ChangeState<Player_Idle>();
                        m_miniGameMovementStates[machine] = machine.enabled;
                        machine.enabled = false;
                        mover.EndSprintCameraEffect();
                    }
                }
                else if (!frozen && m_miniGameFrozenPlayers.TryGetValue(pair.Key, out bool kinematic))
                {
                    pair.Value.isKinematic = kinematic; m_miniGameFrozenPlayers.Remove(pair.Key);
                    if (movers.TryGetValue(pair.Key, out var mover))
                    {
                        var machine = mover.GetComponent<StateMachine>();
                        if (m_miniGameMovementStates.TryGetValue(machine, out bool wasEnabled)) machine.enabled = wasEnabled;
                        m_miniGameMovementStates.Remove(machine);
                    }
                }
            }
            // Retry when a result arrives before the avatar's first state snapshot.
            foreach (var slot in m_miniGames.Where(s => s.m_state != null && s.m_state.m_phase == MiniGamePhase.Result))
                ApplyMiniGameResult(slot);
        }

        void FinishNetworkMiniGame(MiniGameSlot slot, bool clear)
        {
            var state = slot.m_state;
            state.m_score = Mathf.Max(0, slot.m_game.GetScore());
            state.m_clear = clear;
            int? gameStars = slot.m_game.NetworkStars;
            state.m_stars = gameStars.HasValue ? Mathf.Clamp(gameStars.Value, 0, 3) : 3;
            state.m_phase = MiniGamePhase.Result;
            state.m_duration = slot.m_settings != null ? Mathf.Max(.1f, slot.m_settings.m_resultSeconds) : 2f;
            slot.m_deadline = Time.time + state.m_duration;
            // Capture the final pose before Deactivate resets the axe/counters.
            state.m_poses = CaptureMiniGamePoses(slot);
            slot.m_game.Deactivate(); slot.m_activated = false;
            PublishMiniGame(slot);
        }

        MiniGamePose[] CaptureMiniGamePoses(MiniGameSlot slot) => slot.m_transforms.Select(t => new MiniGamePose
        { m_position = t.localPosition, m_rotation = t.localRotation, m_scale = t.localScale, m_active = t.gameObject.activeSelf }).ToArray();

        void PublishMiniGame(MiniGameSlot slot)
        {
            var state = slot.m_state;
            state.m_sequence++;
            if (state.m_phase == MiniGamePhase.Playing)
            {
                float gameSeconds = slot.m_game.NetworkRemainingSeconds;
                state.m_hasTimer = gameSeconds >= 0f;
                state.m_remaining = state.m_hasTimer ? gameSeconds : 0f;
            }
            else state.m_remaining = Mathf.Max(0f, slot.m_deadline - Time.time);
            if (state.m_phase != MiniGamePhase.Result) state.m_poses = CaptureMiniGamePoses(slot);
            state.m_part = 0;
            state.m_parts = Math.Max(1, (state.m_poses.Length + 11) / 12);
            ApplyMiniGameResult(slot);
            PresentMiniGame(slot);
            MiniGameStateChanged?.Invoke(JsonUtility.FromJson<MiniGamePacket>(JsonUtility.ToJson(state)));
            // TcpPeer/Relay have an 8 KB frame limit. Complete snapshots are reassembled before applying them.
            for (int part = 0; part < state.m_parts; part++)
            {
                var fragment = new MiniGamePacket { m_operation = "state", m_game = state.m_game, m_session = state.m_session,
                    m_player = state.m_player, m_sequence = state.m_sequence, m_score = state.m_score, m_stars = state.m_stars,
                    m_phase = state.m_phase, m_remaining = state.m_remaining, m_duration = state.m_duration, m_clear = state.m_clear,
                    m_completed = state.m_completed, m_hasTimer = state.m_hasTimer, m_part = part, m_parts = state.m_parts,
                    m_poses = state.m_poses.Skip(part * 12).Take(12).ToArray() };
                var message = new Message { kind = "minigame", m_miniGame = fragment };
                foreach (var c in connections) if (c.id > 0 && c.closeAt == 0) Send(c.peer, message);
            }
        }

        bool ReceiveMiniGameState(MiniGamePacket packet)
        {
            if (packet == null || packet.m_operation != "state" || packet.m_game < 1 || packet.m_game > m_miniGames.Count
                || packet.m_player < 1 || packet.m_player > 4 || packet.m_session <= 0 || packet.m_sequence <= 0
                || packet.m_phase < MiniGamePhase.Closed || packet.m_phase > MiniGamePhase.Result
                || (packet.m_completed && packet.m_phase != MiniGamePhase.Closed)
                || !Finite(packet.m_duration) || packet.m_duration < 0f
                || !Finite(packet.m_remaining) || packet.m_remaining < 0f || packet.m_score < 0 || packet.m_stars < 0 || packet.m_stars > 3) return false;
            var slot = m_miniGames[packet.m_game - 1];
            if (packet.m_parts != Math.Max(1, (slot.m_transforms.Length + 11) / 12) || packet.m_part < 0 || packet.m_part >= packet.m_parts
                || packet.m_poses == null || packet.m_poses.Length != Math.Min(12, slot.m_transforms.Length - packet.m_part * 12)
                || packet.m_poses.Any(p => p == null
                || !ValidMiniGamePose(p))) return false;
            if (slot.m_state != null && (packet.m_session < slot.m_state.m_session
                || (packet.m_session == slot.m_state.m_session && packet.m_sequence <= slot.m_lastStateSequence))) return true;
            if (packet.m_part == 0)
            {
                if (slot.m_pendingPart != 0) return false;
                slot.m_pending = packet; slot.m_pendingPoses.Clear();
            }
            if (slot.m_pending == null || packet.m_part != slot.m_pendingPart || packet.m_session != slot.m_pending.m_session
                || packet.m_sequence != slot.m_pending.m_sequence || packet.m_player != slot.m_pending.m_player
                || packet.m_phase != slot.m_pending.m_phase || packet.m_score != slot.m_pending.m_score || packet.m_stars != slot.m_pending.m_stars
                || packet.m_remaining != slot.m_pending.m_remaining || packet.m_clear != slot.m_pending.m_clear
                || packet.m_completed != slot.m_pending.m_completed || packet.m_hasTimer != slot.m_pending.m_hasTimer
                || packet.m_duration != slot.m_pending.m_duration) return false;
            slot.m_pendingPoses.AddRange(packet.m_poses);
            if (++slot.m_pendingPart < packet.m_parts) return true;
            packet = slot.m_pending;
            packet.m_poses = slot.m_pendingPoses.ToArray();
            slot.m_pendingPart = 0; slot.m_pending = null;
            slot.m_lastStateSequence = packet.m_sequence;
            slot.m_state = packet;
            for (int i = 0; i < slot.m_transforms.Length; i++)
            {
                var t = slot.m_transforms[i]; var pose = packet.m_poses[i];
                t.localPosition = pose.m_position; t.localRotation = pose.m_rotation; t.localScale = pose.m_scale;
                t.gameObject.SetActive(pose.m_active);
            }
            ApplyMiniGameResult(slot);
            PresentMiniGame(slot);
            MiniGameStateChanged?.Invoke(packet);
            return true;
        }

        static bool ValidMiniGamePose(MiniGamePose pose)
        {
            bool Vector(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z) && p.sqrMagnitude < 1e12f;
            var q = pose.m_rotation;
            float length = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return Vector(pose.m_position) && Vector(pose.m_scale) && Finite(length) && length >= .9f && length <= 1.1f;
        }

        void ApplyMiniGameResult(MiniGameSlot slot)
        {
            var state = slot.m_state;
            if (state.m_phase != MiniGamePhase.Result || !avatars.TryGetValue(state.m_player, out var avatar)) return;
            // Host stores all participants; Client only mirrors its own Host-confirmed result.
            if (!hosting && state.m_player != localId) return;
            if (m_appliedMiniGameResults.TryGetValue(slot.m_game, out int previous) && previous >= state.m_session) return;
            var reference = avatar.GetComponent<PlayerReferenceController>();
            var source = reference != null ? reference.PlayerScore : avatar.GetComponentInChildren<PlayerScore>(true);
            if (source == null) return;
            source.SetMiniGameScore(slot.m_game, state.m_stars);
            m_appliedMiniGameResults[slot.m_game] = state.m_session;
        }

        void PresentMiniGame(MiniGameSlot slot)
        {
            var state = slot.m_state;
            bool visible = state != null && state.m_player == localId && state.m_phase != MiniGamePhase.Closed;
            // Do not hide another participant's currently visible camera.
            slot.m_system.PresentNetworkGame(visible, state != null && !UsesMiniGameUI ? (int)state.m_phase : 0,
                state != null ? state.m_remaining : 0f, state != null ? state.m_score : 0, state != null && state.m_clear);
            var canvas = slot.m_system.MiniGameCanvas;
            if (canvas != null)
            {
                foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if (visible && UsesMiniGameUI && (label.name == "TimeLimit"
                        || (!string.IsNullOrEmpty(label.text) && label.text.StartsWith("Time Limit:", StringComparison.Ordinal))))
                    {
                        if (!m_miniGameLegacyTimers.ContainsKey(label)) m_miniGameLegacyTimers.Add(label, label.enabled);
                        label.enabled = false;
                    }
                    else if (m_miniGameLegacyTimers.TryGetValue(label, out bool wasEnabled))
                    { label.enabled = wasEnabled; m_miniGameLegacyTimers.Remove(label); }
                }
            }
            if (visible)
            {
                if (!m_miniGameViewActive) { m_miniGamePreviousView = view; m_miniGameViewActive = true; }
                if (m_miniGamePreviousView != null) m_miniGamePreviousView.enabled = false;
            }
            else if (state != null && state.m_player == localId && m_miniGameViewActive && !IsMiniGamePlayer(localId))
                RestoreMiniGameView();
        }

        void RestoreMiniGameView()
        {
            if (m_miniGamePreviousView != null) m_miniGamePreviousView.enabled = true;
            m_miniGamePreviousView = null; m_miniGameViewActive = false;
        }

        void ConfigureMiniGamePhysics(MiniGameSlot slot, bool authority)
        {
            slot.m_game.ConfigureNetwork(true, authority);
            foreach (var t in slot.m_transforms)
            {
                foreach (var collider in t.GetComponents<Collider>())
                {
                    if (!m_miniGameColliders.ContainsKey(collider)) m_miniGameColliders.Add(collider, collider.enabled);
                    collider.enabled = authority && m_miniGameColliders[collider];
                }
                foreach (var body in t.GetComponents<Rigidbody>())
                {
                    if (!m_miniGameBodies.ContainsKey(body)) m_miniGameBodies.Add(body, body.isKinematic);
                    body.isKinematic = !authority || m_miniGameBodies[body];
                }
            }
        }

        void CloseMiniGame(MiniGameSlot slot, bool completed = false)
        {
            if (slot.m_activated) { slot.m_game.Deactivate(); slot.m_activated = false; }
            slot.m_game.SetNetworkInput(MiniGameNetworkInput.Neutral);
            slot.m_input = MiniGameNetworkInput.Neutral;
            slot.m_state.m_duration = 0f;
            slot.m_state.m_completed = completed;
            slot.m_state.m_phase = MiniGamePhase.Closed;
            slot.m_deadline = Time.time;
            PublishMiniGame(slot);
        }

        void RemoveMiniGamePlayer(int player)
        {
            foreach (var slot in m_miniGames.Where(s => s.m_state != null && s.m_state.m_player == player
                && s.m_state.m_phase != MiniGamePhase.Closed)) CloseMiniGame(slot);
            m_miniGameRequestTimes.Remove(player);
        }

        void ResetNetworkMiniGames()
        {
            if (m_miniGameUI != null) m_miniGameUI.ResetPresentation();
            foreach (var pair in m_miniGameLegacyTimers) if (pair.Key != null) pair.Key.enabled = pair.Value;
            m_miniGameLegacyTimers.Clear();
            foreach (var slot in m_miniGames)
            {
                if (slot.m_activated) slot.m_game.Deactivate();
                slot.m_activated = false; slot.m_state = null;
                slot.m_game.ConfigureNetwork(false, false);
                slot.m_system.PresentNetworkGame(false, 0, 0f, 0, false);
                slot.m_lastInputSequence = slot.m_lastStateSequence = 0;
                slot.m_pendingPart = 0; slot.m_pending = null; slot.m_pendingPoses.Clear();
            }
            foreach (var pair in m_miniGameColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in m_miniGameBodies) if (pair.Key != null) pair.Key.isKinematic = pair.Value;
            m_miniGameColliders.Clear(); m_miniGameBodies.Clear(); m_miniGameRequestTimes.Clear(); m_appliedMiniGameResults.Clear();
            m_nextMiniGameSession = m_localMiniGameSequence = 0;
            m_miniGameInputAt = m_miniGameRequestAt = m_keyboardSwing = 0f;
            m_previousMiniGameButton = false;
            foreach (var pair in m_miniGameFrozenPlayers)
                if (bodies.TryGetValue(pair.Key, out var body) && body != null) body.isKinematic = pair.Value;
            m_miniGameFrozenPlayers.Clear();
            foreach (var pair in m_miniGameMovementStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            m_miniGameMovementStates.Clear();
            RestoreMiniGameView();
        }

        void DrawNetworkMiniGame()
        {
            var state = LocalMiniGame;
            if (state == null || UsesMiniGameUI) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            string label = state.m_phase == MiniGamePhase.Result
                ? (state.m_clear ? "CLEAR!  " + new string('*', state.m_stars) : "TIME UP")
                : state.m_phase == MiniGamePhase.Countdown ? "READY  " + Mathf.CeilToInt(state.m_remaining)
                : "TIME  " + (state.m_hasTimer ? Mathf.CeilToInt(state.m_remaining).ToString() : "--");
            GUI.Box(new Rect(Screen.width / 2f - 150, 16, 300, 60), label, style);
            GUI.Label(new Rect(Screen.width / 2f - 150, 80, 350, 30), "ESC: Cancel | Z: Swing | D/A: Rotate | W/S: Shake");
        }
    }
}
