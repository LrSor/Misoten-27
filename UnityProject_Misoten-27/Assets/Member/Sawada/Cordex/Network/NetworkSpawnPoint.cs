using System.Collections.Generic;
using UnityEngine;

namespace Sawada.Cordex
{
    // A Client requests interaction with this fixed point, never an arbitrary spawn position/type.
    public sealed class NetworkSpawnPoint : NetworkWorldObject
    {
        [SerializeField, Min(0)] int prefabIndex;
        [SerializeField] Transform spawnPosition;
        [SerializeField, Min(1)] int maxAlive = 5;
        [SerializeField, Min(.2f)] float cooldown = 1;
        readonly List<NetworkWorldObject> spawned = new List<NetworkWorldObject>();
        float nextSpawn;
        protected override bool Interact(int playerId)
        {
            spawned.RemoveAll(o => o == null || o.Removed || !o.gameObject.activeSelf);
            if (spawned.Count >= maxAlive || Time.time < nextSpawn) return false;
            var point = spawnPosition != null ? spawnPosition : transform;
            var item = World.ServerSpawn(prefabIndex, point.position, point.rotation);
            if (item == null) return false;
            spawned.Add(item); nextSpawn = Time.time + cooldown;
            return true;
        }
    }
}
