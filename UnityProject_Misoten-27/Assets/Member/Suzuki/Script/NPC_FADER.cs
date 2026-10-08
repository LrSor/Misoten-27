using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//==================================================
// NPC の見た目の透明度を変えるコンポーネント
// 自分と子オブジェクト（腕など）のすべての Renderer が対象
//
// URP の Lit / Unlit マテリアル（_BaseColor を持つもの）に対応。
// 不透明マテリアルでも、初めて薄くするときに自動で半透明設定に切り替える。
// 元のマテリアルアセットは書き換えず、この NPC 専用のコピーを使う。
//==================================================
public class NPC_FADER : MonoBehaviour
{
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    [Tooltip("この透明度より薄くなったら影を消す")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_shadowOffAlpha = 0.5f;

    private Renderer[] m_renderers;
    private ShadowCastingMode[] m_defaultShadowModes;

    // 透明度を変えるマテリアルと、その元の色
    private readonly List<Material> m_materials = new List<Material>();
    private readonly List<int> m_colorIds = new List<int>();
    private readonly List<Color> m_defaultColors = new List<Color>();

    private bool m_isTransparent;

    private float m_alpha = 1f;

    // 現在の透明度（1 = 不透明, 0 = 完全に透明）
    public float Alpha
    {
        get { return m_alpha; }
    }

    void Awake()
    {
        m_renderers =
            GetComponentsInChildren<Renderer>();

        m_defaultShadowModes =
            new ShadowCastingMode[m_renderers.Length];

        for (int i = 0; i < m_renderers.Length; i++)
        {
            m_defaultShadowModes[i] =
                m_renderers[i].shadowCastingMode;
        }
    }

    void OnDestroy()
    {
        // renderer.materials で作られたコピーを片付ける
        foreach (Material material in m_materials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }

    //==================================================
    // 透明度を設定する（0 ～ 1）
    //==================================================

    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (Mathf.Approximately(alpha, m_alpha))
        {
            return;
        }

        m_alpha = alpha;

        if (!m_isTransparent)
        {
            SetupTransparentMaterials();
        }

        for (int i = 0; i < m_materials.Count; i++)
        {
            Color color = m_defaultColors[i];

            // 元から半透明だった場合も考えて掛け算にする
            color.a *= alpha;

            m_materials[i].SetColor(
                m_colorIds[i],
                color);
        }

        // 薄くなったら影を消す（透明なのに影だけ残るのを防ぐ）
        bool castShadow =
            alpha >= m_shadowOffAlpha;

        for (int i = 0; i < m_renderers.Length; i++)
        {
            if (m_renderers[i] == null)
            {
                continue;
            }

            m_renderers[i].shadowCastingMode =
                castShadow
                    ? m_defaultShadowModes[i]
                    : ShadowCastingMode.Off;
        }
    }

    //==================================================
    // マテリアルを半透明用に切り替える（最初の1回だけ）
    //==================================================

    void SetupTransparentMaterials()
    {
        m_isTransparent = true;

        foreach (Renderer targetRenderer in m_renderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            // .materials を使うと、この Renderer 専用のコピーが作られる
            // （元のマテリアルアセットや他の NPC には影響しない）
            foreach (Material material in targetRenderer.materials)
            {
                int colorId;

                if (material.HasProperty(BASE_COLOR_ID))
                {
                    colorId = BASE_COLOR_ID;
                }
                else if (material.HasProperty(COLOR_ID))
                {
                    colorId = COLOR_ID;
                }
                else
                {
                    // 色を持たないシェーダーは対象外
                    continue;
                }

                MakeTransparent(material);

                m_materials.Add(material);
                m_colorIds.Add(colorId);
                m_defaultColors.Add(material.GetColor(colorId));
            }
        }
    }

    // URP Lit / Unlit の Surface Type を Transparent にするのと同じ設定
    static void MakeTransparent(Material material)
    {
        SetFloatIfExists(material, "_Surface", 1f);   // 1 = Transparent
        SetFloatIfExists(material, "_Blend", 0f);     // 0 = Alpha

        SetFloatIfExists(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfExists(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfExists(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfExists(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfExists(material, "_ZWrite", 0f);

        material.SetOverrideTag("RenderType", "Transparent");

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.renderQueue =
            (int)RenderQueue.Transparent;

        // 半透明は深度を書かないので DepthOnly パスも止める
        material.SetShaderPassEnabled("DepthOnly", false);
    }

    static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
