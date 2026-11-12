#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    // Exercise real JSON dispatch in memory without saving scenes or editing prefab assets.
    public static class NetworkScoreRankingChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        sealed class Peer : INetworkTestPeer
        {
            public readonly Queue<string> incoming = new Queue<string>();
            public readonly List<string> sent = new List<string>();
            public string Error => "";
            public bool Closed { get; private set; }
            public bool TryRead(out string message)
            {
                message = incoming.Count > 0 ? incoming.Dequeue() : null;
                return message != null;
            }
            public void Send(string message) => sent.Add(message);
            public void Dispose() => Closed = true;
        }

        static void Check(bool value, string detail)
        { if (!value) throw new Exception("Score ranking check failed: " + detail); }
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Call(NetworkTestSession session, string name, params object[] args)
            => typeof(NetworkTestSession).GetMethod(name, Private).Invoke(session, args);

        static NetworkTestSession Create(Scene scene, int id, bool host, int score)
        {
            var owner = new GameObject("Ranking session " + id);
            owner.SetActive(false); // Do not execute the runtime camera/world setup.
            SceneManager.MoveGameObjectToScene(owner, scene);
            var session = owner.AddComponent<NetworkTestSession>();
            Set(session, "localId", id); Set(session, "hosting", host);
            var avatar = new GameObject("Owned avatar " + id);
            avatar.transform.SetParent(owner.transform);
            var source = avatar.AddComponent<PlayerScore>();
            Set(source, "m_totalScore", score);
            Check(source.GetTotalScore() == score, "PlayerScore getter");
            ((Dictionary<int, GameObject>)Get(session, "avatars")).Add(id, avatar);
            return session;
        }

        static void AddConnection(NetworkTestSession host, int id, Peer peer)
        {
            var type = typeof(NetworkTestSession).GetNestedType("Connection", BindingFlags.NonPublic);
            var connection = Activator.CreateInstance(type, true);
            type.GetField("id").SetValue(connection, id);
            type.GetField("peer").SetValue(connection, peer);
            ((IList)Get(host, "connections")).Add(connection);
        }

        static NetworkTestSession.Message Read(Peer peer, string kind) => peer.sent
            .Select(JsonUtility.FromJson<NetworkTestSession.Message>).Last(m => m.kind == kind);
        static void Deliver(Peer peer, NetworkTestSession.Message message) => peer.incoming.Enqueue(JsonUtility.ToJson(message));

        [MenuItem("Sawada/Cordex/Check Score Ranking (in memory)")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            float scale = Time.timeScale, step = Time.fixedDeltaTime;
            const string timeSettingsPath = "ProjectSettings/TimeManager.asset";
            byte[] timeSettingsBefore = File.ReadAllBytes(timeSettingsPath);
            var timeSettings = AssetDatabase.LoadAllAssetsAtPath(timeSettingsPath).FirstOrDefault();
            try
            {
                var rules = new ScoreRankingCoordinator();
                Check(rules.Begin(new[] { 1, 2, 3, 4 }, 10f), "begin round");
                Check(!rules.Begin(new[] { 1 }, 10f), "simultaneous request ignored");
                rules.Submit(0, 2, 999); rules.Submit(1, 9, 999); rules.Submit(1, 4, -1);
                rules.Submit(1, 1, 100); rules.Submit(1, 2, 200); rules.Submit(1, 2, 999);
                rules.Submit(1, 3, 200); rules.Submit(1, 4, 0);
                Check(!rules.TryFinish(10.5f, out _), "minimum visible slow duration");
                Check(rules.TryFinish(11f, out var tied), "all answers finish");
                Check(tied.Select(e => e.id).SequenceEqual(new[] { 2, 3, 1, 4 })
                    && tied.Select(e => e.rank).SequenceEqual(new[] { 1, 1, 3, 4 }), "descending order, ties, zero score, duplicate ignored");
                rules.Begin(new[] { 1, 2, 3 }, 20f);
                rules.Submit(1, 1, 999); // Previous round cannot submit.
                rules.Submit(2, 2, 100); rules.RemoveDisconnected(2);
                rules.Submit(2, 3, 0);
                Check(!rules.TryFinish(22.9f, out _), "wait for missing participants");
                Check(rules.TryFinish(23f, out var missing) && missing[0].id == 3 && missing[0].rank == 1
                    && missing.Skip(1).All(e => !e.submitted && e.rank == 0), "deadline, missing vs zero, disconnect, stale round");

                // Each simulated endpoint receives its own process time settings.
                Time.timeScale = .8f; Time.fixedDeltaTime = .016f;
                var host = Create(scene, 1, true, 100);
                var client = Create(scene, 2, false, 200);
                var third = Create(scene, 3, false, 200);
                var hostToClient = new Peer(); var clientToHost = new Peer();
                var hostToThird = new Peer(); var thirdToHost = new Peer();
                AddConnection(host, 2, hostToClient); Set(client, "server", clientToHost);
                AddConnection(host, 3, hostToThird); Set(third, "server", thirdToHost);
                client.RequestRanking();
                Call(host, "HandleRankingMessage", 2, Read(clientToHost, "ranking-request"), 30f);
                Check(Mathf.Approximately(Time.timeScale, .16f) && Mathf.Approximately(Time.fixedDeltaTime, .0032f), "Host slow scale and physics");
                Time.timeScale = .8f; Time.fixedDeltaTime = .016f;
                Deliver(clientToHost, Read(hostToClient, "ranking-begin"));
                Call(client, "PollClient", 30f); Call(client, "UpdateRanking", 30f);
                var answer = Read(clientToHost, "ranking-score");
                Check(answer.scoreAvailable && answer.score == 200 && answer.rankingId == 1, "Client captures and sends own score");
                answer.id = 1; // A forged ID must not replace the Host's score.
                Call(host, "HandleRankingMessage", 2, answer, 30.1f);
                Time.timeScale = .8f; Time.fixedDeltaTime = .016f;
                Deliver(thirdToHost, Read(hostToThird, "ranking-begin"));
                Call(third, "PollClient", 30f); Call(third, "UpdateRanking", 30f);
                Call(host, "HandleRankingMessage", 3, Read(thirdToHost, "ranking-score"), 30.1f);
                Call(host, "UpdateRanking", 31f);
                Check(host.LastRanking.Count == 3 && host.LastRanking[0].id == 2
                    && host.LastRanking[1].id == 3 && host.LastRanking[1].rank == 1
                    && host.LastRanking[2].score == 100 && host.LastRanking[2].rank == 3,
                    "Host ranks own and two Client scores, binds sender identity");
                int events = 0; client.RankingCompleted += () => events++;
                var result = Read(hostToClient, "ranking-result");
                Deliver(clientToHost, result); Deliver(clientToHost, result);
                Call(client, "PollClient", 31f);
                Deliver(thirdToHost, Read(hostToThird, "ranking-result")); Call(third, "PollClient", 31f);
                Check(JsonUtility.ToJson(new NetworkTestSession.Message { ranking = host.LastRanking.ToArray() })
                    == JsonUtility.ToJson(new NetworkTestSession.Message { ranking = client.LastRanking.ToArray() })
                    && third.LastRanking.Count == 3 && third.LastRanking[2].rank == 3
                    && events == 1, "identical result at all endpoints, one completion event");
                Check(Mathf.Approximately(Time.timeScale, .8f) && Mathf.Approximately(Time.fixedDeltaTime, .016f), "restore original time settings");

                Deliver(clientToHost, new NetworkTestSession.Message { kind = "ranking-begin", rankingId = 2 });
                Call(client, "PollClient", 40f);
                Call(client, "UpdateRanking", 45.1f);
                Check(Mathf.Approximately(Time.timeScale, .8f), "Client restores time on result timeout");
                Deliver(clientToHost, new NetworkTestSession.Message { kind = "ranking-begin", rankingId = 3 });
                Call(client, "PollClient", 46f);
                // Runtime teardown uses delayed Destroy; leave edit-mode objects for finally.
                ((Dictionary<int, GameObject>)Get(client, "avatars")).Clear();
                client.StopSession("Ranking check disconnect");
                Check(Mathf.Approximately(Time.timeScale, .8f) && client.LastRanking.Count == 0, "disconnect restores time and clears result");
                ((Dictionary<int, GameObject>)Get(host, "avatars")).Clear();
                host.StopSession("Ranking check finished");
                ((Dictionary<int, GameObject>)Get(third, "avatars")).Clear(); third.StopSession("Ranking check finished");
                foreach (string path in new[] { "Assets/Member/Sawada/Scenes/testMapPlayerMove.unity",
                    "Assets/Member/Sawada/Scenes/CordexScene.unity" })
                {
                    var map = EditorSceneManager.OpenPreviewScene(path);
                    try
                    {
                        Check(map.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                            .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts: " + path);
                        var session = map.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<NetworkTestSession>(true)).FirstOrDefault();
                        Check(session != null, "target scene network session: " + path);
                        var prefab = new SerializedObject(session).FindProperty("playerPrefab").objectReferenceValue as GameObject;
                        Check(prefab != null && prefab.GetComponentInChildren<PlayerScore>(true) != null, "referenced network prefab has score source: " + path);
                        Check(prefab.GetComponentsInChildren<Transform>(true)
                            .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts on referenced prefab: " + path);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(map); }
                }
                Debug.Log("[NetworkScoreRankingChecks] PASS: getter, ties, duplicate/stale/invalid submissions, missing/disconnect, JSON request/begin/score/result, sender ownership, equal results, time restore, timeout, prefab score source");
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                Time.timeScale = scale; Time.fixedDeltaTime = step;
                // Edit-mode setters dirty TimeManager; do not persist a serialization upgrade.
                if (timeSettings != null) EditorUtility.ClearDirty(timeSettings);
                if (!File.ReadAllBytes(timeSettingsPath).SequenceEqual(timeSettingsBefore))
                    File.WriteAllBytes(timeSettingsPath, timeSettingsBefore);
            }
        }
    }
}
#endif
