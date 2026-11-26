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
    public static class FieldDataReceiverChecks
    {
        const string ScenePath = "Assets/Member/Sawada/Scenes/SawadaGame.unity";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Flags).SetValue(target, value);
        static void Check(bool value, string message)
        { if (!value) throw new Exception("DataReceiver check failed: " + message); }

        static (FieldDataReceiverManager manager, NetworkWorld world, PlayerSwapController player, Inventory inventory)
            Create(Scene scene, BaseArmorSO[] catalog, int terminals)
        {
            GameObject Object(string name)
            {
                var obj = new GameObject(name);
                SceneManager.MoveGameObjectToScene(obj, scene);
                return obj;
            }
            var root = Object("DataReceivers");
            var manager = root.AddComponent<FieldDataReceiverManager>();
            Set(manager, "dataCatalog", catalog);
            for (int i = 0; i < terminals; i++)
            {
                var terminal = Object("Terminal " + i);
                terminal.transform.SetParent(root.transform);
                terminal.transform.position = new Vector3(i * 6f, 0, 0);
                terminal.AddComponent<FieldDataReceiver>();
            }
            var owner = Object("Session"); owner.SetActive(false);
            var session = owner.AddComponent<NetworkTestSession>();
            var world = new NetworkWorld(session, Array.Empty<GameObject>(), owner.transform);
            Set(session, "world", world);
            var avatar = Object("Player"); avatar.SetActive(false);
            var wardrobe = new GameObject("Wardrobe"); wardrobe.transform.SetParent(avatar.transform);
            var fitting = avatar.AddComponent<CharacterAmorController>();
            Set(fitting, "m_temporaryOutfit", wardrobe.transform);
            var inventory = avatar.AddComponent<Inventory>();
            Set(inventory, "m_armorSlots", new List<BaseArmorSO> { null, null });
            Set(inventory, "m_characterAmorController", fitting);
            var player = avatar.AddComponent<PlayerSwapController>();
            Set(player, "m_inventory", inventory);
            player.SetNetworkSwapInput(false);
            return (manager, world, player, inventory);
        }

        [MenuItem("Sawada/Cordex/Check DataReceiver Distribution (in memory)")]
        public static void Run()
        {
            Check(!EditorApplication.isPlaying, "run in Edit Mode");
            var randomState = UnityEngine.Random.state;
            var catalog = Enumerable.Range(1, 9).Select(i => {
                var data = ScriptableObject.CreateInstance<BaseArmorSO>(); data.name = "Data " + i; return data;
            }).ToArray();
            var scenes = new List<Scene>();
            try
            {
                foreach (int terminalCount in new[] { 10, 16 })
                {
                    var hostScene = EditorSceneManager.NewPreviewScene(); scenes.Add(hostScene);
                    var clientScene = EditorSceneManager.NewPreviewScene(); scenes.Add(clientScene);
                    var host = Create(hostScene, catalog, terminalCount);
                    var client = Create(clientScene, catalog, terminalCount);
                    Check(host.world.Catalog == client.world.Catalog, "matching catalogs");
                    Check(!client.manager.CanAcquire && !client.manager.InitializeField(), "client cannot initialize");
                    host.world.SetHost(true);
                    var seenReplacements = new HashSet<int>();
                    for (int iteration = 0; iteration < 500; iteration++)
                    {
                        var full = host.manager.Receivers.Where(r => r.DataId != 0).ToArray();
                        Check(full.Length == terminalCount - 1, "constant occupied count");
                        Check(full.Select(r => r.DataId).Distinct().Count() == 9, "all nine types remain");
                        var source = full[UnityEngine.Random.Range(0, full.Length)];
                        var oldIds = host.manager.Receivers.Select(r => r.DataId).ToArray();
                        var acquired = source.Data;
                        host.player.transform.position = source.transform.position + Vector3.right * 100;
                        Check(!source.TryAcquire(host.player, host.inventory), "far pickup rejected");
                        host.player.transform.position = source.transform.position;
                        host.player.AddCanAcquireData(source);
                        host.player.SetNetworkSwapInput(true);
                        Check(host.player.Swap(), "real Swap button path accepts pickup");
                        Check(host.inventory.GetCurrentArmor() == acquired, "inventory receives correct data");
                        Check(source.DataId == 0, "source emptied");
                        Check(!source.TryAcquire(host.player, host.inventory), "duplicate pickup rejected");
                        Check(!host.player.Swap(), "one input cannot acquire twice");
                        var destination = host.manager.Receivers.Single(r => r != source && oldIds[r.ReceiverId - 1] == 0);
                        Check(destination.DataId > 0, "different empty terminal filled");
                        seenReplacements.Add(destination.DataId);
                        // Skip the first 250 snapshots: this also exercises a late joining client's baseline.
                        if (iteration >= 250)
                        {
                            foreach (var message in host.world.Capture())
                                Check(client.world.Receive(message), "client accepts full snapshot");
                            Check(client.manager.Receivers.Select(r => r.DataId)
                                .SequenceEqual(host.manager.Receivers.Select(r => r.DataId)), "client placement matches host");
                            Check(!client.manager.Receivers.Any(r => r.CanAcquire), "client cannot acquire locally");
                        }
                    }
                    Check(seenReplacements.Count == 9, "replacement covers catalog");
                    host.world.Reset(); client.world.Reset();
                    Check(host.manager.Receivers.All(r => r.DataId == 0) && !host.manager.CanAcquire, "reset clears data");
                    host.world.SetHost(true);
                    Check(host.manager.Receivers.Count(r => r.DataId > 0) == terminalCount - 1, "restart initializes again");
                    foreach (var message in host.world.Capture()) Check(client.world.Receive(message), "reconnect snapshot");
                }
                Debug.Log("[DataReceiverChecks] PASS: 1000 pickups, nine-type coverage, constant count, distance, duplicate input, inventory, late join, split snapshots, client authority, reset/restart.");
            }
            finally
            {
                foreach (var scene in scenes) EditorSceneManager.ClosePreviewScene(scene);
                foreach (var data in catalog) UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Random.state = randomState;
            }
        }

        [MenuItem("Sawada/Cordex/Check DataReceiver Scene References")]
        public static void ValidateSawadaGame()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var root = scene.GetRootGameObjects().Single(o => o.name == "DataReceivers");
                var manager = root.GetComponent<FieldDataReceiverManager>();
                Check(manager != null, "scene manager reference");
                manager.CollectReceivers();
                Check(manager.Receivers.Count >= 10, "scene terminal count");
                Check(manager.DataCatalog.Count == 9 && manager.DataCatalog.All(d => d != null)
                    && manager.DataCatalog.Distinct().Count() == 9, "scene nine data references");
                foreach (var receiver in manager.Receivers)
                {
                    Check(receiver.GetComponent<SphereCollider>().isTrigger, "scene trigger");
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(receiver.gameObject) == 0,
                        "receiver has no missing scripts");
                }
                var legacy = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "FieldArmors");
                Check(legacy == null || !legacy.activeSelf, "legacy pickups disabled");
                Check(manager.InitializeField(), "scene initial distribution");
                Check(manager.Receivers.Count(r => r.DataId > 0) == manager.Receivers.Count - 1,
                    "scene occupied terminal count");
                Check(manager.Receivers.Where(r => r.DataId > 0).Select(r => r.DataId).Distinct().Count() == 9,
                    "scene initial nine-type coverage");
                var sample = manager.Receivers[0];
                for (int id = 1; id <= 9; id++)
                {
                    sample.SetData(id);
                    Check(sample.transform.childCount == 1, "one data display after replacement");
                    var scale = sample.transform.GetChild(0).lossyScale;
                    Check(Mathf.Abs(scale.x - scale.y) < .001f && Mathf.Abs(scale.y - scale.z) < .001f,
                        "uniform data display scale on stretched terminal");
                }
                sample.SetData(0);
                Check(sample.transform.childCount == 0, "empty terminal clears data display");
                Debug.Log("[DataReceiverChecks] PASS: scene manager, catalog, terminal count, triggers, script references, initial distribution and display replacement.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void RunBatch()
        {
            Run();
            ValidateSawadaGame();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            Physics.SyncTransforms();
            foreach (var receiver in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<FieldDataReceiver>(true)))
            {
                var position = receiver.transform.position;
                var hits = Physics.RaycastAll(position + Vector3.up * 1.5f, Vector3.down, 4f, ~0,
                    QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene == scene
                        && h.collider.GetComponentInParent<FieldDataReceiver>() == null && h.normal.y > .8f).ToArray();
                Check(hits.Any(h => Mathf.Abs(h.point.y - (position.y - 1f)) < .05f),
                    "terminal stands on walkable ground: " + receiver.name);
            }
            Debug.Log("[DataReceiverChecks] PASS: all scene terminals stand on ground.");
        }

        public static void InspectGroundBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InspectGround();
        }

        [MenuItem("Sawada/Cordex/Inspect DataReceiver Ground (read only)")]
        public static void InspectGround()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            Check(scene.isLoaded && !EditorApplication.isPlaying, "SawadaGame must be open in Edit Mode");
            var manager = scene.GetRootGameObjects().Single(o => o.name == "DataReceivers")
                .GetComponent<FieldDataReceiverManager>();
            manager.CollectReceivers();
            Physics.SyncTransforms();
            foreach (var receiver in manager.Receivers)
            {
                var point = receiver.transform.position;
                var hits = Physics.RaycastAll(new Vector3(point.x, 100, point.z), Vector3.down, 200,
                    ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene == scene
                        && h.collider.GetComponentInParent<FieldDataReceiver>() == null && h.normal.y > .8f)
                    .OrderByDescending(h => h.point.y).ToArray();
                Debug.Log("[DataReceiverGround] " + receiver.name + " " + point + ": " +
                    string.Join("; ", hits.Select(h => h.collider.name + " y=" + h.point.y.ToString("F2"))));
            }
            for (int x = -18; x <= 18; x += 6)
                for (int z = -18; z <= 18; z += 6)
                {
                    var hits = Physics.RaycastAll(new Vector3(x, 100, z), Vector3.down, 200,
                        ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.gameObject.scene == scene
                            && h.collider.name.Contains("Floor") && h.normal.y > .8f)
                        .OrderByDescending(h => h.point.y).ToArray();
                    if (hits.Length > 0) Debug.Log("[DataReceiverGroundSample] " + x + "," + z + ": " + hits[0].point.y);
                }
        }
    }
}
#endif
