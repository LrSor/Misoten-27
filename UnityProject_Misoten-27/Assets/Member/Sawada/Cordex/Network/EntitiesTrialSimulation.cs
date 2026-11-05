using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Sawada.Cordex
{
    // Leave other scenes with a local ECS world. Only the trial session creates network worlds.
    public sealed class EntitiesTrialBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = 0;
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }

    public struct TrialInput : ICommandData
    {
        public NetworkTick Tick { get; set; }
        public float2 Move;
        public float2 Forward;
        public uint SprintPress;
    }

    public struct TrialMotion : IComponentData
    {
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.Interpolate)] public float3 Position;
        [GhostField(Quantization = 1000)] public float3 Velocity;
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.Interpolate)] public float Yaw;
        [GhostField(Quantization = 1000)] public float SprintTime;
        [GhostField] public bool Sprinting;
        [GhostField] public uint LastSprintPress;
    }

    public struct TrialPrefab : IComponentData { public Entity Value; }
    public struct TrialJoined : IComponentData { }
    public struct TrialJoinRpc : IRpcCommand { public FixedString128Bytes Catalog; }
    // Individual RPCs must fit in one reliable transport packet (that pipeline is not fragmented).
    public struct TrialWorldRpc : IRpcCommand
    {
        public int Frame;
        public ushort Part, Parts;
        public FixedString512Bytes Json;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrialPrefabSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var prefab = EntityManager.CreateEntity();
            EntityManager.SetName(prefab, "Sawada.EntitiesTrial.Player.v1");
            EntityManager.AddComponentData(prefab, new TrialMotion());
            EntityManager.AddComponentData(prefab, new GhostOwner());
            EntityManager.AddComponent<GhostOwnerIsLocal>(prefab);
            EntityManager.AddComponent<Simulate>(prefab);
            EntityManager.AddComponentData(prefab, new AutoCommandTarget { Enabled = true });
            EntityManager.AddBuffer<TrialInput>(prefab);
            GhostPrefabCreation.ConvertToGhostPrefab(EntityManager, prefab, new GhostPrefabCreation.Config
            {
                Name = "Sawada.EntitiesTrial.Player.v1",
                Importance = 100,
                SupportedGhostModes = GhostModeMask.All,
                DefaultGhostMode = GhostMode.OwnerPredicted,
                OptimizationMode = GhostOptimizationMode.Dynamic,
                RollbackPredictionOnStructuralChanges = true
            });
            EntityManager.CreateSingleton(new TrialPrefab { Value = prefab });
            var rate = new ClientServerTickRate
            {
                SimulationTickRate = 50, NetworkTickRate = 25,
                // Preserve 20ms motor steps even if rendering falls below 50fps.
                MaxSimulationStepsPerFrame = 4, MaxSimulationStepBatchSize = 1
            };
            rate.ResolveDefaults();
            using var rates = EntityManager.CreateEntityQuery(typeof(ClientServerTickRate));
            if (rates.IsEmptyIgnoreFilter) EntityManager.CreateSingleton(rate);
            else EntityManager.SetComponentData(rates.GetSingletonEntity(), rate);
            Enabled = false;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class TrialInputSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var session = EntitiesTrialSession.Active;
            if (session == null || session.ClientWorld != World) return;
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<TrialInput>>().WithAll<GhostOwnerIsLocal>())
            {
                var input = session.ReadInput();
                input.Tick = tick;
                buffer.AddCommandData(input);
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class TrialMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var time = SystemAPI.GetSingleton<NetworkTime>();
            var tick = time.ServerTick;
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (motion, buffer, owner) in SystemAPI.Query<RefRW<TrialMotion>, DynamicBuffer<TrialInput>, RefRO<GhostOwner>>().WithAll<Simulate>())
            {
                buffer.GetDataAtTick(tick, out var input);
                // A stalled command stream must not keep a player running forever.
                if (!input.Tick.IsValid || tick.TicksSince(input.Tick) > 20)
                    input = new TrialInput { SprintPress = motion.ValueRO.LastSprintPress };
                TrialMotor.Step(ref motion.ValueRW, input, dt);
                var session = EntitiesTrialSession.Active;
                if (session != null && session.ClientWorld == World && !time.IsPartialTick)
                    session.RecordPrediction(owner.ValueRO.NetworkId, tick, motion.ValueRO.Position);
            }
        }
    }

    // A replayable kinematic capsule. Queries existing static map colliders; no Physics.Simulate,
    // Rigidbody events, inventory changes, or dynamic player collisions are replayed.
    public static class TrialMotor
    {
        const float Radius = .48f;
        const float HalfSegment = .5f;
        const float Skin = .02f;

        public static void Step(ref TrialMotion state, TrialInput input, float dt)
        {
            Vector2 move = math.all(math.isfinite(input.Move))
                ? Vector2.ClampMagnitude(new Vector2(input.Move.x, input.Move.y), 1) : Vector2.zero;
            Vector2 forward = math.all(math.isfinite(input.Forward))
                ? new Vector2(input.Forward.x, input.Forward.y).normalized : Vector2.up;
            if (input.SprintPress != state.LastSprintPress)
            {
                state.LastSprintPress = input.SprintPress;
                if (!state.Sprinting && move.sqrMagnitude > .0001f)
                { state.Sprinting = true; state.SprintTime = 0; }
            }
            Vector3 velocity = state.Velocity;
            Vector3 horizontal = new Vector3(velocity.x, 0, velocity.z);
            if (state.Sprinting)
            {
                horizontal = new Vector3(forward.x, 0, forward.y) * Mathf.Lerp(10, 5, Mathf.Clamp01(state.SprintTime / 5));
                state.SprintTime += dt;
                if (state.SprintTime >= 5) state.Sprinting = false;
            }
            else horizontal = Vector3.MoveTowards(horizontal, new Vector3(move.x, 0, move.y) * 5,
                (move.sqrMagnitude > .0001f ? 15 : 20) * dt);
            if (horizontal.sqrMagnitude > .000001f)
                state.Yaw += Mathf.Clamp(Mathf.DeltaAngle(state.Yaw, Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg), -720 * dt, 720 * dt);
            velocity = horizontal + Vector3.up * Mathf.Max(-30, velocity.y - 9.81f * dt);
            Vector3 position = state.Position;
            Vector3 remaining = velocity * dt;
            // A few slide planes handle floor + wall + corners without stepping the global physics scene.
            for (int slide = 0; slide < 4 && remaining.sqrMagnitude > .00000001f; slide++)
            {
                float distance = remaining.magnitude;
                if (!Cast(position, remaining / distance, distance + Skin, out var hit))
                { position += remaining; break; }
                float travel = Mathf.Clamp(hit.distance - Skin, 0, distance);
                position += remaining / distance * travel;
                remaining = Vector3.ProjectOnPlane(remaining * (1 - travel / distance), hit.normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
            }
            state.Position = position;
            state.Velocity = velocity;
        }

        static bool Cast(Vector3 position, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.MaxValue;
            foreach (var hit in Physics.CapsuleCastAll(position + Vector3.up * HalfSegment,
                position - Vector3.up * HalfSegment, Radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                // Dynamic objects remain Host-owned and are outside this movement trial.
                if (hit.rigidbody != null || hit.collider.GetComponentInParent<NetworkWorldObject>() != null) continue;
                if (hit.distance < closest) { closest = hit.distance; nearest = hit; }
            }
            return closest != float.MaxValue;
        }
    }
}
