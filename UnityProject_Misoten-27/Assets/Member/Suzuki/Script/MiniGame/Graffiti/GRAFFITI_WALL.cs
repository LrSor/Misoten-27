using System.Collections.Generic;
using UnityEngine;

//==================================================
// 「落書きクリーンアップ」の落書きされた壁
//
// ・きれい度（0～1）に合わせて、落書きの色がだんだん壁の色に近づいて消えていく
//   （汚れの 5 段階は GRAFFITI_GAME が決めて、ここに伝える）
// ・今は仮の見た目：「落書き」の子の板（PLACEHOLDER_COLOR 付き）が落書き
//
// ■ 本番の絵ができたら
//   ・「段階ごとに切り替える物」に、汚れの段階ごとの絵（汚れがひどい順）を入れると、
//     その段階のものだけ表示される
//   ・仮の落書き（「落書き」の子）は消してよい
//==================================================
public class GRAFFITI_WALL : MonoBehaviour
{
    [Header("仮の落書き")]
    [Tooltip("仮の落書きの親（子の PLACEHOLDER_COLOR の色を変えていく）")]
    [SerializeField]
    private Transform m_graffitiRoot;

    [Tooltip("きれいになったときの色（壁の色）")]
    [SerializeField]
    private Color m_wallColor = new Color(0.62f, 0.62f, 0.6f);

    [Tooltip("色が変わるときの、なめらかさ")]
    [SerializeField]
    private float m_fadeSmoothness = 6f;

    [Header("本番の絵（汚れがひどい順。空欄でもよい）")]
    [Tooltip("段階ごとに切り替える物。きれい度に合わせて 1 つだけ表示する")]
    [SerializeField]
    private GameObject[] m_stageObjects;

    private readonly List<PLACEHOLDER_COLOR> m_pieces = new List<PLACEHOLDER_COLOR>();
    private readonly List<Color> m_startColors = new List<Color>();

    private float m_targetRate;
    private float m_rate;

    void Awake()
    {
        if (m_graffitiRoot != null)
        {
            foreach (PLACEHOLDER_COLOR piece in m_graffitiRoot.GetComponentsInChildren<PLACEHOLDER_COLOR>(true))
            {
                m_pieces.Add(piece);
                m_startColors.Add(piece.Color);
            }
        }
    }

    // きれい度（0 = 汚れたまま、1 = 全部きれい）
    public void SetCleanRate(float rate)
    {
        m_targetRate = Mathf.Clamp01(rate);
        UpdateStageObjects();
    }

    // 最初の汚れた状態に戻す
    public void ResetWall()
    {
        m_targetRate = 0f;
        m_rate = 0f;
        ApplyColor();
        UpdateStageObjects();
    }

    void Update()
    {
        if (Mathf.Approximately(m_rate, m_targetRate))
        {
            return;
        }

        m_rate = Mathf.Lerp(m_rate, m_targetRate, 1f - Mathf.Exp(-Time.deltaTime * m_fadeSmoothness));

        if (Mathf.Abs(m_rate - m_targetRate) < 0.002f)
        {
            m_rate = m_targetRate;
        }

        ApplyColor();
    }

    void ApplyColor()
    {
        for (int i = 0; i < m_pieces.Count; i++)
        {
            if (m_pieces[i] == null)
            {
                continue;
            }

            m_pieces[i].Color = Color.Lerp(m_startColors[i], m_wallColor, m_rate);

            // 全部きれいになったら消す
            m_pieces[i].gameObject.SetActive(m_rate < 0.999f);
        }
    }

    void UpdateStageObjects()
    {
        if (m_stageObjects == null || m_stageObjects.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(
            Mathf.FloorToInt(m_targetRate * m_stageObjects.Length),
            0, m_stageObjects.Length - 1);

        // 全部きれいなら、どれも出さない
        bool isClean = m_targetRate >= 0.999f;

        for (int i = 0; i < m_stageObjects.Length; i++)
        {
            if (m_stageObjects[i] != null)
            {
                m_stageObjects[i].SetActive(!isClean && i == index);
            }
        }
    }
}
