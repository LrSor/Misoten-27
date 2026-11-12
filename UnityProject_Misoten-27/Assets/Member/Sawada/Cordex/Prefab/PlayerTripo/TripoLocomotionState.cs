using UnityEngine;

namespace Sawada.Cordex
{
    public sealed class TripoLocomotionState : StateMachineBehaviour
    {
        public const string SpeedParameter = "MovementSpeed";
        public const string RateParameter = "LocomotionPlaybackRate";
        const string SprintBlendParameter = "SprintBlend";

        // The source forward cycle covers roughly one unit per second at its original rate.
        public const float NominalWalkSpeed = 1f;
        const float NominalRunSpeed = 4f;

        static readonly int Speed = Animator.StringToHash(SpeedParameter);
        static readonly int Rate = Animator.StringToHash(RateParameter);
        static readonly int SprintBlend = Animator.StringToHash(SprintBlendParameter);

        Transform body;
        PlayerMoveController motor;
        StateMachine movement;
        Vector3 previousPosition;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var rigidbody = animator.GetComponentInParent<Rigidbody>();
            body = rigidbody != null ? rigidbody.transform : animator.transform;
            motor = body.GetComponent<PlayerMoveController>();
            movement = body.GetComponent<StateMachine>();
            previousPosition = body.position;
            animator.SetFloat(Speed, 0f);
            animator.SetFloat(Rate, 1f);
            animator.SetFloat(SprintBlend, 0f);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var displacement = body.position - previousPosition;
            previousPosition = body.position;
            displacement.y = 0f;

            // The same rendered position is available for host physics and interpolated network players.
            var deltaTime = Time.deltaTime;
            var speed = deltaTime > 0f && displacement.sqrMagnitude < 1f
                ? displacement.magnitude / deltaTime
                : 0f;
            animator.SetFloat(Speed, speed > 0.08f ? speed : 0f, 0.12f, deltaTime);

            bool sprinting = movement != null && movement.isActiveAndEnabled
                ? movement.IsInState<Player_Sprint>()
                : motor != null && motor.NetworkSprinting;
            animator.SetFloat(SprintBlend, sprinting ? 1f : 0f, 0.1f, deltaTime);

            float referenceSpeed = Mathf.Lerp(NominalWalkSpeed, NominalRunSpeed, animator.GetFloat(SprintBlend));
            var rate = animator.GetFloat(Speed) / referenceSpeed;
            animator.SetFloat(Rate, Mathf.Clamp(rate, 1f, 2.25f));
        }
    }
}
