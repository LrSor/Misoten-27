using UnityEngine;
using UnityEngine.UI;

//==================================================
// 集中線（画面のふちから中心に向かう線）
//
// ・Canvas の中に置いて、画面いっぱいの大きさにする（アンカーを四隅に広げる）
// ・Intensity（0～1）で強さを変える。0 なら何も出ない
//     強いほど線が多く・太く・中心近くまで伸びる
// ・画像は使わずに線を描いているので、素材がなくても使える
//   （本番の画像ができたら、Image に差し替えてもよい）
// ・色は Inspector の Color で変えられる（透明度も効く）
//==================================================
[RequireComponent(typeof(CanvasRenderer))]
public class SPEED_LINES : MaskableGraphic
{
    [Header("強さ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_intensity = 0f;

    [Tooltip("強さを変えたとき、なめらかに追いかける速さ（0 ですぐ変わる）")]
    [SerializeField]
    private float m_smoothness = 8f;

    [Header("線")]
    [Tooltip("一番強いときの線の数")]
    [SerializeField]
    private int m_maxLines = 70;

    [Tooltip("画面のふちでの線の太さ（px）")]
    [SerializeField]
    private float m_lineWidth = 28f;

    [Tooltip("弱いとき、線の先がどこまで来るか（中心 0 ～ 画面のふち 1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_weakReach = 0.85f;

    [Tooltip("強いとき、線の先がどこまで来るか（中心 0 ～ 画面のふち 1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_strongReach = 0.45f;

    [Tooltip("線をかき直す間隔（秒）。小さいほどチカチカ動く")]
    [SerializeField]
    private float m_refreshInterval = 0.05f;

    private float m_currentIntensity;
    private float m_timer;
    private int m_seed;

    public float Intensity
    {
        get { return m_intensity; }
        set { m_intensity = Mathf.Clamp01(value); }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        m_currentIntensity = m_intensity;
        SetVerticesDirty();
    }

    void Update()
    {
        float before = m_currentIntensity;

        if (m_smoothness <= 0f)
        {
            m_currentIntensity = m_intensity;
        }
        else
        {
            m_currentIntensity = Mathf.Lerp(
                m_currentIntensity, m_intensity, 1f - Mathf.Exp(-Time.deltaTime * m_smoothness));

            if (Mathf.Abs(m_currentIntensity - m_intensity) < 0.001f)
            {
                m_currentIntensity = m_intensity;
            }
        }

        if (m_currentIntensity <= 0.001f)
        {
            if (before > 0.001f)
            {
                SetVerticesDirty();
            }

            return;
        }

        m_timer -= Time.deltaTime;

        if (m_timer <= 0f)
        {
            m_timer = m_refreshInterval;
            m_seed++;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (m_currentIntensity <= 0.001f)
        {
            return;
        }

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = rect.size.magnitude * 0.5f;

        System.Random random = new System.Random(m_seed);

        int count = Mathf.Max(1, Mathf.RoundToInt(m_maxLines * m_currentIntensity));
        float reach = Mathf.Lerp(m_weakReach, m_strongReach, m_currentIntensity);

        Color32 lineColor = color;
        lineColor.a = (byte)(lineColor.a * Mathf.Lerp(0.35f, 1f, m_currentIntensity));

        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
            float width = m_lineWidth * Mathf.Lerp(0.3f, 1f, (float)random.NextDouble()) *
                          Mathf.Lerp(0.6f, 1.2f, m_currentIntensity);
            float inner = radius * reach * Mathf.Lerp(0.85f, 1.25f, (float)random.NextDouble());

            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * 0.5f);

            Vector2 tip = center + direction * inner;
            Vector2 outer = center + direction * (radius * 1.05f);

            int index = vh.currentVertCount;

            vh.AddVert(tip, lineColor, Vector4.zero);
            vh.AddVert(outer + side, lineColor, Vector4.zero);
            vh.AddVert(outer - side, lineColor, Vector4.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
