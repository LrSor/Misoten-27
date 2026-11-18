using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//==================================================
// キラキラ（画面に星がまたたく）
//
// ・Canvas の中に置いて、画面いっぱいの大きさにする（アンカーを四隅に広げる）
// ・Intensity（0～1）で強さを変える。0 なら新しい星は出ない
// ・「中心にする物」を入れると、その物のまわりに星が出る（例：走っている配達員）
//   空欄なら画面全体に出る
// ・画像は使わずに星を描いているので、素材がなくても使える
//==================================================
[RequireComponent(typeof(CanvasRenderer))]
public class SPARKLE_EFFECT : MaskableGraphic
{
    [Header("強さ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_intensity = 0f;

    [Header("星")]
    [Tooltip("一番強いとき、1 秒に出る星の数")]
    [SerializeField]
    private float m_maxSpawnPerSecond = 30f;

    [Tooltip("星の大きさ（px）")]
    [SerializeField]
    private Vector2 m_sizeRange = new Vector2(20f, 60f);

    [Tooltip("星が光っている時間（秒）")]
    [SerializeField]
    private Vector2 m_lifeRange = new Vector2(0.3f, 0.7f);

    [Tooltip("星の細さ（小さいほど細い）")]
    [Range(0.05f, 0.5f)]
    [SerializeField]
    private float m_thickness = 0.18f;

    [Header("出る場所")]
    [Tooltip("この物のまわりに出す（空欄なら画面全体）")]
    [SerializeField]
    private Transform m_center;

    [Tooltip("中心にする物を写しているカメラ")]
    [SerializeField]
    private Camera m_camera;

    [Tooltip("中心からどれくらい離れて出るか（px）")]
    [SerializeField]
    private float m_spawnRadius = 250f;

    [Tooltip("中心にする物の、どの高さを中心にするか（m）")]
    [SerializeField]
    private float m_centerHeight = 1f;

    private class STAR
    {
        public Vector2 m_position;
        public float m_size;
        public float m_life;
        public float m_maxLife;
        public float m_rotation;
    }

    private readonly List<STAR> m_stars = new List<STAR>();
    private float m_spawnStock;

    public float Intensity
    {
        get { return m_intensity; }
        set { m_intensity = Mathf.Clamp01(value); }
    }

    public Transform Center
    {
        get { return m_center; }
        set { m_center = value; }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnDisable()
    {
        m_stars.Clear();
        m_spawnStock = 0f;
        base.OnDisable();
    }

    void Update()
    {
        if (m_stars.Count == 0 && m_intensity <= 0f)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        // 新しい星を出す
        m_spawnStock += m_maxSpawnPerSecond * m_intensity * deltaTime;

        while (m_spawnStock >= 1f)
        {
            m_spawnStock -= 1f;
            Spawn();
        }

        // 古い星を消す
        for (int i = m_stars.Count - 1; i >= 0; i--)
        {
            m_stars[i].m_life += deltaTime;

            if (m_stars[i].m_life >= m_stars[i].m_maxLife)
            {
                m_stars.RemoveAt(i);
            }
        }

        SetVerticesDirty();
    }

    void Spawn()
    {
        Rect rect = rectTransform.rect;
        Vector2 position;

        if (m_center != null &&
            m_camera != null &&
            TryGetCenter(out Vector2 center))
        {
            position = center + Random.insideUnitCircle * m_spawnRadius;
        }
        else
        {
            position = new Vector2(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(rect.yMin, rect.yMax));
        }

        STAR star = new STAR();
        star.m_position = position;
        star.m_size = Random.Range(m_sizeRange.x, m_sizeRange.y);
        star.m_maxLife = Mathf.Max(0.05f, Random.Range(m_lifeRange.x, m_lifeRange.y));
        star.m_life = 0f;
        star.m_rotation = Random.Range(-20f, 20f) * Mathf.Deg2Rad;

        m_stars.Add(star);
    }

    // 中心にする物が、この UI の上でどこに見えるか
    bool TryGetCenter(out Vector2 local)
    {
        local = Vector2.zero;

        Vector3 screen = m_camera.WorldToScreenPoint(m_center.position + Vector3.up * m_centerHeight);

        if (screen.z < 0f)
        {
            return false;
        }

        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Camera uiCamera =
            (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screen, uiCamera, out local);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        foreach (STAR star in m_stars)
        {
            // 出てすぐ大きくなって、だんだん小さくなる
            float t = star.m_life / star.m_maxLife;
            float scale = Mathf.Sin(t * Mathf.PI);
            float size = star.m_size * scale * 0.5f;

            if (size <= 0.01f)
            {
                continue;
            }

            Color32 starColor = color;
            starColor.a = (byte)(starColor.a * Mathf.Clamp01(scale * 1.5f));

            // 縦長のひし形と横長のひし形を重ねて、4 つのとがった星にする
            AddDiamond(vh, star.m_position, size, size * m_thickness, star.m_rotation, starColor);
            AddDiamond(vh, star.m_position, size * m_thickness, size, star.m_rotation, starColor);
        }
    }

    static void AddDiamond(VertexHelper vh, Vector2 center, float halfWidth, float halfHeight,
                           float rotation, Color32 starColor)
    {
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);

        Vector2 right = new Vector2(cos, sin) * halfWidth;
        Vector2 up = new Vector2(-sin, cos) * halfHeight;

        int index = vh.currentVertCount;

        vh.AddVert(center + up, starColor, Vector4.zero);
        vh.AddVert(center + right, starColor, Vector4.zero);
        vh.AddVert(center - up, starColor, Vector4.zero);
        vh.AddVert(center - right, starColor, Vector4.zero);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
