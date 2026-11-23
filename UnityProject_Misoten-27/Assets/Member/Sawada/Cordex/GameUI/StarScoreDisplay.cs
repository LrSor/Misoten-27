using TMPro;
using UnityEngine;

/// <summary>保存済み合計とは別の一時的な表示値を、星の到着に合わせて更新する。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StarCollectionEffect))]
public sealed class StarScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text m_index;
    [SerializeField] private PlayerScore m_playerScore;
    [Tooltip("未接続の場合は到着数だけを表示に加算する。保存スコアは変更しない。")]
    [SerializeField] private StarCollectionEffect m_collectionEffect;

    private int m_displayedScore;
    private bool m_collecting;
    private bool m_warnedMissingSource;

    public int DisplayedScore => m_displayedScore;

    private void OnEnable()
    {
        if (m_collectionEffect == null) m_collectionEffect = GetComponent<StarCollectionEffect>();
        if (m_index == null && transform.parent != null)
            m_index = transform.parent.Find("Index")?.GetComponent<TMP_Text>();
        m_collectionEffect.CollectionStarted += OnCollectionStarted;
        m_collectionEffect.StarArrived += OnStarArrived;
        m_collectionEffect.CollectionEnded += OnCollectionEnded;
        SynchronizeFromSavedScore();
    }

    private void Start()
    {
        // 別コンポーネントの Awake で PlayerCanvas が接続される場合にも対応する。
        SynchronizeFromSavedScore();
    }

    // PlayerDisplay とプレイヤーを実行時に接続する場合にも使用できる。
    public void SetPlayerScore(PlayerScore source)
    {
        BindPlayerScore(source, true);
    }

    // A late network result must not jump to the final score before its stars arrive.
    public void BindPlayerScore(PlayerScore source, bool synchronize)
    {
        m_playerScore = source;
        m_warnedMissingSource = false;
        if (synchronize && !m_collecting) SynchronizeFromSavedScore();
    }

    public void SynchronizeFromSavedScore()
    {
        // 回収中に呼ばれても先に最終値へ飛ばさない。
        if (m_collecting) return;
        if (TryGetSavedScore(out int total)) m_displayedScore = total;
        RefreshLabel();
    }

    private bool TryGetSavedScore(out int total)
    {
        if (m_playerScore == null && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas ownerCanvas = canvas.rootCanvas;
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    foreach (PlayerScore candidate in root.GetComponentsInChildren<PlayerScore>(true))
                    {
                        Canvas candidateCanvas = candidate.GetPlayerCanvas();
                        if (candidateCanvas == null || candidateCanvas.rootCanvas != ownerCanvas) continue;
                        m_playerScore = candidate;
                        break;
                    }
                    if (m_playerScore != null) break;
                }
            }
        }
        total = m_playerScore != null ? Mathf.Max(0, m_playerScore.GetTotalScore()) : 0;
        return m_playerScore != null;
    }

    private void OnCollectionStarted(int starCount)
    {
        m_collecting = true;
        if (!TryGetSavedScore(out _) && !m_warnedMissingSource)
        {
            Debug.LogWarning("StarScoreDisplay: PlayerScore が未接続です。Index の表示だけを更新します。", this);
            m_warnedMissingSource = true;
        }
    }

    private void OnStarArrived()
    {
        if (!m_collecting) return;
        if (TryGetSavedScore(out int total))
        {
            // 同じミニゲームの最高点が更新されない場合は増やさない。
            if (m_displayedScore < total) m_displayedScore++;
        }
        else if (m_displayedScore < int.MaxValue) m_displayedScore++;
        RefreshLabel();
    }

    private void OnCollectionEnded(bool completed)
    {
        m_collecting = false;
        // 中断時も、未着分を含む正式な合計へ表示を戻す。
        SynchronizeFromSavedScore();
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
        SynchronizeFromSavedScore();
    }
}
