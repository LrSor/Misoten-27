using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class LiuMatchaSpeedLines : MaskableGraphic
{
    [Tooltip("最大速度で表示する集中線の本数")]
    [SerializeField, Range(1, 64)] private int m_maxLines = 24;
    [Tooltip("集中線が最大本数になる回転速度（周／秒）")]
    [SerializeField, Min(0.01f)] private float m_fullSpeed = 2f;
    [Tooltip("最大速度のとき、各線が1秒間に流れる回数")]
    [SerializeField, Min(0.1f)] private float m_cyclesAtFullSpeed = 2f;

    private LiuMatchaGame m_game;
    private LiuMatchaSpeedJudge m_speedJudge;
    private int m_lineCount;
    private float m_phase;

    protected override void Awake()
    {
        base.Awake();
        if (!Application.isPlaying)
            return;

        m_game = GetComponentInParent<LiuMatchaGame>();
        if (m_game != null)
            m_speedJudge = m_game.GetComponentInChildren<LiuMatchaSpeedJudge>(true);
        if (m_speedJudge == null || transform.parent == null ||
            transform.parent.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning("抹茶ミニゲームの Canvas 直下に空の子を作り、そこに取り付けてください。", this);
            enabled = false;
            return;
        }

        // プレイヤーごとの Canvas に合わせ、文字の後ろに描く。
        raycastTarget = false;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.SetAsFirstSibling();
    }

    private void LateUpdate()
    {
        if (m_speedJudge == null)
            return;

        float speedRatio = 0f;
        if (m_speedJudge.isActiveAndEnabled && !m_game.GetIsFinish())
            speedRatio = Mathf.Clamp01(m_speedJudge.CurrentSpeed / m_fullSpeed);

        int count = Mathf.RoundToInt(m_maxLines * speedRatio);
        if (count == 0)
        {
            if (m_lineCount == 0) return;
            m_phase = 0f;
        }
        else
        {
            float cyclesPerSecond = m_cyclesAtFullSpeed * Mathf.Lerp(0.35f, 1f, speedRatio);
            m_phase = Mathf.Repeat(m_phase + cyclesPerSecond * Time.deltaTime, 1f);
        }

        m_lineCount = count;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect area = GetPixelAdjustedRect();
        if (m_lineCount == 0 || area.width <= 0f || area.height <= 0f)
            return;

        float shortSide = Mathf.Min(area.width, area.height);

        for (int i = 0; i < m_lineCount; i++)
        {
            // 黄金角で追加し、線が数本しかない段階でも上下左右に分散させる。
            float angle = (21f + i * 137.50776f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 border = direction / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            Vector2 edge = area.center + Vector2.Scale(area.size * 0.5f, border);
            Vector2 tangent = Mathf.Abs(border.x) > Mathf.Abs(border.y)
                ? new Vector2(0f, Mathf.Sign(border.x))
                : new Vector2(-Mathf.Sign(border.y), 0f);

            float variety = Mathf.Repeat(0.41f + i * 0.381966f, 1f);
            // 出発のタイミングをずらし、内側で消えてから外縁に戻る。
            float progress = Mathf.Repeat(m_phase + 0.13f + i * 0.754878f, 1f);
            float travel = 0.20f * progress;
            Vector2 tail = Vector2.Lerp(edge, area.center, travel);
            Vector2 tip = Vector2.Lerp(tail, area.center, Mathf.Lerp(0.16f, 0.26f, variety));
            float halfWidth = shortSide * Mathf.Lerp(0.002f, 0.005f, variety) * (1f - travel);

            float fadeIn = Mathf.SmoothStep(0f, 1f, progress / 0.12f);
            float fadeOut = Mathf.SmoothStep(0f, 1f, (1f - progress) / 0.22f);
            Color edgeColor = color;
            edgeColor.a *= 0.88f * fadeIn * fadeOut;
            Color tipColor = edgeColor;
            tipColor.a *= 0.35f;
            int first = mesh.currentVertCount;
            mesh.AddVert(tail + tangent * halfWidth, edgeColor, Vector2.zero);
            mesh.AddVert(tail - tangent * halfWidth, edgeColor, Vector2.zero);
            mesh.AddVert(tip, tipColor, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }
    }
}
