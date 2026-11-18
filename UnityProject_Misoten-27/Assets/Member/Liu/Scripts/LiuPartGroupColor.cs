using UnityEngine;

public class LiuPartGroupColor : MonoBehaviour
{
    private void Start()
    {
        // 運動データと描画コンポーネントを取得する。
        Part part = GetComponent<Part>();
        Renderer partRenderer = GetComponentInChildren<Renderer>();

        if (part == null || part.Data == null || partRenderer == null)
        {
            Debug.LogWarning(
                "[LiuPartGroupColor] PartとRendererを確認してください。",
                this);
            return;
        }

        // クエストと同じグループカラーを使用する。
        Color groupColor =
            LiuQuestGroupColor.GetGroupColor(part.Data.group);

        // このオブジェクトの色だけを設定する。
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        partRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", groupColor);
        partRenderer.SetPropertyBlock(properties);
    }
}
