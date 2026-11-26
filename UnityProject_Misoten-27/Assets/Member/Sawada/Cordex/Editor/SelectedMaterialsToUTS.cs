using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sawada.Cordex.EditorTools
{
    public sealed class SelectedMaterialsToUTS : EditorWindow
    {
        [Serializable]
        public sealed class Settings
        {
            public float firstShade = 0.7f;
            public float secondShade = 0.45f;
            public float shadeStep = 0.5f;
            public float feather = 0.001f;
            public bool outline = true;
            public float outlineWidth = 1f;
            public Color outlineColor = new Color(0.08f, 0.07f, 0.09f, 1f);
            public bool rimLight = true;
            public Color rimColor = new Color(0.5f, 0.55f, 0.65f, 1f);
            public float rimPower = 0.1f;
        }

        const string ShaderName = "Toon/Toon";
        const string AllowedRoot = "Assets/Member/Sawada/";
        const string UndoLabel = "選択マテリアルをUTS化";
        [SerializeField] Settings settings = new Settings();
        Vector2 scroll;
        string result;

        [MenuItem("Tools/Sawada/選択マテリアルをUTS化")]
        [MenuItem("Assets/Sawada/選択マテリアルをUTS化", false, 2000)]
        public static void Open()
        {
            GetWindow<SelectedMaterialsToUTS>("選択マテリアルをUTS化").minSize = new Vector2(470, 500);
        }

        void OnSelectionChange() => Repaint();

        public static Material[] GetSelectedMaterials()
        {
            // Selection.objects deliberately does not traverse folders, renderers or prefabs.
            return Selection.objects.OfType<Material>().Distinct()
                .OrderBy(AssetDatabase.GetAssetPath).ToArray();
        }

        public static string GetSkipReason(Material material)
        {
            if (material == null) return "マテリアルではありません";
            string path = AssetDatabase.GetAssetPath(material);
            if (!path.StartsWith(AllowedRoot, StringComparison.Ordinal))
                return "編集範囲外（Assets/Member/Sawada/ 配下のみ）";
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsMainAsset(material))
                return "モデルに埋め込まれたマテリアルです。先に .mat として複製してください";
            if (material.isVariant) return "Material Variantは対象外です。通常のマテリアルを使用してください";
            if (!AssetDatabase.IsOpenForEdit(material)) return "書き込みできません";
            if (material.shader == null) return "シェーダーが見つかりません";
            string shader = material.shader.name;
            if (shader.StartsWith("Toon/", StringComparison.Ordinal)) return "既にUTSです（設定を維持）";
            bool urp = shader == "Universal Render Pipeline/Lit" ||
                       shader == "Universal Render Pipeline/Simple Lit" ||
                       shader == "Universal Render Pipeline/Unlit";
            bool standard = shader == "Standard" || shader == "Standard (Specular setup)";
            if (!urp && !standard) return "未対応シェーダー: " + shader;
            if ((urp && material.GetFloat("_Surface") > 0.5f) ||
                (standard && material.GetFloat("_Mode") >= 2))
                return "半透明は対象外です（不透明・アルファクリップのみ対応）";
            return null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("事前に複製した .mat をProjectビューで1つ以上選択してください。" +
                "選択中のマテリアル自体を変更・保存します。Ctrl+Zで戻せます。\n" +
                "対応: URP Lit / Simple Lit / Unlit、Standard。不透明・アルファクリップのみ。\n" +
                "テクスチャ・色・UV・有効な法線マップと発光を引き継ぎます。金属・滑らかさ・AOは引き継ぎません。", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("セルシェーディング", EditorStyles.boldLabel);
            settings.firstShade = EditorGUILayout.Slider("1段目の影の明るさ", settings.firstShade, 0, 1);
            settings.secondShade = EditorGUILayout.Slider("2段目の影の明るさ", settings.secondShade, 0, 1);
            settings.shadeStep = EditorGUILayout.Slider("影の境界", settings.shadeStep, 0, 1);
            settings.feather = EditorGUILayout.Slider("影のぼかし", settings.feather, 0.0001f, 1);
            settings.outline = EditorGUILayout.Toggle("アウトライン", settings.outline);
            using (new EditorGUI.DisabledScope(!settings.outline))
            {
                settings.outlineWidth = Mathf.Max(0, EditorGUILayout.FloatField("線の太さ（モデルのスケール依存）", settings.outlineWidth));
                settings.outlineColor = EditorGUILayout.ColorField("線の色", settings.outlineColor);
            }
            settings.rimLight = EditorGUILayout.Toggle("リムライト", settings.rimLight);
            using (new EditorGUI.DisabledScope(!settings.rimLight))
            {
                settings.rimColor = EditorGUILayout.ColorField("リムの色・明るさ", settings.rimColor);
                settings.rimPower = EditorGUILayout.Slider("リムの広がり", settings.rimPower, 0, 1);
            }
            EditorGUILayout.Space();
            var materials = GetSelectedMaterials();
            int ready = materials.Count(m => GetSkipReason(m) == null);
            EditorGUILayout.LabelField($"選択: {materials.Length} / 変換可能: {ready}", EditorStyles.boldLabel);
            foreach (var material in materials)
            {
                EditorGUILayout.ObjectField(material, typeof(Material), false);
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(material), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(GetSkipReason(material) ?? "変換対象", EditorStyles.wordWrappedMiniLabel);
            }
            if (materials.Length == 0) EditorGUILayout.LabelField("Projectビューで .mat を選択してください。");
            EditorGUILayout.EndScrollView();
            bool hasShader = Shader.Find(ShaderName) != null;
            if (!hasShader) EditorGUILayout.HelpBox("UTS（Toon/Toon）が見つかりません。パッケージの導入状態を確認してください。", MessageType.Error);
            using (new EditorGUI.DisabledScope(ready == 0 || !hasShader || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button($"選択した {ready} 個のマテリアルをUTS化", GUILayout.Height(32)))
                {
                    var messages = new List<string>();
                    int count = ConvertMaterials(materials, settings, messages);
                    result = $"{count} 個をUTS化しました。スキップ: {materials.Length - count} 個。";
                    Debug.Log("[SelectedMaterialsToUTS] " + result + "\n" + string.Join("\n", messages));
                }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }

        public static int ConvertMaterials(IEnumerable<Material> selection, Settings options, List<string> messages)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("UTSのToon/Toonシェーダーが見つかりません。");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);
            int converted = 0;
            try
            {
                foreach (var material in selection.Where(m => m != null).Distinct())
                {
                    string reason = GetSkipReason(material);
                    if (reason != null)
                    {
                        messages?.Add(AssetDatabase.GetAssetPath(material) + ": " + reason);
                        continue;
                    }
                    var replacement = BuildConvertedMaterial(material, shader, options);
                    try
                    {
                        Undo.RegisterCompleteObjectUndo(material, UndoLabel);
                        EditorUtility.CopySerialized(replacement, material);
                        EditorUtility.SetDirty(material);
                        AssetDatabase.SaveAssetIfDirty(material);
                        converted++;
                        messages?.Add(AssetDatabase.GetAssetPath(material) + ": 変換完了");
                    }
                    finally { DestroyImmediate(replacement); }
                }
            }
            finally { Undo.CollapseUndoOperations(group); }
            SceneView.RepaintAll();
            return converted;
        }

        static Material BuildConvertedMaterial(Material source, Shader shader, Settings options)
        {
            bool urp = source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
            string map = urp ? "_BaseMap" : "_MainTex";
            Color color = source.GetColor(urp ? "_BaseColor" : "_Color");
            Texture texture = source.GetTexture(map);
            Vector2 scale = source.GetTextureScale(map);
            Vector2 offset = source.GetTextureOffset(map);
            bool clipping = urp ? source.GetFloat("_AlphaClip") > 0.5f : source.GetFloat("_Mode") == 1;
            var target = new Material(shader)
            {
                name = source.name,
                enableInstancing = source.enableInstancing,
                doubleSidedGI = source.doubleSidedGI,
                globalIlluminationFlags = source.globalIlluminationFlags
            };
            foreach (string property in new[] { "_MainTex", "_BaseMap", "_1st_ShadeMap", "_2nd_ShadeMap" })
            {
                target.SetTexture(property, texture);
                target.SetTextureScale(property, scale);
                target.SetTextureOffset(property, offset);
            }
            target.SetColor("_BaseColor", color);
            target.SetColor("_Color", color);
            target.SetColor("_1st_ShadeColor", Shade(color, options.firstShade));
            target.SetColor("_2nd_ShadeColor", Shade(color, options.secondShade));
            target.SetFloat("_Use_BaseAs1st", 1);
            target.SetFloat("_Use_1stAs2nd", 1);
            target.SetFloat("_BaseColor_Step", options.shadeStep);
            target.SetFloat("_ShadeColor_Step", Mathf.Max(0, options.shadeStep - 0.25f));
            target.SetFloat("_BaseShade_Feather", options.feather);
            target.SetFloat("_1st2nd_Shades_Feather", options.feather);
            target.SetFloat("_Outline_Width", options.outline ? options.outlineWidth : 0);
            target.SetColor("_Outline_Color", options.outlineColor);
            target.SetFloat("_Is_LightColor_Outline", 0);
            target.SetShaderPassEnabled("SRPDefaultUnlit", options.outline);
            target.EnableKeyword("_OUTLINE_NML");
            target.SetFloat("_RimLight", options.rimLight ? 1 : 0);
            target.SetColor("_RimLightColor", options.rimColor);
            target.SetFloat("_RimLight_Power", options.rimPower);
            target.SetFloat("_ClippingMode", clipping ? 2 : 0);
            target.SetFloat("_IsBaseMapAlphaAsClippingMask", clipping ? 1 : 0);
            // UTS uses texture alpha alone; URP/Standard multiply it by the base color alpha.
            float cutoff = clipping ? source.GetFloat("_Cutoff") : 0;
            float textureCutoff = color.a > 0 ? cutoff / color.a : (cutoff > 0 ? 2 : 0);
            target.SetFloat("_Clipping_Level", clipping ? 0.5f - textureCutoff : 0);
            target.EnableKeyword(clipping ? "_IS_CLIPPING_TRANSMODE" : "_IS_CLIPPING_OFF");
            target.EnableKeyword(clipping ? "_IS_OUTLINE_CLIPPING_YES" : "_IS_OUTLINE_CLIPPING_NO");
            target.EnableKeyword("_EMISSIVE_SIMPLE");
            target.renderQueue = clipping ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            target.SetOverrideTag("RenderType", clipping ? "TransparentCutOut" : "Opaque");
            if (source.HasProperty("_Cull")) target.SetFloat("_CullMode", source.GetFloat("_Cull"));
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null && source.IsKeywordEnabled("_NORMALMAP"))
            {
                CopyTexture(source, "_BumpMap", target, "_NormalMap");
                target.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1);
                target.SetFloat("_Is_NormalMapToBase", 1);
            }
            if (source.HasProperty("_EmissionColor") && source.IsKeywordEnabled("_EMISSION"))
            {
                target.SetColor("_Emissive_Color", source.GetColor("_EmissionColor"));
                if (source.GetTexture("_EmissionMap") != null) CopyTexture(source, "_EmissionMap", target, "_Emissive_Tex");
            }
            return target;
        }

        static Color Shade(Color color, float multiplier) =>
            new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);

        static void CopyTexture(Material source, string sourceProperty, Material target, string targetProperty)
        {
            target.SetTexture(targetProperty, source.GetTexture(sourceProperty));
            target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
        }
    }
}
