using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Sawada.Cordex
{
    // Complete, bounded snapshots: deletion and late joining do not depend on event history.
    public sealed class NetworkWorld
    {
        public const int MaxObjects = 512;
        const int PerPart = 12;
        [Serializable] public sealed class State
        {
            public int id, prefab, value;
            public bool active;
            public Vector3 position, scale;
            public Quaternion rotation;
            public float alpha;
        }
        readonly NetworkTestSession session;
        readonly GameObject[] prefabs;
        readonly Dictionary<int, NetworkWorldObject> objects = new Dictionary<int, NetworkWorldObject>();
        readonly Dictionary<int, NetworkWorldObject> sceneObjects = new Dictionary<int, NetworkWorldObject>();
        readonly NetworkNpcSpawner[] spawners;
        readonly Transform staging;
        readonly List<State> pending = new List<State>();
        int nextId, sendTick, pendingTick, pendingPart, pendingParts, receivedTick;
        public bool IsHost { get; private set; }
        public string Catalog { get; }

        internal NetworkWorld(NetworkTestSession owner, GameObject[] templates, Transform inactive)
        {
            session = owner; staging = inactive;
            var scene = owner.gameObject.scene;
            spawners = FindInScene<NPC_SPAWNER>(scene).OrderBy(o => HierarchyKey(o.transform), StringComparer.Ordinal)
                .Select(o => new NetworkNpcSpawner(o)).ToArray();
            var catalog = new List<GameObject>(templates ?? Array.Empty<GameObject>());
            foreach (var prefab in spawners.SelectMany(s => s.Config.m_npcPrefabs ?? Array.Empty<NPC_VANISH>())
                .Where(p => p != null).Select(p => p.gameObject))
                if (!catalog.Contains(prefab)) catalog.Add(prefab);
            prefabs = catalog.ToArray(); // Preserve explicitly configured indices, including empty slots.
            var candidates = FindInScene<NetworkWorldObject>(scene).ToList();
            // Existing scene NPCs and field armor need no prefab asset changes.
            foreach (var item in FindInScene<NPC_BASE>(scene).Select(o => o.gameObject)
                .Concat(FindInScene<FieldArmorMachine>(scene).Select(o => o.gameObject)))
            {
                if (item.GetComponentInParent<NetworkWorldObject>(true) == null)
                    candidates.Add(item.AddComponent<NetworkWorldObject>());
            }
            candidates = candidates.OrderBy(o => HierarchyKey(o.transform), StringComparer.Ordinal).ToList();
            if (candidates.Count > MaxObjects) throw new InvalidOperationException("Too many network world objects (max 512)");
            foreach (var item in candidates)
            {
                if (item.transform.parent != null && item.transform.parent.GetComponentInParent<NetworkWorldObject>(true) != null)
                    throw new InvalidOperationException("Nested NetworkWorldObject is unsupported: " + item.name);
                int id = ++nextId;
                item.Initialize(this, id, -1);
                objects.Add(id, item); sceneObjects.Add(id, item);
                item.SetAuthority(false);
            }
            string signature = scene.path + "\n" + string.Join("\n", candidates.Select(o =>
                HierarchyKey(o.transform) + ":" + o.GetType().FullName)) + "\n" +
                string.Join("\n", prefabs.Select(p => p != null ? p.name : "<null>"));
            using (var hash = SHA256.Create()) Catalog = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(signature)));
        }

        static string HierarchyKey(Transform t) => t.parent == null ? t.GetSiblingIndex() + ":" + t.name
            : HierarchyKey(t.parent) + "/" + t.GetSiblingIndex() + ":" + t.name;
        static IEnumerable<T> FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        internal void SetHost(bool host)
        {
            IsHost = host;
            foreach (var item in objects.Values) if (item != null) item.SetAuthority(host);
        }

        internal void TickHost()
        {
            if (!IsHost) return;
            foreach (var spawner in spawners) spawner.Tick(this, prefabs);
        }

        public NetworkWorldObject ServerSpawn(int prefabId, Vector3 position, Quaternion rotation)
        {
            if (!IsHost || !ValidPrefab(prefabId) || objects.Count >= MaxObjects || !Finite(position) || !Finite(rotation)) return null;
            var item = Create(prefabId, ++nextId, position, rotation);
            item.SetAuthority(true);
            item.gameObject.SetActive(true);
            return item;
        }

        NetworkWorldObject Create(int prefab, int id, Vector3 position, Quaternion rotation)
        {
            var copy = UnityEngine.Object.Instantiate(prefabs[prefab], staging);
            copy.SetActive(false);
            copy.transform.SetPositionAndRotation(position, rotation);
            var item = copy.GetComponent<NetworkWorldObject>() ?? copy.AddComponent<NetworkWorldObject>();
            item.Initialize(this, id, prefab);
            item.SetAuthority(IsHost);
            copy.transform.SetParent(session.transform, true);
            objects.Add(id, item);
            return item;
        }

        public bool ServerDespawn(NetworkWorldObject item)
        {
            if (!IsHost || item == null || item.World != this || item.Removed) return false;
            item.Removed = true;
            item.gameObject.SetActive(false);
            if (item.PrefabId >= 0)
            { objects.Remove(item.NetworkId); DestroyObject(item.gameObject); }
            return true;
        }

        public bool RequestAction(int id, string action) => session.RequestWorldAction(id, action);
        internal bool HandleAction(int player, int id, string action)
        {
            return IsHost && session.TryGetPlayerPosition(player, out var position)
                && objects.TryGetValue(id, out var target) && target != null
                && target.HandleAction(player, position, action);
        }

        internal NetworkWorldObject Nearest(Vector3 position) => objects.Values
            .Where(o => o != null && (o is NetworkDoor || o is NetworkSpawnPoint) && o.gameObject.activeInHierarchy)
            .OrderBy(o => (o.transform.position - position).sqrMagnitude).FirstOrDefault();

        internal IEnumerable<NetworkTestSession.Message> Capture()
        {
            foreach (int id in objects.Where(p => p.Value == null).Select(p => p.Key).ToArray()) objects.Remove(id);
            var values = objects.Values.OrderBy(o => o.NetworkId).Select(o => o.Capture()).ToArray();
            int parts = Math.Max(1, (values.Length + PerPart - 1) / PerPart);
            int tick = ++sendTick;
            for (int part = 0; part < parts; part++)
                yield return new NetworkTestSession.Message { kind = "world", tick = tick, part = part, parts = parts,
                    world = values.Skip(part * PerPart).Take(PerPart).ToArray() };
        }

        internal bool Receive(NetworkTestSession.Message message)
        {
            if (IsHost || message.world == null || message.world.Length > PerPart || message.tick <= receivedTick
                || message.parts < 1 || message.parts > (MaxObjects + PerPart - 1) / PerPart
                || message.part < 0 || message.part >= message.parts) return false;
            if (message.part == 0)
            {
                if (pendingPart != 0) return false;
                pending.Clear(); pendingTick = message.tick; pendingParts = message.parts;
            }
            if (message.tick != pendingTick || message.parts != pendingParts || message.part != pendingPart) return false;
            pending.AddRange(message.world); pendingPart++;
            if (pending.Count > MaxObjects || pending.Any(s => !Valid(s))
                || pending.Select(s => s.id).Distinct().Count() != pending.Count) return false;
            if (pendingPart != pendingParts) return true;
            var ids = new HashSet<int>(pending.Select(s => s.id));
            foreach (var pair in objects.ToArray())
            {
                if (ids.Contains(pair.Key) || pair.Value == null) continue;
                if (pair.Value.PrefabId < 0) { pair.Value.gameObject.SetActive(false); pair.Value.Removed = true; }
                else { DestroyObject(pair.Value.gameObject); objects.Remove(pair.Key); }
            }
            foreach (var state in pending)
            {
                bool first = !objects.TryGetValue(state.id, out var item) || item == null;
                if (first)
                {
                    if (state.prefab < 0) return false;
                    item = Create(state.prefab, state.id, state.position, state.rotation);
                }
                if (item.PrefabId != state.prefab) return false;
                item.Removed = false;
                item.Apply(state, first || receivedTick == 0);
            }
            receivedTick = message.tick; pendingPart = 0; pending.Clear();
            return true;
        }

        bool Valid(State s) => s != null && s.id > 0 && Finite(s.position) && Finite(s.scale) && Finite(s.rotation)
            && Finite(s.alpha) && s.alpha >= 0 && s.alpha <= 1
            && (s.prefab == -1 ? sceneObjects.ContainsKey(s.id) : ValidPrefab(s.prefab) && !sceneObjects.ContainsKey(s.id));
        bool ValidPrefab(int id) => id >= 0 && id < prefabs.Length && prefabs[id] != null;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        static bool Finite(Quaternion p) => Finite(p.x) && Finite(p.y) && Finite(p.z) && Finite(p.w)
            && p.x * p.x + p.y * p.y + p.z * p.z + p.w * p.w > .5f;

        static void DestroyObject(GameObject item)
        {
            item.SetActive(false);
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(item); return; }
#endif
            UnityEngine.Object.Destroy(item);
        }

        internal void Reset()
        {
            IsHost = false;
            foreach (var item in objects.Values) if (item != null && item.PrefabId >= 0)
                DestroyObject(item.gameObject);
            objects.Clear();
            foreach (var pair in sceneObjects)
            {
                if (pair.Value == null) continue;
                pair.Value.ResetObject(); objects.Add(pair.Key, pair.Value);
            }
            nextId = sceneObjects.Count; sendTick = pendingTick = pendingPart = pendingParts = receivedTick = 0;
            pending.Clear();
            foreach (var spawner in spawners) spawner.Reset();
        }
    }
}
