using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class LiuMatchaStarReveal : BaseMeshEffect
{
    [Tooltip("次の星を表示するまでの秒数")]
    [SerializeField, Min(0.01f)] private float m_starInterval = 0.3f;
    private int m_visibleStars;

    protected override void OnEnable()
    {
        m_visibleStars = 1;
        base.OnEnable();
        if (!Application.isPlaying) return;

        int starCount = GetComponent<Text>().text.Length;
        if (starCount > 1)
            StartCoroutine(Reveal(starCount));
    }

    private IEnumerator Reveal(int starCount)
    {
        while (m_visibleStars < starCount)
        {
            yield return new WaitForSecondsRealtime(m_starInterval);
            m_visibleStars++;
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || !Application.isPlaying) return;

        // 星の位置を固定したまま、これから表示する文字だけ透明にする。
        UIVertex vertex = default;
        for (int i = m_visibleStars * 4; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.color.a = 0;
            vertices.SetUIVertex(vertex, i);
        }
    }

    protected override void OnDisable()
    {
        StopAllCoroutines();
        base.OnDisable();
    }
}
