using UnityEngine;

//==================================================
// パーティクルの集中線（05_Sprite の 2 コマを切りかえてアニメーションさせる）
//
// ・ミニゲームのカメラの子に置く。カメラの目の前に、画面いっぱいの大きさで自動で合わせる
//   （画角が変わってズームしても、画面いっぱいのまま）
// ・Intensity（0～1）で強さを変える。0 なら見えない
//     強いほど、こく・線が画面の中心近くまで来る
//   （SHAKE_EFFECTS が振り方に合わせて自動で変える）
//
// ・最初の作成は、メニュー「Suzuki / 集中線（パーティクル）を作成」で自動で行う
// ・コマの切りかわる速さは、Particle System の Texture Sheet Animation の Cycles で変えられる
//==================================================
[RequireComponent(typeof(ParticleSystem))]
public class SPEED_LINES_PARTICLE : MonoBehaviour
{
    [Header("強さ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_intensity = 0f;

    [Tooltip("強さを変えたとき、なめらかに追いかける速さ（0 ですぐ変わる）")]
    [SerializeField]
    private float m_smoothness = 8f;

    [Header("見た目")]
    [Tooltip("線の色（画像の色にかける）")]
    [SerializeField]
    private Color m_color = Color.white;

    [Tooltip("線の明るさ（画像の線は灰色なので、1.6 くらいで白になる）")]
    [SerializeField]
    private float m_brightness = 1.6f;

    [Tooltip("弱いときの、こさ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_weakAlpha = 0.35f;

    [Tooltip("強いときの、こさ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_strongAlpha = 1f;

    [Tooltip("弱いときの大きさ（画面に対する倍率。大きいほど線が外に逃げて、中心まで来ない）")]
    [SerializeField]
    private float m_weakScale = 1.4f;

    [Tooltip("強いときの大きさ（1 で画面ぴったり）")]
    [SerializeField]
    private float m_strongScale = 1f;

    [Header("画面に合わせる")]
    [Tooltip("カメラからの距離（m）。カメラの Near より少し遠くにする")]
    [SerializeField]
    private float m_distance = 0.5f;

    [Tooltip("画像 1 コマの横 ÷ 縦（作成メニューが自動で入れる）")]
    [SerializeField]
    private float m_imageAspect = 1.78f;

    // URP のパーティクル用シェーダーの色
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

    private Camera m_camera;
    private ParticleSystemRenderer m_renderer;
    private MaterialPropertyBlock m_block;
    private float m_current;

    public float Intensity
    {
        get { return m_intensity; }
        set { m_intensity = Mathf.Clamp01(value); }
    }

    void Awake()
    {
        m_camera = GetComponentInParent<Camera>(true);
        m_renderer = GetComponent<ParticleSystemRenderer>();
        m_block = new MaterialPropertyBlock();
        m_current = m_intensity;

        Apply();
    }

    void LateUpdate()
    {
        if (m_smoothness <= 0f)
        {
            m_current = m_intensity;
        }
        else
        {
            m_current = Mathf.Lerp(m_current, m_intensity, 1f - Mathf.Exp(-Time.deltaTime * m_smoothness));

            if (Mathf.Abs(m_current - m_intensity) < 0.001f)
            {
                m_current = m_intensity;
            }
        }

        Apply();
    }

    void Apply()
    {
        if (m_renderer == null)
        {
            return;
        }

        bool isVisible = m_current > 0.001f;

        if (m_renderer.enabled != isVisible)
        {
            m_renderer.enabled = isVisible;
        }

        if (!isVisible)
        {
            return;
        }

        // カメラの目の前に、画面いっぱいの大きさで置く
        if (m_camera != null)
        {
            float distance = Mathf.Max(m_camera.nearClipPlane + 0.05f, m_distance);
            float height = 2f * distance * Mathf.Tan(m_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * m_camera.aspect;

            // 画面の縦横比が画像と違っても、すき間ができないように大きい方に合わせる
            float cover = Mathf.Max(height, width / Mathf.Max(0.01f, m_imageAspect));
            float scale = cover * Mathf.Lerp(m_weakScale, m_strongScale, m_current);

            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;
        }

        // こさと明るさ
        Color color = m_color * m_brightness;
        color.a = m_color.a * Mathf.Lerp(m_weakAlpha, m_strongAlpha, m_current);

        m_renderer.GetPropertyBlock(m_block);
        m_block.SetColor(BASE_COLOR_ID, color);
        m_renderer.SetPropertyBlock(m_block);
    }

    void OnValidate()
    {
        m_distance = Mathf.Max(0.01f, m_distance);
        m_imageAspect = Mathf.Max(0.01f, m_imageAspect);
    }
}
