using UnityEngine;

namespace Sawada.Cordex
{
    // Owned by the session; no scene or prefab attachment is required.
    internal sealed class WorldSlowController
    {
        bool active;
        float originalScale, originalFixedDeltaTime;

        public void Begin()
        {
            if (active) return;
            originalScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            active = true;
            Time.timeScale = originalScale * ScoreRankingCoordinator.SlowScale;
            Time.fixedDeltaTime = originalFixedDeltaTime * ScoreRankingCoordinator.SlowScale;
        }

        public void Restore()
        {
            if (!active) return;
            Time.timeScale = originalScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            active = false;
        }
    }
}
