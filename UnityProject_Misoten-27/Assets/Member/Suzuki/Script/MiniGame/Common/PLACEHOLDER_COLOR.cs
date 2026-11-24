using UnityEngine;

//==================================================
// 仮の見た目に色を付ける（本番のモデルができるまでの仮）
//
// ・付けたオブジェクトと、その子の見た目（Renderer）すべてに色を付ける
// ・マテリアル自体は変えないので、ほかの物には影響しない
// ・本番のモデルに差し替えたら、このコンポーネントは外してよい
//==================================================
public class PLACEHOLDER_COLOR : MonoBehaviour
{
    [SerializeField]
    private Color m_color = Color.white;

    // URP の Lit / Unlit シェーダーの色、と古いシェーダーの色
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    public Color Color
    {
        get { return m_color; }
        set
        {
            m_color = value;
            Apply();
        }
    }

    void Awake()
    {
        Apply();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            Apply();
        }
    }

    public void Apply()
    {
        SetColor(gameObject, m_color);
    }

    // ほかのスクリプトからも使える
    public static void SetColor(GameObject target, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BASE_COLOR_ID, color);
            block.SetColor(COLOR_ID, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
