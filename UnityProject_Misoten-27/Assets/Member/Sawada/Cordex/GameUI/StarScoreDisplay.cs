using TMPro;
using UnityEngine;

/// <summary>到着した星の数だけ Index に加算する。保存スコアの読み書きは行わない。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StarCollectionEffect))]
public sealed class StarScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text m_index;
    [SerializeField] private StarCollectionEffect m_collectionEffect;

    private int m_displayedScore;
    private bool m_collecting;

    public int DisplayedScore => m_displayedScore;

    private void OnEnable()
    {
        if (m_collectionEffect == null) m_collectionEffect = GetComponent<StarCollectionEffect>();
        if (m_index == null && transform.parent != null)
            m_index = transform.parent.Find("Index")?.GetComponent<TMP_Text>();
        m_collectionEffect.CollectionStarted += OnCollectionStarted;
        m_collectionEffect.StarArrived += OnStarArrived;
        m_collectionEffect.CollectionEnded += OnCollectionEnded;
        RefreshLabel();
    }

    private void OnCollectionStarted(int starCount)
    {
        m_collecting = true;
    }

    private void OnStarArrived()
    {
        if (!m_collecting) return;
        if (m_displayedScore < int.MaxValue) m_displayedScore++;
        RefreshLabel();
    }

    private void OnCollectionEnded(bool completed)
    {
        // 中断時も到着済みの数だけ残し、未着分は加算しない。
        m_collecting = false;
    }

    private void RefreshLabel()
    {
        if (m_index != null) m_index.text = m_displayedScore.ToString("00");
    }

    private void OnDisable()
    {
        if (m_collectionEffect != null)
        {
            m_collectionEffect.CollectionStarted -= OnCollectionStarted;
            m_collectionEffect.StarArrived -= OnStarArrived;
            m_collectionEffect.CollectionEnded -= OnCollectionEnded;
        }
        m_collecting = false;
    }
}
