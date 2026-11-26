using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sawada.Cordex.EditorTools
{
    // Run with -executeMethod Sawada.Cordex.EditorTools.SelectedMaterialsToUTSChecks.RunBatch.
    public static class SelectedMaterialsToUTSChecks
    {
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
            string root = "Assets/Member/Sawada/Cordex/UTSChecks_" + Guid.NewGuid().ToString("N");
            Object[] previousSelection = Selection.objects;
            Material outside = null;
            try
            {
                AssetDatabase.CreateFolder("Assets/Member/Sawada/Cordex", root.Substring(root.LastIndexOf('/') + 1));
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                Check(lit != null && Shader.Find("Toon/Toon") != null, "URP and UTS shaders exist");
                var texture = new Texture2D(2, 2);
                AssetDatabase.CreateAsset(texture, root + "/Texture.asset");
                var first = new Material(lit) { name = "First", enableInstancing = true };
                var second = new Material(lit) { name = "Second" };
                first.SetTexture("_BaseMap", texture);
                first.SetColor("_BaseColor", new Color(0.8f, 0.5f, 0.25f, 1));
                first.SetTextureScale("_BaseMap", new Vector2(2, 3));
                first.SetTextureOffset("_BaseMap", new Vector2(0.1f, 0.2f));
                first.SetTexture("_BumpMap", texture);
                first.EnableKeyword("_NORMALMAP");
                first.SetFloat("_BumpScale", 0.7f);
                first.SetTexture("_EmissionMap", texture);
                first.SetColor("_EmissionColor", new Color(0.2f, 0.3f, 0.4f, 1));
                first.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                first.EnableKeyword("_EMISSION");
                second.SetFloat("_AlphaClip", 1);
                second.SetFloat("_Cutoff", 0.7f);
                second.SetColor("_BaseColor", new Color(1, 1, 1, 0.8f));
                second.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(first, root + "/First.mat");
                AssetDatabase.CreateAsset(second, root + "/Second.mat");
                string guid = AssetDatabase.AssetPathToGUID(root + "/First.mat");
                var options = new SelectedMaterialsToUTS.Settings();
                var messages = new List<string>();

                Selection.objects = new Object[] { first, texture };
                Check(SelectedMaterialsToUTS.GetSelectedMaterials().SequenceEqual(new[] { first }), "single selection ignores other asset types");
                Selection.objects = new Object[] { second, first, texture };
                var selected = SelectedMaterialsToUTS.GetSelectedMaterials();
                Check(selected.Length == 2, "multiple selection includes only materials");
                Check(SelectedMaterialsToUTS.ConvertMaterials(selected, options, messages) == 2, "batch converts both materials");
                Check(first.shader.name == "Toon/Toon" && second.shader.name == "Toon/Toon", "UTS assigned");
                Check(first.name == "First" && first.enableInstancing, "name and instancing retained");
                Check(first.GetTexture("_MainTex") == texture && first.GetTexture("_BaseMap") == texture, "base texture retained");
                Check(first.GetTextureScale("_MainTex") == new Vector2(2, 3) && first.GetTextureOffset("_MainTex") == new Vector2(0.1f, 0.2f), "UV retained");
                Check(first.GetColor("_BaseColor") == new Color(0.8f, 0.5f, 0.25f, 1), "base color retained");
                Check(first.GetTexture("_NormalMap") == texture && Mathf.Approximately(first.GetFloat("_BumpScale"), 0.7f), "normal map retained");
                Check(first.GetTexture("_Emissive_Tex") == texture && first.GetColor("_Emissive_Color") == new Color(0.2f, 0.3f, 0.4f, 1),
                    $"emission retained (texture={first.GetTexture("_Emissive_Tex")}, color={first.GetColor("_Emissive_Color")})");
                Check(first.GetFloat("_Outline_Width") > 0 && first.GetShaderPassEnabled("SRPDefaultUnlit") && first.GetFloat("_RimLight") == 1, "outline and rim enabled");
                Check(first.IsKeywordEnabled("_IS_CLIPPING_OFF") && second.IsKeywordEnabled("_IS_CLIPPING_TRANSMODE"), "clipping variants set immediately");
                Check(Mathf.Approximately(second.GetFloat("_Clipping_Level"), -0.375f) && second.renderQueue == 2450 && second.GetFloat("_CullMode") == 0, "cutoff with color alpha, queue and culling retained");
                Check(AssetDatabase.AssetPathToGUID(root + "/First.mat") == guid, "asset GUID retained");
                Check(SelectedMaterialsToUTS.ConvertMaterials(selected, options, messages) == 0, "existing UTS skipped without overwriting");
                Undo.PerformUndo();
                Check(first.shader == lit && second.shader == lit, "one undo restores both materials");
                Check(first.GetTexture("_BaseMap") == texture && first.IsKeywordEnabled("_NORMALMAP"), "undo restores original texture and keywords");
                Undo.PerformRedo();
                Check(first.shader.name == "Toon/Toon" && second.shader.name == "Toon/Toon", "redo restores conversion");
                options.outline = false;
                options.rimLight = false;
                var third = new Material(lit);
                AssetDatabase.CreateAsset(third, root + "/Third.mat");
                Check(SelectedMaterialsToUTS.ConvertMaterials(new[] { third }, options, messages) == 1, "single conversion");
                Check(third.GetFloat("_Outline_Width") == 0 && !third.GetShaderPassEnabled("SRPDefaultUnlit") && third.GetFloat("_RimLight") == 0, "effects can be disabled");
                var transparent = new Material(lit);
                transparent.SetFloat("_Surface", 1);
                AssetDatabase.CreateAsset(transparent, root + "/Transparent.mat");
                Check(SelectedMaterialsToUTS.GetSkipReason(transparent) != null, "unsupported transparency is rejected");
                var custom = new Material(Shader.Find("Hidden/InternalErrorShader"));
                AssetDatabase.CreateAsset(custom, root + "/Custom.mat");
                Check(SelectedMaterialsToUTS.GetSkipReason(custom) != null, "custom shader is rejected");
                outside = new Material(lit);
                Check(SelectedMaterialsToUTS.GetSkipReason(outside) != null, "nonasset material rejected");
                var outsidePath = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !p.StartsWith("Assets/Member/Sawada/", StringComparison.Ordinal));
                if (outsidePath != null)
                    Check(SelectedMaterialsToUTS.GetSkipReason(AssetDatabase.LoadAssetAtPath<Material>(outsidePath)) != null, "asset outside allowed folder rejected");
                Check(SelectedMaterialsToUTS.ConvertMaterials(new[] { transparent, third, custom }, options, messages) == 0, "mixed invalid selection has no changes");
                foreach (string sourceShader in new[] { "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit", "Standard", "Standard (Specular setup)" })
                {
                    var supported = new Material(Shader.Find(sourceShader));
                    bool isUrp = sourceShader.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
                    string sourceMap = isUrp ? "_BaseMap" : "_MainTex";
                    supported.SetTexture(sourceMap, texture);
                    string supportedPath = root + "/Supported" + Guid.NewGuid().ToString("N") + ".mat";
                    AssetDatabase.CreateAsset(supported, supportedPath);
                    Check(SelectedMaterialsToUTS.ConvertMaterials(new[] { supported }, options, messages) == 1 &&
                        supported.GetTexture("_MainTex") == texture, sourceShader + " conversion");
                }
                Debug.Log("[SelectedMaterialsToUTSChecks] PASS: single/multiple selection, conversion, textures/colors/UV/normal/emission, clipping, effects, GUID, undo/redo, scope and skip checks.");
            }
            finally
            {
                Selection.objects = previousSelection;
                if (outside != null) Object.DestroyImmediate(outside);
                if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root);
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("[SelectedMaterialsToUTSChecks] FAIL: " + message);
        }
    }
}
