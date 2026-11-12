using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Sawada.Cordex
{
    // Read the legacy component's serialized settings without editing it or its prefab.
    internal sealed class NetworkNpcSpawner
    {
        [Serializable] public sealed class Route
        {
            public Transform m_spawnPoint, m_destination;
        }
        [Serializable] public sealed class Settings
        {
            public NPC_VANISH[] m_npcPrefabs = Array.Empty<NPC_VANISH>();
            public Route[] m_routes = Array.Empty<Route>();
            public int m_maxCount = 5;
            public float m_minInterval = 1, m_maxInterval = 3, m_spawnClearRadius = 1.5f, m_navMeshSampleDistance = 2;
        }
        readonly NPC_SPAWNER source;
        readonly bool enabled;
        readonly List<NetworkWorldObject> alive = new List<NetworkWorldObject>();
        public Settings Config { get; }
        float nextSpawn;

        internal NetworkNpcSpawner(NPC_SPAWNER original)
        {
            source = original; enabled = original.enabled;
            Config = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(original));
            original.enabled = false;
        }

        internal void Tick(NetworkWorld world, GameObject[] catalog)
        {
            if (source == null || !enabled || !source.gameObject.activeInHierarchy) return;
            foreach (var item in alive.ToArray())
                if (item == null || item.Removed || !item.gameObject.activeSelf)
                {
                    if (item != null && !item.Removed) world.ServerDespawn(item);
                    alive.Remove(item);
                }
            if (alive.Count >= Config.m_maxCount || Time.time < nextSpawn) return;
            nextSpawn = Time.time + .5f;
            var routes = (Config.m_routes ?? Array.Empty<Route>()).Where(r => r != null && r.m_spawnPoint != null
                && r.m_destination != null && !alive.Any(n => n != null &&
                    (n.transform.position - r.m_spawnPoint.position).sqrMagnitude < Config.m_spawnClearRadius * Config.m_spawnClearRadius)).ToArray();
            var templates = (Config.m_npcPrefabs ?? Array.Empty<NPC_VANISH>()).Where(p => p != null).ToArray();
            if (routes.Length == 0 || templates.Length == 0) return;
            var route = routes[UnityEngine.Random.Range(0, routes.Length)];
            if (!NavMesh.SamplePosition(route.m_spawnPoint.position, out var hit, Config.m_navMeshSampleDistance, NavMesh.AllAreas)) return;
            var direction = route.m_destination.position - hit.position; direction.y = 0;
            var rotation = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : route.m_spawnPoint.rotation;
            int prefab = Array.IndexOf(catalog, templates[UnityEngine.Random.Range(0, templates.Length)].gameObject);
            var spawned = world.ServerSpawn(prefab, hit.position, rotation);
            if (spawned == null) return;
            spawned.GetComponent<NPC_VANISH>().SetDestination(route.m_destination);
            alive.Add(spawned);
            nextSpawn = Time.time + UnityEngine.Random.Range(Mathf.Max(.1f, Config.m_minInterval), Mathf.Max(.1f, Config.m_maxInterval));
        }

        internal void Reset() { alive.Clear(); nextSpawn = 0; }
    }
}
