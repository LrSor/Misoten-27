using UnityEngine;

//==================================================
// パーティクルのキラキラ（08_Sprite1 の星を 4 コマでアニメーションさせる）
//
// ・Particle System と一緒に付ける
// ・Intensity（0～1）で、1 秒に出る星の数を変える。0 なら新しい星は出ない
//   （SHAKE_EFFECTS が振り方に合わせて自動で変える）
// ・Burst(数) で、一度にまとめて出せる（クリアしたときなど）
// ・光り方：明るさを 1 より大きくすると、ブルーム（光のにじみ）がかかって光って見える
//     ふだん：「ふだんの明るさ」
//     一番速い段階のとき：IsFast = true で「速いときに足す明るさ」が足される
//     Flash(強さ)：パッと明るくなって、だんだん元に戻る（まっすぐ振れたとき・ゴールなど）
//   ※ ブルームは、ミニゲームのカメラと BloomVolume（作成メニューで作る）があるときにかかる
//
// ・ゲームを再生していないときは、選ぶだけでシーンビューに「一番強いとき」の見え方が出る（プレビュー）
//   ゲームを再生すると、振り方に合わせた強さに切りかわる（振っていなければ出ない）
// ・Particle System の中身（大きさ・色・寿命・出る範囲など）は、Inspector でそのまま調整してよい
// ・最初の作成は、メニュー「Suzuki / キラキラ（パーティクル）を作成」で自動で行う
//==================================================
[RequireComponent(typeof(ParticleSystem))]
public class SPARKLE_PARTICLE : MonoBehaviour
{
    [Header("強さ（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_intensity = 0f;

    [Tooltip("一番強いとき、1 秒に出る星の数")]
    [SerializeField]
    private float m_maxRate = 25f;

    [Header("光り方（1 より大きいとブルームで光がにじむ）")]
    [Tooltip("星の色（画像の色にかける）")]
    [SerializeField]
    private Color m_color = Color.white;

    [Tooltip("ふだんの明るさ")]
    [SerializeField]
    private float m_glow = 1.2f;

    [Tooltip("一番速い段階のときに足す明るさ")]
    [SerializeField]
    private float m_fastGlow = 0.8f;

    [Tooltip("パッと光ったときに足す明るさ（Flash(1) のとき）")]
    [SerializeField]
    private float m_flashGlow = 2.5f;

    [Tooltip("パッと光ったあと、元に戻る速さ（大きいほどすぐ戻る）")]
    [SerializeField]
    private float m_flashFadeSpeed = 4f;

    // URP のパーティクル用シェーダーの色
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

    private ParticleSystem m_particle;
    private ParticleSystemRenderer m_renderer;
    private MaterialPropertyBlock m_block;
    private float m_flash;
    private bool m_isFast;
    private float m_fastBlend;

    // 一番速い段階のとき true（明るくなる）
    public bool IsFast
    {
        get { return m_isFast; }
        set { m_isFast = value; }
    }

    // パッと光らせる（強さ 1 で「パッと光ったときに足す明るさ」いっぱい）
    public void Flash(float strength = 1f)
    {
        m_flash = Mathf.Max(m_flash, Mathf.Clamp01(strength));
    }

    public float Intensity
    {
        get { return m_intensity; }
        set
        {
            m_intensity = Mathf.Clamp01(value);
            Apply();
        }
    }

    void Awake()
    {
        m_particle = GetComponent<ParticleSystem>();
        m_renderer = GetComponent<ParticleSystemRenderer>();
        m_block = new MaterialPropertyBlock();
        Apply();
        ApplyGlow();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        m_flash = Mathf.MoveTowards(m_flash, 0f, deltaTime * m_flashFadeSpeed);
        m_fastBlend = Mathf.MoveTowards(m_fastBlend, m_isFast ? 1f : 0f, deltaTime * 4f);

        ApplyGlow();
    }

    // 明るさを色に掛けて、パーティクルの見た目に渡す
    void ApplyGlow()
    {
        if (m_renderer == null)
        {
            return;
        }

        float glow = m_glow + m_fastGlow * m_fastBlend + m_flashGlow * m_flash;

        Color color = m_color * glow;
        color.a = m_color.a;

        m_renderer.GetPropertyBlock(m_block);
        m_block.SetColor(BASE_COLOR_ID, color);
        m_renderer.SetPropertyBlock(m_block);
    }

    void OnValidate()
    {
        m_maxRate = Mathf.Max(0f, m_maxRate);

        if (Application.isPlaying)
        {
            Apply();
            return;
        }

        // 再生していないとき：一番強いときの量で出して、シーンビューで見えるようにする（プレビュー用）
        ParticleSystem particle = GetComponent<ParticleSystem>();

        if (particle != null)
        {
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = m_maxRate;
        }
    }

    // まとめて出す
    public void Burst(int count)
    {
        if (m_particle == null)
        {
            return;
        }

        if (!m_particle.isPlaying)
        {
            m_particle.Play();
        }

        m_particle.Emit(Mathf.Max(0, count));
    }

    // 出ている星を全部消す
    public void ClearAll()
    {
        if (m_particle != null)
        {
            m_particle.Clear();
        }
    }

    void Apply()
    {
        if (m_particle == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = m_particle.emission;
        emission.rateOverTime = m_maxRate * m_intensity;

        if (m_intensity > 0f && !m_particle.isPlaying)
        {
            m_particle.Play();
        }
    }
}
