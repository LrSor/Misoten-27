using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sawada.Cordex
{
    /// <summary>Connects rigged outfits below any named container to the shared skeleton.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class AutoRigSlot : MonoBehaviour
    {
        [Serializable] sealed class SkinBinding
        {
            // Keep the serialized source references so saved outfits can still be detached safely.
            public SkinnedMeshRenderer renderer;
            public Transform[] originalBones;
            public Transform originalRoot;
            public bool originalOffscreen;
            public int originalLayer;
            public bool hasOriginalLayer;
            [NonSerialized] public Transform[] mappedBones;
            [NonSerialized] public Transform mappedRoot;
            [NonSerialized] public Transform mappedRig;
            [NonSerialized] public Mesh mappedMesh;
            [NonSerialized] public int mappedVersion;
            [NonSerialized] public bool applied;
        }
        [Serializable] sealed class AnimatorBinding
        {
            public Animator animator;
            public bool originalEnabled;
        }

        [SerializeField, HideInInspector] List<SkinBinding> skins = new();
        [SerializeField, HideInInspector] List<AnimatorBinding> animators = new();
        static readonly Dictionary<SkinnedMeshRenderer, AutoRigSlot> owners = new();
        readonly HashSet<int> warned = new();
        readonly List<SkinnedMeshRenderer> foundSkins = new();
        readonly List<Animator> foundAnimators = new();
        readonly List<AutoRigSlot> otherSlots = new();
        AutoRigWardrobe wardrobe;
        double nextScan;

        void OnEnable()
        {
            nextScan = 0;
            if (gameObject.scene.IsValid())
                foreach (var binding in skins)
                    if (binding.renderer != null && Owns(binding.renderer.transform)) owners[binding.renderer] = this;
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
        }
#if UNITY_EDITOR
        void EditorTick() { if (!Application.isPlaying) LateUpdate(); }
#endif
        void OnTransformChildrenChanged() { nextScan = 0; }
        void OnTransformParentChanged() { nextScan = 0; }
        void OnValidate() { nextScan = 0; }

        void LateUpdate()
        {
            if (!gameObject.scene.IsValid() || Time.realtimeSinceStartupAsDouble < nextScan) return;
            Synchronize(false);
            nextScan = Time.realtimeSinceStartupAsDouble + .2;
        }

        bool Owns(Transform item) => item != null && item.IsChildOf(transform)
            && item.GetComponentInParent<AutoRigSlot>(true) == this;

        [ContextMenu("Synchronize attachments now")]
        public void Synchronize() => Synchronize(true);

        void Synchronize(bool force)
        {
            if (!gameObject.scene.IsValid()) return;
            wardrobe = GetComponentInParent<AutoRigWardrobe>();
            if (wardrobe == null || !wardrobe.isActiveAndEnabled || !wardrobe.TryGetBone("Hips", out _))
            { RestoreAll(); return; }

            // Release the previous slot before acquiring an outfit moved between containers.
            wardrobe.GetComponentsInChildren(true, otherSlots);
            foreach (var other in otherSlots)
                if (other != this) other.ReleaseRemoved();
            bool changed = ReleaseRemoved();
            GetComponentsInChildren(true, foundSkins);
            int playerLayer = LayerMask.NameToLayer("Player");
            foreach (var renderer in foundSkins)
            {
                if (!Owns(renderer.transform) || renderer.sharedMesh == null) continue;
                if (owners.TryGetValue(renderer, out var previous) && previous != null && previous != this)
                    previous.ReleaseRemoved(); // Also supports moving outfits to another character.

                var source = FindSkin(renderer);
                bool isNew = source == null;
                if (isNew) source = new SkinBinding { renderer = renderer, originalBones = renderer.bones,
                    originalRoot = renderer.rootBone, originalOffscreen = renderer.updateWhenOffscreen };

                if (source.mappedBones == null || source.mappedRig != wardrobe.CommonRig
                    || source.mappedVersion != wardrobe.BoneVersion || source.mappedMesh != renderer.sharedMesh)
                {
                    if (!MapBones(source))
                    {
                        if (!isNew) { Restore(source); skins.Remove(source); changed = true; }
                        Warn(renderer, "共通Rigに対応しない骨があります。骨名・ウェイトとOptimize Game Objectsの設定を確認してください。");
                        continue;
                    }
                }
                if (isNew) { skins.Add(source); changed = true; }
                // Change only the rendered outfit object, leaving colliders and cameras alone.
                if (playerLayer >= 0)
                {
                    if (!source.hasOriginalLayer)
                    {
                        source.originalLayer = renderer.gameObject.layer;
                        source.hasOriginalLayer = true;
                        changed = true;
                    }
                    if (renderer.gameObject.layer != playerLayer)
                    {
                        renderer.gameObject.layer = playerLayer;
                        Mark(renderer.gameObject);
                    }
                }
                owners[renderer] = this;
                if (force || !source.applied || renderer.rootBone != source.mappedRoot || !renderer.updateWhenOffscreen)
                {
                    renderer.bones = source.mappedBones;
                    renderer.rootBone = source.mappedRoot;
                    renderer.updateWhenOffscreen = true;
                    source.applied = true;
                    Mark(renderer);
                }
            }

            GetComponentsInChildren(true, foundAnimators);
            foreach (var animator in foundAnimators)
            {
                if (!Owns(animator.transform)) continue;
                bool known = false;
                foreach (var binding in animators) if (binding.animator == animator) { known = true; break; }
                if (!known) { animators.Add(new AnimatorBinding { animator = animator, originalEnabled = animator.enabled }); changed = true; }
                if (animator.enabled) { animator.enabled = false; Mark(animator); }
            }
            if (changed) Mark(this);
        }

        SkinBinding FindSkin(SkinnedMeshRenderer renderer)
        {
            foreach (var binding in skins) if (binding.renderer == renderer) return binding;
            return null;
        }

        bool MapBones(SkinBinding binding)
        {
            var original = binding.originalBones;
            if (original == null || original.Length == 0) return false;
            if (binding.mappedBones == null || binding.mappedBones.Length != original.Length)
                binding.mappedBones = new Transform[original.Length];
            for (int i = 0; i < original.Length; i++)
                if (original[i] == null || !wardrobe.TryGetBone(original[i].name, out binding.mappedBones[i])) return false;
            Transform root = wardrobe.CommonRig;
            if (binding.originalRoot != null && !wardrobe.TryGetBone(binding.originalRoot.name, out root)) return false;
            binding.mappedRoot = root;
            binding.mappedRig = wardrobe.CommonRig;
            binding.mappedVersion = wardrobe.BoneVersion;
            binding.mappedMesh = binding.renderer.sharedMesh;
            binding.applied = false;
            return true;
        }

        bool ReleaseRemoved()
        {
            bool changed = false;
            for (int i = skins.Count - 1; i >= 0; i--)
                if (skins[i].renderer == null || !Owns(skins[i].renderer.transform))
                { Restore(skins[i]); skins.RemoveAt(i); changed = true; }
            for (int i = animators.Count - 1; i >= 0; i--)
                if (animators[i].animator == null || !Owns(animators[i].animator.transform))
                { Restore(animators[i]); animators.RemoveAt(i); changed = true; }
            if (changed) Mark(this);
            return changed;
        }

        void Warn(UnityEngine.Object item, string message)
        { if (warned.Add(item.GetInstanceID())) Debug.LogWarning($"[{name}] {item.name}: {message}", item); }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            RestoreAll();
            warned.Clear();
        }

        void RestoreAll()
        {
            if (skins.Count == 0 && animators.Count == 0) return;
            foreach (var skin in skins) Restore(skin);
            foreach (var animator in animators) Restore(animator);
            skins.Clear(); animators.Clear();
            Mark(this);
        }

        void Restore(SkinBinding binding)
        {
            if (!ReferenceEquals(binding.renderer, null) && owners.TryGetValue(binding.renderer, out var owner) && owner == this)
                owners.Remove(binding.renderer);
            if (binding.renderer == null) return;
            binding.renderer.bones = binding.originalBones;
            binding.renderer.rootBone = binding.originalRoot;
            binding.renderer.updateWhenOffscreen = binding.originalOffscreen;
            if (binding.hasOriginalLayer)
            {
                binding.renderer.gameObject.layer = binding.originalLayer;
                Mark(binding.renderer.gameObject);
                binding.hasOriginalLayer = false;
            }
            binding.applied = false;
            Mark(binding.renderer);
        }
        static void Restore(AnimatorBinding binding)
        { if (binding.animator != null) { binding.animator.enabled = binding.originalEnabled; Mark(binding.animator); } }

        static void Mark(UnityEngine.Object item)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && item != null)
            {
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
#endif
        }
    }
}
