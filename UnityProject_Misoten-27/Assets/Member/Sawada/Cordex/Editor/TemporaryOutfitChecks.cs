using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sawada.Cordex
{
    // Batch-only Play Mode checks. Existing scenes and prefabs are never saved.
    [InitializeOnLoad]
    public static class TemporaryOutfitChecks
    {
        const string Pending = "Sawada.Cordex.TemporaryOutfitChecks.Pending";

        static TemporaryOutfitChecks()
        {
            if (SessionState.GetBool(Pending, false))
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in Unity batch mode.");
            CheckUnifiedReferences();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(Pending, false);
            try
            {
                CheckReplacement();
                CheckImportedOutfits();
                CheckUnifiedAnimation();
                Debug.Log("[TemporaryOutfitChecks] PASS: replacement, same-frame switching, null/empty entries, unequip, explicit target, player isolation, FBX rig binding, missing scripts");
                Debug.Log("[TemporaryOutfitChecks] PASS: unified FBX references, CordexScene, prefab materials, humanoid animation");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Temporary outfit check failed: " + message);
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static (CharacterAmorController controller, Transform target) Player(string name)
        {
            var player = new GameObject(name);
            var model = Child(player.transform, "Model");
            var wardrobe = Child(model, "Wardrobe");
            return (player.AddComponent<CharacterAmorController>(), Child(wardrobe, "TemporaryOutfit"));
        }

        static void CheckReplacement()
        {
            var first = Player("FirstPlayer");
            var second = Player("SecondPlayer");
            var old = Child(first.target, "Existing FBX");
            var shirt = new GameObject("Shirt");
            var pants = new GameObject("Pants");
            shirt.transform.localScale = new Vector3(2, 3, 4);
            var armor = ScriptableObject.CreateInstance<BaseArmorSO>();
            armor.m_temporaryOutfit = new List<GameObject> { shirt, null, pants };
            Check(first.controller.FitArmor(armor) == null, "initial previous armor");
            Check(first.target.childCount == 2, "all non-null models instantiated");
            Check(!old.gameObject.activeSelf && old.parent == null, "old outfit immediately removed");
            Check(first.target.GetChild(0).gameObject != shirt, "model is cloned");
            Check(first.target.GetChild(0).localScale == shirt.transform.localScale, "source scale retained");
            second.controller.FitArmor(armor);
            var replacement = ScriptableObject.CreateInstance<BaseArmorSO>();
            replacement.m_temporaryOutfit = new List<GameObject> { pants };
            Check(first.controller.FitArmor(replacement) == armor, "previous armor returned");
            Check(first.target.childCount == 1, "same-frame switch has no duplicates");
            Check(second.target.childCount == 2, "other player's outfit unchanged");
            Check(first.controller.FitArmor(null) == replacement && first.target.childCount == 0, "unequip clears models");
            armor.m_temporaryOutfit.Clear();
            first.controller.FitArmor(armor);
            Check(first.target.childCount == 0, "empty list");
            armor.m_temporaryOutfit = null;
            first.controller.FitArmor(armor);
            Check(first.target.childCount == 0, "null list");
            var explicitTarget = Child(first.controller.transform, "CustomSlot");
            typeof(CharacterAmorController).GetField("m_temporaryOutfit", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(first.controller, explicitTarget);
            first.controller.FitArmor(replacement);
            Check(explicitTarget.childCount == 1, "explicit target");
            Check(shirt != null && pants != null, "source models preserved");
        }

        static void CheckImportedOutfits()
        {
            var player = Player("RiggedPlayer");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Member/Sawada/FBX/Player/Sawada_Base_20261116.fbx");
            Check(source != null, "base FBX exists");
            var body = Object.Instantiate(source, player.target.parent.parent, false);
            var rig = body.GetComponentsInChildren<Transform>(true).First(t => t.name == "Common_Rig");
            var wardrobe = player.target.parent.gameObject.AddComponent<AutoRigWardrobe>();
            typeof(AutoRigWardrobe).GetField("commonRig", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wardrobe, rig);
            player.target.gameObject.AddComponent<AutoRigSlot>();
            for (int i = 1; i <= 3; i++)
            {
                var armor = AssetDatabase.LoadAssetAtPath<BaseArmorSO>($"Assets/Member/Sawada/Scripts/Armor/SO/BaseArmor0{i}.asset");
                Check(armor != null && armor.m_temporaryOutfit != null && armor.m_temporaryOutfit.Any(m => m != null), "armor model references");
                player.controller.FitArmor(armor);
                Check(player.target.childCount == armor.m_temporaryOutfit.Count(m => m != null), "imported model count");
                var skins = player.target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Check(skins.Length > 0, "imported outfit has skinned meshes");
                Check(skins.All(s => s.bones.Length > 0 && s.bones.All(b => b != null && b.IsChildOf(rig))), "bones connected to player rig");
                Check(skins.All(s => s.gameObject.layer == LayerMask.NameToLayer("Player")), "player render layer");
            }
            foreach (string path in new[] { "Assets/Member/Sawada/Prefab/Player/Player_ver3.prefab", "Assets/Member/Sawada/Prefab/Player/Player_cordex.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null && prefab.GetComponentsInChildren<Transform>(true)
                    .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "prefab missing scripts: " + path);
            }
        }

        static void CheckUnifiedReferences()
        {
            const string modelPath = "Assets/Member/Sawada/FBX/Player/Sawada_Base_20261116.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Check(model != null, "unified FBX exists");
            string guid = AssetDatabase.AssetPathToGUID(modelPath);
            var ids = new HashSet<long>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .Concat(model.GetComponentsInChildren<Component>(true).Cast<Object>())
                .Concat(model.GetComponentsInChildren<Transform>(true).Select(t => (Object)t.gameObject)))
            {
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long id)
                    && assetGuid == guid) ids.Add(id);
            }
            foreach (string file in new[] { "Assets/Member/Sawada/Prefab/Player/Player_cordex.prefab", "Assets/Member/Sawada/Scenes/CordexScene.unity" })
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\{fileID: (-?\d+), guid: " + guid))
                {
                    long id = long.Parse(match.Groups[1].Value);
                    Check(id == 100100000 || ids.Contains(id), "FBX fileID " + id + " in " + file);
                }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Member/Sawada/Scenes/CordexScene.unity");
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                Check(transforms.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "scene missing scripts");
                var models = transforms.Where(t => t.name == "Sawada_Base_HeadBodyRig").ToArray();
                Check(models.Length > 0, "scene base model");
                foreach (var item in models) CheckModel(item.gameObject, modelPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void CheckModel(GameObject model, string expectedPath)
        {
            Check(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(model)) == expectedPath,
                "model source: " + model.name);
            var animator = model.GetComponent<Animator>();
            Check(animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                "humanoid avatar: " + model.name);
            Check(animator.runtimeAnimatorController != null && !animator.applyRootMotion, "animation settings");
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(skins.Length >= 2 && skins.All(s => s.sharedMesh != null
                && s.sharedMaterials.All(m => m != null && m.shader != null)), "model meshes and materials");
        }

        static void CheckUnifiedAnimation()
        {
            foreach (string file in new[] { "Assets/Member/Sawada/Prefab/Player/Player_ver3.prefab", "Assets/Member/Sawada/Prefab/Player/Player_cordex.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                var model = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Sawada_Base_HeadBodyRig");
                CheckModel(model.gameObject, "Assets/Member/Sawada/FBX/Player/Sawada_Base_20261116.fbx");
                var copy = Object.Instantiate(model.gameObject);
                var animator = copy.GetComponent<Animator>();
                animator.Rebind();
                animator.Update(0);
                Check(animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Hips) != null, "runtime humanoid binding: " + file);
                Object.Destroy(copy);
            }
        }
    }
}
