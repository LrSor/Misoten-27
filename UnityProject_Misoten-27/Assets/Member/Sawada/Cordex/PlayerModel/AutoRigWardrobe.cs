using System.Collections.Generic;
using UnityEngine;

namespace Sawada.Cordex
{
    /// <summary>Shared skeleton used by the outfit slots below this object.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AutoRigWardrobe : MonoBehaviour
    {
        [SerializeField] Transform commonRig;
        readonly Dictionary<string, Transform> bones = new();
        Transform cachedRig;
        bool valid;
        public int BoneVersion { get; private set; }

        public Transform CommonRig => commonRig != null ? commonRig : transform.parent != null
            ? transform.parent.Find("Common_Rig") : null;

        public bool TryGetBone(string boneName, out Transform bone)
        {
            var rig = CommonRig;
            if (rig != cachedRig || bones.Count == 0)
            {
                cachedRig = rig;
                bones.Clear();
                BoneVersion++;
                valid = rig != null;
                if (rig != null)
                    foreach (var item in rig.GetComponentsInChildren<Transform>(true))
                    {
                        if (!bones.TryAdd(item.name, item)) valid = false;
                    }
            }
            bone = null;
            return valid && boneName != null && bones.TryGetValue(boneName, out bone);
        }

        void OnEnable() { cachedRig = null; bones.Clear(); }
        void OnValidate() { cachedRig = null; bones.Clear(); }
    }
}
