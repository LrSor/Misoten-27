using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    internal sealed class LocalWalkPrediction
    {
        internal struct Pose
        {
            public Vector3 Position, Velocity;
            public Quaternion Rotation;
        }

        struct InputStep
        {
            public int Sequence;
            public Vector2 Move;
            public float DeltaTime;
        }

        // Allow normal Relay round trips while bounding the amount of replay work.
        const int HistoryLimit = 20;
        const int ResumeHistorySize = 16;
        const float Skin = .02f;
        const float MaxVisualCorrection = .35f;
        readonly List<InputStep> history = new List<InputStep>(HistoryLimit);
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly PlayerMoveController motor;
        readonly PhysicsScene physics;
        readonly Scene scene;
        readonly int collisionMask;
        readonly float speed, radius, halfSegment;
        readonly Vector3 center;
        Pose predicted;
        Vector3 correction;
        bool ready, sprinting, historySuspended;
        int discardedSequence;

        public LocalWalkPrediction(CapsuleCollider capsule, PlayerMoveController motor, float speed)
        {
            this.motor = motor;
            this.speed = speed;
            scene = capsule.gameObject.scene;
            physics = scene.GetPhysicsScene();
            var scale = capsule.transform.lossyScale;
            radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            halfSegment = Mathf.Max(0, capsule.height * Mathf.Abs(scale.y) * .5f - radius);
            center = Vector3.Scale(capsule.center, scale);
            for (int layer = 0; layer < 32; layer++)
                if (!Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, layer)) collisionMask |= 1 << layer;
        }

        public bool CanDisplay => ready && !sprinting && !historySuspended;

        public void Record(int sequence, Vector2 move, float deltaTime)
        {
            if (history.Count == HistoryLimit)
            {
                historySuspended = true;
                discardedSequence = history[0].Sequence;
                history.RemoveAt(0);
            }
            history.Add(new InputStep { Sequence = sequence, Move = move, DeltaTime = deltaTime });
            if (CanDisplay) Step(ref predicted, move, deltaTime);
        }

        public void Reconcile(Pose confirmed, int appliedSequence, bool hostSprinting)
        {
            bool wasDisplaying = CanDisplay;
            var previous = predicted.Position;
            int appliedCount = 0;
            while (appliedCount < history.Count && history[appliedCount].Sequence <= appliedSequence) appliedCount++;
            if (appliedCount > 0) history.RemoveRange(0, appliedCount);
            if (appliedSequence >= discardedSequence) discardedSequence = 0;
            // Leave a few steps of headroom before resuming to avoid flickering at the limit.
            if (discardedSequence == 0 && history.Count <= ResumeHistorySize) historySuspended = false;
            predicted = confirmed;
            ready = true;
            sprinting = hostSprinting;
            if (CanDisplay)
                foreach (var input in history) Step(ref predicted, input.Move, input.DeltaTime);

            // Only offset actual corrections. Ordinary walking is shown at the predicted pose.
            var error = previous - predicted.Position;
            if (!wasDisplaying || !CanDisplay || error.sqrMagnitude > MaxVisualCorrection * MaxVisualCorrection)
                correction = Vector3.zero;
            else if (error.sqrMagnitude > .000009f)
                correction = Vector3.ClampMagnitude(correction + error, MaxVisualCorrection);
        }

        public Pose Render(Vector2 move, float remainder, float frameTime)
        {
            var pose = predicted;
            if (remainder > 0) Step(ref pose, move, remainder);
            correction *= Mathf.Exp(-24f * frameTime);
            if (correction.sqrMagnitude > .000001f) Sweep(ref pose, correction);
            return pose;
        }

        void Step(ref Pose pose, Vector2 move, float dt)
        {
            var horizontal = motor.StepWalkVelocity(pose.Velocity, move, new Vector3(move.x, 0, move.y), speed, dt);
            pose.Velocity = new Vector3(horizontal.x, pose.Velocity.y, horizontal.z) + Physics.gravity * dt;
            pose.Rotation = motor.StepWalkRotation(pose.Rotation, horizontal, dt);
            Sweep(ref pose, pose.Velocity * dt);
        }

        void Sweep(ref Pose pose, Vector3 delta)
        {
            for (int iteration = 0; iteration < 3; iteration++)
            {
                float distance = delta.magnitude;
                if (distance < .00001f) return;
                var direction = delta / distance;
                var middle = pose.Position + pose.Rotation * center;
                var segment = Vector3.up * halfSegment;
                int count = physics.CapsuleCast(middle + segment, middle - segment, Mathf.Max(.001f, radius - Skin),
                    direction, hits, distance + Skin, collisionMask, QueryTriggerInteraction.Ignore);
                int nearest = -1;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    var collider = hit.collider;
                    if (collider == null || collider.gameObject.scene != scene || collider.attachedRigidbody != null ||
                        collider.GetComponentInParent<NetworkWorldObject>() != null || Vector3.Dot(direction, hit.normal) >= -.001f)
                        continue;
                    if (nearest < 0 || hit.distance < hits[nearest].distance) nearest = i;
                }
                if (nearest < 0) { pose.Position += delta; return; }
                var contact = hits[nearest];
                float travel = Mathf.Clamp(contact.distance - Skin, 0, distance);
                pose.Position += direction * travel;
                delta = Vector3.ProjectOnPlane(delta - direction * travel, contact.normal);
                if (Vector3.Dot(pose.Velocity, contact.normal) < 0)
                    pose.Velocity = Vector3.ProjectOnPlane(pose.Velocity, contact.normal);
            }
        }
    }
}
