using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to the imported character model root (do not change its child transforms).
public sealed class ModularCharacter : MonoBehaviour
{
    public Transform commonRig;
    public Renderer headRenderer;
    public SkinnedMeshRenderer[] embeddedOutfits;
    public GameObject[] outfitPrefabs;
    GameObject equipped;
    MaterialPropertyBlock headProperties;

    void Awake() { FindReferences(); }

    void FindReferences()
    {
        if (!commonRig)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Common_Rig") { commonRig = t; break; }
        if (!headRenderer)
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name == "Head_UV") { headRenderer = r; break; }
    }

    public void SetHeadTexture(Texture texture)
    {
        FindReferences();
        if (!headRenderer || !headRenderer.sharedMaterial)
            throw new InvalidOperationException("Assign Head_UV and its own material first.");
        var mat = headRenderer.sharedMaterial;
        string map = mat.HasProperty("_BaseMap") ? "_BaseMap" :
            mat.HasProperty("_BaseColorMap") ? "_BaseColorMap" :
            mat.HasProperty("_MainTex") ? "_MainTex" : null;
        if (map == null) throw new InvalidOperationException("Unsupported head shader texture property.");
        if (headProperties == null) headProperties = new MaterialPropertyBlock();
        headRenderer.GetPropertyBlock(headProperties);
        headProperties.SetTexture(map, texture);
        if (mat.HasProperty("_BaseColor")) headProperties.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) headProperties.SetColor("_Color", Color.white);
        headRenderer.SetPropertyBlock(headProperties);
    }

    // -1 = no clothing. Register all existing clothing renderers in the Inspector.
    public void SelectEmbeddedOutfit(int index)
    {
        int length = embeddedOutfits == null ? 0 : embeddedOutfits.Length;
        if (index < -1 || index >= length) throw new ArgumentOutOfRangeException(nameof(index));
        ClearDynamicOutfit();
        for (int i = 0; i < length; ++i)
            if (embeddedOutfits[i]) embeddedOutfits[i].enabled = i == index;
    }

    // Prefabs must use the same exported rest skeleton, model-root transforms and units.
    // Copies mesh renderers only; all bones reference this character's existing rig.
    public void EquipPrefab(int index)
    {
        if (outfitPrefabs == null || index < 0 || index >= outfitPrefabs.Length || !outfitPrefabs[index])
            throw new ArgumentOutOfRangeException(nameof(index));
        FindReferences();
        if (!commonRig) throw new InvalidOperationException("Assign Common_Rig; disable Optimize Game Objects.");
        var bones = new Dictionary<string, Transform>();
        foreach (var bone in commonRig.GetComponentsInChildren<Transform>(true))
        {
            if (bones.ContainsKey(bone.name)) throw new InvalidOperationException("Duplicate bone: " + bone.name);
            bones.Add(bone.name, bone);
        }
        var source = outfitPrefabs[index];
        var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Outfit has no skinned meshes.");
        var next = new GameObject("Equipped_" + source.name);
        next.transform.SetParent(transform, false);
        next.SetActive(false);
        try
        {
            foreach (var src in renderers)
            {
                var mapped = new Transform[src.bones.Length];
                for (int i = 0; i < mapped.Length; ++i)
                    if (!src.bones[i] || !bones.TryGetValue(src.bones[i].name, out mapped[i]))
                        throw new InvalidOperationException("Missing outfit bone on Common_Rig.");
                Transform root = commonRig;
                if (src.rootBone && !bones.TryGetValue(src.rootBone.name, out root))
                    throw new InvalidOperationException("Missing outfit root bone.");
                var go = new GameObject(src.name);
                go.transform.SetParent(next.transform, false);
                // Preserve every FBX node transform between renderer and model root.
                Matrix4x4 matrix = source.transform.worldToLocalMatrix * src.transform.localToWorldMatrix;
                go.transform.localPosition = matrix.GetColumn(3);
                go.transform.localRotation = matrix.rotation;
                go.transform.localScale = matrix.lossyScale;
                var dst = go.AddComponent<SkinnedMeshRenderer>();
                dst.sharedMesh = src.sharedMesh;
                dst.sharedMaterials = src.sharedMaterials;
                dst.bones = mapped;
                dst.rootBone = root;
                dst.localBounds = src.localBounds;
                dst.quality = src.quality;
                dst.shadowCastingMode = src.shadowCastingMode;
                dst.receiveShadows = src.receiveShadows;
                // Bounds are recalculated while moving; optimize after animation QA.
                dst.updateWhenOffscreen = true;
            }
        }
        catch { Destroy(next); throw; }
        SelectEmbeddedOutfit(-1);
        equipped = next;
        equipped.SetActive(true);
    }

    void ClearDynamicOutfit()
    {
        if (!equipped) return;
        equipped.SetActive(false);
        Destroy(equipped);
        equipped = null;
    }
}
