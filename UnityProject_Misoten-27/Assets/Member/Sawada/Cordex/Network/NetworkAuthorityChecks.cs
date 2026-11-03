#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    // In-memory checks; never saves or modifies an existing scene/prefab.
    public static class NetworkAuthorityChecks
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Authority check failed: " + message); }
        static void Set(NetworkTestSession session, string name, object value) => typeof(NetworkTestSession)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);

        static (NetworkTestSession session, NetworkWorld world, NetworkDoor door) Create(Scene scene)
        {
            var owner = new GameObject("Session"); owner.SetActive(false);
            SceneManager.MoveGameObjectToScene(owner, scene);
            var session = owner.AddComponent<NetworkTestSession>();
            var staging = new GameObject("Staging"); staging.transform.SetParent(owner.transform);
            var doorObject = new GameObject("Door"); SceneManager.MoveGameObjectToScene(doorObject, scene);
            var door = doorObject.AddComponent<NetworkDoor>(); doorObject.AddComponent<BoxCollider>();
            var spawnPoint = new GameObject("SpawnPoint"); SceneManager.MoveGameObjectToScene(spawnPoint, scene);
            spawnPoint.AddComponent<NetworkSpawnPoint>();
            var hidden = new GameObject("HiddenNPC"); SceneManager.MoveGameObjectToScene(hidden, scene);
            hidden.SetActive(false); hidden.AddComponent<NPC_FADER>(); hidden.AddComponent<NetworkWorldObject>();
            for (int i = 0; i < 24; i++)
            {
                var prop = new GameObject("Prop " + i); SceneManager.MoveGameObjectToScene(prop, scene);
                prop.AddComponent<NetworkWorldObject>();
            }
            var template = new GameObject("SpawnTemplate"); template.transform.SetParent(staging.transform);
            template.AddComponent<Rigidbody>();
            var world = new NetworkWorld(session, new[] { template }, staging.transform);
            Set(session, "world", world);
            return (session, world, door);
        }

        [MenuItem("Sawada/Cordex/Check Host Authority (in memory)")]
        public static void Run()
        {
            Scene previous = SceneManager.GetActiveScene();
            var hostScene = EditorSceneManager.NewPreviewScene();
            var clientScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var host = Create(hostScene); var client = Create(clientScene);
                Check(host.world.Catalog == client.world.Catalog, "matching catalog");
                host.world.SetHost(true);
                Check(host.door.GetComponent<Collider>().enabled, "Host owns collision");
                Check(!client.door.GetComponent<Collider>().enabled, "Client collision disabled");
                var player = new GameObject("Player"); SceneManager.MoveGameObjectToScene(player, hostScene);
                var body = player.AddComponent<Rigidbody>(); body.isKinematic = true;
                var bodies = (Dictionary<int, Rigidbody>)typeof(NetworkTestSession).GetField("bodies",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host.session);
                bodies.Add(1, body); Set(host.session, "localId", 1); Set(host.session, "hosting", true);
                bool accepted = false;
                host.session.WorldActionCompleted += (id, ok, reason) => accepted = ok;
                Check(host.session.RequestWorldAction(host.door.NetworkId, "interact") && accepted, "near interaction accepted");
                var spawnPoint = hostScene.GetRootGameObjects().Select(o => o.GetComponent<NetworkSpawnPoint>()).First(o => o != null);
                Set(host.session, "localWorldActionAt", 0f);
                Check(host.session.RequestWorldAction(spawnPoint.NetworkId, "interact") && accepted, "Client-style spawn request accepted");
                Set(host.session, "localWorldActionAt", 0f);
                Check(host.session.RequestWorldAction(spawnPoint.NetworkId, "interact") && !accepted, "spawn cooldown enforced");
                body.position = Vector3.one * 100; Set(host.session, "localWorldActionAt", 0f);
                Check(host.session.RequestWorldAction(host.door.NetworkId, "interact") && !accepted, "far interaction rejected");
                Check(client.world.ServerSpawn(0, Vector3.zero, Quaternion.identity) == null, "Client cannot spawn");
                var spawned = host.world.ServerSpawn(0, new Vector3(5, 0, 0), Quaternion.identity);
                Check(spawned != null, "Host spawn");
                var frame = host.world.Capture().ToArray();
                int hiddenId = hostScene.GetRootGameObjects().First(o => o.name == "HiddenNPC").GetComponent<NetworkWorldObject>().NetworkId;
                foreach (var state in frame.SelectMany(m => m.world)) if (state.id == hiddenId) state.alpha = 0;
                Check(frame.Length >= 3, "snapshot split into bounded parts");
                foreach (var message in frame)
                {
                    Check(System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(message)) < 8192, "frame size");
                    Check(client.world.Receive(message), "Client accepts complete world");
                }
                Check(client.door.Capture().value == 1, "door state replicated");
                Check(host.world.ServerDespawn(spawned), "Host despawn");
                Check(host.world.ServerDespawn(host.door), "scene despawn");
                foreach (var message in host.world.Capture()) Check(client.world.Receive(message), "deletion snapshot");
                Check(!client.door.gameObject.activeSelf, "scene deletion reflected");
                Check(!client.world.Receive(frame[0]), "old frame rejected");
                host.world.Reset(); client.world.Reset();
                Check(host.door.gameObject.activeSelf && client.door.gameObject.activeSelf, "stop restores scene objects");
                host.world.SetHost(true);
                foreach (var message in host.world.Capture()) Check(client.world.Receive(message), "reconnect baseline");
                // Legacy private serialized fields must retain Unity object references.
                var legacy = new GameObject("Spawner"); SceneManager.MoveGameObjectToScene(legacy, hostScene);
                var source = legacy.AddComponent<NPC_SPAWNER>();
                var sourceData = new SerializedObject(source);
                var route = sourceData.FindProperty("m_routes"); route.arraySize = 1;
                route.GetArrayElementAtIndex(0).FindPropertyRelative("m_spawnPoint").objectReferenceValue = host.door.transform;
                route.GetArrayElementAtIndex(0).FindPropertyRelative("m_destination").objectReferenceValue = player.transform;
                sourceData.ApplyModifiedPropertiesWithoutUndo();
                var adapter = new NetworkNpcSpawner(source);
                Check(adapter.Config.m_routes[0].m_spawnPoint == host.door.transform, "legacy spawn-point reference");
                Check(adapter.Config.m_routes[0].m_destination == player.transform, "legacy destination reference");
                // Invalid floating-point values must never reach a proxy transform.
                var invalid = host.world.Capture().First(); invalid.world[0].position.x = float.NaN;
                Check(!client.world.Receive(invalid), "non-finite state rejected");
                Debug.Log("[NetworkAuthorityChecks] PASS: requests, distance rejection, collision ownership, spawn/despawn, multipart sync, door, reconnect, legacy references");
            }
            finally
            {
                // Closing in-memory scenes bypasses runtime OnDestroy/session teardown.
                foreach (var scene in new[] { hostScene, clientScene })
                {
                    foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
#endif
