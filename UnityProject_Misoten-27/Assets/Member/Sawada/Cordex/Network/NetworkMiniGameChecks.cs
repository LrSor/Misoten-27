#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    /// <summary>Exercises production request/state handlers in temporary scenes; never saves assets.</summary>
    public static class NetworkMiniGameChecks
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("MiniGame check failed: " + message); }

        static (NetworkTestSession session, MiniGame game, MiniGameZone zone, PlayerScore score) Create(Scene scene, bool host)
        {
            GameObject Object(string name)
            {
                var obj = new GameObject(name);
                SceneManager.MoveGameObjectToScene(obj, scene);
                return obj;
            }
            var owner = Object("Session"); owner.SetActive(false);
            var session = owner.AddComponent<NetworkTestSession>();
            var gameObject = Object("Game");
            var game = gameObject.AddComponent<MiniGame>();
            var piece = Object("Piece"); piece.transform.SetParent(gameObject.transform);
            var logic = piece.AddComponent<MiniGameObject>();
            for (int i = 0; i < 13; i++) Object("Visual " + i).transform.SetParent(piece.transform);
            Set(game, "m_objects", new[] { logic });
            var system = gameObject.AddComponent<MiniGameSystem>();
            Set(system, "m_minigame", game);
            Set(system, "m_countNum", 3f);
            var zone = Object("Zone").AddComponent<MiniGameZone>(); Set(zone, "m_system", system);
            var avatar = Object("Player"); var body = avatar.AddComponent<Rigidbody>();
            body.useGravity = false;
            var score = avatar.AddComponent<PlayerScore>();
            Set(session, "hosting", host); Set(session, "localId", 1);
            ((Dictionary<int, Rigidbody>)Get(session, "bodies")).Add(1, body);
            ((Dictionary<int, GameObject>)Get(session, "avatars")).Add(1, avatar);
            var states = (Dictionary<int, NetworkTestSession.PlayerState>)Get(session, "states");
            states.Add(1, new NetworkTestSession.PlayerState { id = 1 });
            states.Add(2, new NetworkTestSession.PlayerState { id = 2 });
            Call(session, "InitializeMiniGames");
            return (session, game, zone, score);
        }

        [MenuItem("Sawada/Cordex/Check MiniGame Networking (in memory)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var hostScene = EditorSceneManager.NewPreviewScene();
            var clientScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var host = Create(hostScene, true); var client = Create(clientScene, false);
                var frame = MiniGameNetworkInput.Neutral;
                Check(frame.IsValid(), "neutral input");
                frame.acceleration.x = float.NaN;
                Check(!frame.IsValid(), "NaN rejected");
                frame = MiniGameNetworkInput.Neutral; frame.gyro.z = 65f;
                Check(!frame.IsValid(), "sensor limit");
                frame = MiniGameNetworkInput.Neutral; frame.orientation = default;
                Check(!frame.IsValid(), "invalid quaternion");
                Check((string)Get(host.session, "miniGameCatalog") == (string)Get(client.session, "miniGameCatalog"), "stable catalog");
                bool Request(int player, NetworkTestSession.MiniGamePacket packet, float time)
                    => (bool)Call(host.session, "HandleMiniGameRequest", player, packet, time);
                Check(Request(1, new NetworkTestSession.MiniGamePacket { operation = "start", game = 1 }, 1f), "entry request");
                var state = host.session.LocalMiniGame;
                Check(state != null && state.phase == NetworkTestSession.MiniGamePhase.Countdown, "Host accepted entry");
                int sessionId = state.session;
                Request(2, new NetworkTestSession.MiniGamePacket { operation = "cancel", game = 1, session = sessionId }, 1.1f);
                Check(host.session.LocalMiniGame != null, "non-owner cannot cancel");
                var input = MiniGameNetworkInput.Neutral; input.acceleration.y = -1f;
                Request(1, new NetworkTestSession.MiniGamePacket { operation = "input", game = 1,
                    session = sessionId, sequence = 1, input = input }, 1.1f);
                Check(!host.game.IsActive, "no simulation during countdown");
                var slots = (System.Collections.IList)Get(host.session, "miniGames");
                Set(slots[0], "deadline", Time.time - 1f);
                Call(host.session, "UpdateNetworkMiniGames", 1.2f);
                Check(host.game.IsActive && host.game.IsNetworkAuthority, "Host starts simulation");
                Check(host.game.NetworkInput.acceleration.y == -1f, "owner input delivered");
                input.acceleration.y = -2f;
                Request(1, new NetworkTestSession.MiniGamePacket { operation = "input", game = 1,
                    session = sessionId - 1, sequence = 2, input = input }, 1.3f);
                Call(host.session, "UpdateNetworkMiniGames", 1.3f);
                Check(host.game.NetworkInput.acceleration.y == -1f, "old session ignored");
                Call(host.session, "UpdateNetworkMiniGames", 1.5f);
                Check(host.game.NetworkInput.acceleration == Vector3.zero, "stale sensor neutralized");
                host.game.SetScore(170); host.game.FinishMiniGame();
                Call(host.session, "UpdateNetworkMiniGames", 1.6f);
                Check(state.phase == NetworkTestSession.MiniGamePhase.Result && state.stars == 3, "result and rating");
                Check(!host.game.IsActive && host.score.GetTotalScore() == 3, "Host saves stars once");
                var copy = JsonUtility.FromJson<NetworkTestSession.MiniGamePacket>(JsonUtility.ToJson(state));
                bool Receive(NetworkTestSession.MiniGamePacket full)
                {
                    for (int part = 0; part < full.parts; part++)
                    {
                        var fragment = JsonUtility.FromJson<NetworkTestSession.MiniGamePacket>(JsonUtility.ToJson(full));
                        fragment.part = part; fragment.poses = full.poses.Skip(part * 12).Take(12).ToArray();
                        Check(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(new NetworkTestSession.Message
                            { kind = "minigame", miniGame = fragment })) < 8192, "transport frame limit");
                        if (!(bool)Call(client.session, "ReceiveMiniGameState", fragment)) return false;
                    }
                    return true;
                }
                Check(Receive(copy), "Client reassembles Host result");
                Check(client.score.GetTotalScore() == 3 && !client.game.IsActive, "Client mirrors result without simulation");
                Check(Receive(copy) && client.score.GetTotalScore() == 3, "duplicate result ignored");
                copy = JsonUtility.FromJson<NetworkTestSession.MiniGamePacket>(JsonUtility.ToJson(state));
                copy.stars = 4;
                Check(!(bool)Call(client.session, "ReceiveMiniGameState", copy), "invalid result rejected");
                Request(1, new NetworkTestSession.MiniGamePacket { operation = "cancel", game = 1, session = sessionId }, 1.7f);
                Check(host.session.LocalMiniGame == null, "cancel releases session");
                Request(1, new NetworkTestSession.MiniGamePacket { operation = "start", game = 1 }, 2f);
                Check(host.session.LocalMiniGame.session > sessionId, "retry gets fresh session");
                Call(host.session, "RemoveMiniGamePlayer", 1);
                Check(host.session.LocalMiniGame == null, "disconnect releases game");
                host.zone.transform.position = Vector3.one * 100f;
                Request(1, new NetworkTestSession.MiniGamePacket { operation = "start", game = 1 }, 3f);
                Check(host.session.LocalMiniGame == null, "out-of-range entry denied");
                Debug.Log("NetworkMiniGameChecks: PASS (ownership, sessions, sensor validation, countdown, stale input, rating, score mirror, duplicates, chunking, frame limit, cancel, disconnect, proximity)");
            }
            finally
            {
                foreach (var scene in new[] { hostScene, clientScene })
                {
                    foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }
    }
}
#endif
