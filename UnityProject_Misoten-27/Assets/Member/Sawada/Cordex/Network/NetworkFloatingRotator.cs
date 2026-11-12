using UnityEngine;

namespace Sawada.Cordex
{
    // Put on the parent Empty. Its model children follow the replicated root pose.
    public sealed class NetworkFloatingRotator : NetworkWorldObject
    {
        [SerializeField, Min(0f)] float amplitude = .5f;
        [SerializeField, Min(.1f)] float period = 3f;
        [SerializeField] float degreesPerSecond = 45f;
        float elapsed;

        protected override void Update()
        {
            // Clients interpolate the Host's pose; they never run their own motion clock.
            base.Update();
            if (World == null || !World.IsHost)
            {
                elapsed = 0f;
                return;
            }

            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * (2f * Mathf.PI / Mathf.Max(.1f, period))) * amplitude;
            transform.SetPositionAndRotation(InitialPosition + Vector3.up * offset,
                InitialRotation * Quaternion.Euler(0f, elapsed * degreesPerSecond, 0f));
        }
    }
}
