using Unity.Cinemachine;
using UnityEngine;

namespace Sawada.Cordex
{
    // Apply effects to the generated camera state, leaving the saved lens and follow target intact.
    [DisallowMultipleComponent]
    public sealed class TackleCameraEffects : CinemachineExtension
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Min(0f)] public float SprintFovBoost = 15f;
            [Min(0f)] public float FovBlendTime = 0.2f;
            [Min(0f)] public float HitShakeDuration = 0.3f;
            [Min(0f)] public float HitShakeAngle = 1.5f;
            [Min(0f)] public float HitShakeFrequency = 25f;
        }

        PlayerMoveController m_owner;
        Settings m_settings;
        int m_seenHits;
        float m_fovOffset;
        float m_shakeStarted = float.NegativeInfinity;

        public void Bind(PlayerMoveController owner, Settings settings)
        {
            m_owner = owner;
            m_settings = settings;
            m_seenHits = owner.TackleHitSequence;
            m_fovOffset = 0f;
            m_shakeStarted = float.NegativeInfinity;
        }

        void OnDisable()
        {
            m_fovOffset = 0f;
            m_shakeStarted = float.NegativeInfinity;
            if (m_owner != null) m_seenHits = m_owner.TackleHitSequence;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || m_owner == null || m_settings == null)
                return;

            float target = m_owner.SprintCameraActive ? Mathf.Max(0f, m_settings.SprintFovBoost) : 0f;
            m_fovOffset = deltaTime < 0f || m_settings.FovBlendTime <= 0f ? target
                : Mathf.MoveTowards(m_fovOffset, target,
                    Mathf.Max(0f, m_settings.SprintFovBoost) * deltaTime / m_settings.FovBlendTime);
            state.Lens.FieldOfView = Mathf.Clamp(state.Lens.FieldOfView + m_fovOffset, 1f, 179f);

            if (m_seenHits != m_owner.TackleHitSequence)
            {
                m_seenHits = m_owner.TackleHitSequence;
                m_shakeStarted = Time.time;
            }

            float elapsed = Time.time - m_shakeStarted;
            if (elapsed < 0f || elapsed >= m_settings.HitShakeDuration || m_settings.HitShakeDuration <= 0f)
                return;

            float amplitude = m_settings.HitShakeAngle * (1f - elapsed / m_settings.HitShakeDuration);
            float phase = elapsed * m_settings.HitShakeFrequency;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(phase, 0.37f),
                Mathf.PerlinNoise(phase, 7.13f), Mathf.PerlinNoise(phase, 15.71f)) * 2f - Vector3.one;
            // Angular shake avoids pushing the camera through nearby walls.
            state.OrientationCorrection *= Quaternion.Euler(noise * amplitude);
        }
    }
}
