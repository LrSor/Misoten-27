using UnityEngine;

namespace Sawada.Cordex
{
    /// <summary>Optional per-instance rules. Attach to the MiniGameSystem in a scene, not its prefab asset.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMiniGameSettings : MonoBehaviour
    {
        [Min(1f)] public float fallbackTimeLimit = 10f;
        [Min(.1f)] public float resultSeconds = 2f;
        [Min(0)] public int twoStarScore = 100;
        [Min(0)] public int threeStarScore = 150;

        public int Evaluate(int score, bool clear) => !clear ? 0
            : score >= Mathf.Max(twoStarScore, threeStarScore) ? 3 : score >= twoStarScore ? 2 : 1;
    }
}
