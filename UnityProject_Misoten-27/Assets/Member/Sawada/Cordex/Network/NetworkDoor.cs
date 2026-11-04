using UnityEngine;

namespace Sawada.Cordex
{
    // Place on the door hinge. The authoritative rotation is replicated by NetworkWorld.
    public sealed class NetworkDoor : NetworkWorldObject
    {
        [SerializeField] Vector3 openEuler = new Vector3(0, 90, 0);
        [SerializeField, Min(1)] float degreesPerSecond = 120;
        bool open;
        protected override int Value { get => open ? 1 : 0; set => open = value != 0; }
        protected override bool Interact(int playerId) { open = !open; return true; }
        protected override void Update()
        {
            base.Update();
            if (World == null || !World.IsHost) return;
            Quaternion target = open ? InitialRotation * Quaternion.Euler(openEuler) : InitialRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
        }
    }
}
