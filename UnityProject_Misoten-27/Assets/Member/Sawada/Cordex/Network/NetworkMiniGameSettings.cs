using UnityEngine;
using UnityEngine.Serialization;

namespace Sawada.Cordex
{
    /// <summary>Presentation timing only. Game time and star ratings come from the MiniGame itself.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMiniGameSettings : MonoBehaviour
    {
        [FormerlySerializedAs("resultSeconds")]
        [Min(.1f)] public float m_resultSeconds = 2f;
    }
}
