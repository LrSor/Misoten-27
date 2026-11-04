using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Sawada.Cordex
{
    // Add to the root of an NPC / movable prop. Simulation runs only on Host.
    public class NetworkWorldObject : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float interactionDistance = 3f;
        [SerializeField] bool allowClientDelete;
        [Tooltip("Visual-only scripts which may also run on Clients.")]
        [SerializeField] MonoBehaviour[] clientVisualScripts = Array.Empty<MonoBehaviour>();
        public int NetworkId { get; internal set; }
        internal NetworkWorld World { get; set; }
        internal int PrefabId;
        internal bool Removed;
        internal Vector3 InitialPosition, InitialScale;
        internal Quaternion InitialRotation;
        internal bool InitialActive;
        MonoBehaviour[] simulation;
        bool[] simulationEnabled;
        Rigidbody[] bodies;
        bool[] kinematic, gravity;
        CollisionDetectionMode[] collisionModes;
        RigidbodyInterpolation[] interpolationModes;
        NavMeshAgent[] agents;
        bool[] agentEnabled;
        Collider[] colliders;
        bool[] colliderEnabled;
        Vector3 targetPosition, targetScale;
        Quaternion targetRotation;
        bool hasTarget;

        internal void Initialize(NetworkWorld world, int id, int prefab)
        {
            World = world; NetworkId = id; PrefabId = prefab;
            InitialPosition = transform.position; InitialRotation = transform.rotation;
            InitialScale = transform.localScale; InitialActive = gameObject.activeSelf;
            simulation = GetComponentsInChildren<MonoBehaviour>(true).Where(s => s != null
                && !(s is NetworkWorldObject) && !(s is FieldArmorMachine) && !(s is NPC_FADER)
                && !clientVisualScripts.Contains(s)).ToArray();
            simulationEnabled = simulation.Select(s => s.enabled).ToArray();
            bodies = GetComponentsInChildren<Rigidbody>(true);
            kinematic = bodies.Select(b => b.isKinematic).ToArray();
            gravity = bodies.Select(b => b.useGravity).ToArray();
            collisionModes = bodies.Select(b => b.collisionDetectionMode).ToArray();
            interpolationModes = bodies.Select(b => b.interpolation).ToArray();
            agents = GetComponentsInChildren<NavMeshAgent>(true);
            agentEnabled = agents.Select(a => a.enabled).ToArray();
            colliders = GetComponentsInChildren<Collider>(true);
            colliderEnabled = colliders.Select(c => c.enabled).ToArray();
        }

        internal void SetAuthority(bool host)
        {
            for (int i = 0; i < simulation.Length; i++) if (simulation[i] != null)
                simulation[i].enabled = host && simulationEnabled[i];
            for (int i = 0; i < agents.Length; i++) if (agents[i] != null)
                agents[i].enabled = host && agentEnabled[i];
            for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null)
            {
                if (!host) bodies[i].collisionDetectionMode = CollisionDetectionMode.Discrete;
                bodies[i].isKinematic = !host || kinematic[i];
                bodies[i].useGravity = host && gravity[i];
                if (host) bodies[i].collisionDetectionMode = collisionModes[i];
                bodies[i].interpolation = host ? interpolationModes[i] : RigidbodyInterpolation.None;
            }
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null)
                colliders[i].enabled = host && colliderEnabled[i];
        }

        internal NetworkWorld.State Capture() => new NetworkWorld.State {
            id = NetworkId, prefab = PrefabId, active = !Removed && gameObject.activeSelf,
            position = transform.position, rotation = transform.rotation, scale = transform.localScale,
            value = Value, alpha = GetComponent<NPC_FADER>() is NPC_FADER f ? f.Alpha : 1f
        };

        internal void Apply(NetworkWorld.State state, bool first)
        {
            targetPosition = state.position; targetRotation = state.rotation; targetScale = state.scale;
            hasTarget = true;
            Value = state.value;
            gameObject.SetActive(state.active);
            if (first) transform.SetPositionAndRotation(targetPosition, targetRotation);
            transform.localScale = targetScale;
            // A never-activated proxy has not run the fader's Awake yet.
            if (gameObject.activeInHierarchy && GetComponent<NPC_FADER>() is NPC_FADER f) f.SetAlpha(state.alpha);
            // A fader or OnEnable may touch colliders; proxies never own collision.
            SetAuthority(false);
        }

        protected virtual int Value { get; set; }
        protected virtual bool Interact(int playerId) => false;
        internal bool HandleAction(int playerId, Vector3 playerPosition, string action)
        {
            if (World == null || !World.IsHost || Removed || !gameObject.activeInHierarchy
                || (playerPosition - transform.position).sqrMagnitude > interactionDistance * interactionDistance)
                return false;
            if (action == "interact") return Interact(playerId);
            if (action != "delete" || !allowClientDelete) return false;
            return World.ServerDespawn(this);
        }

        public bool RequestInteraction() => World != null && World.RequestAction(NetworkId, "interact");
        public bool RequestDelete() => World != null && World.RequestAction(NetworkId, "delete");

        protected virtual void Update()
        {
            if (World == null || World.IsHost || !hasTarget) return;
            float t = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, t),
                Quaternion.Slerp(transform.rotation, targetRotation, t));
        }

        internal void ResetObject()
        {
            Removed = hasTarget = false; Value = 0;
            transform.SetPositionAndRotation(InitialPosition, InitialRotation);
            transform.localScale = InitialScale;
            foreach (var body in bodies) if (body != null && !body.isKinematic)
            { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            if (GetComponent<NPC_FADER>() is NPC_FADER f) f.SetAlpha(1);
            gameObject.SetActive(InitialActive);
            SetAuthority(false);
        }
    }
}
