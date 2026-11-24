using UnityEngine;

public sealed class MiniGameUIResultState : BaseState
{
    [Header("リザルトの吹き出し")]
    [Tooltip("未指定の場合は Explanation State に設定済みの Speech Bubble を使用する。")]
    [SerializeField] private RectTransform m_speechBubble;
    [SerializeField, Min(0f)] private float m_slideSeconds = 0.35f;
    [Tooltip("Canvas の座標単位で指定する、画面下端からの余白。")]
    [SerializeField, Min(0f)] private float m_bottomMargin = 24f;

    private MiniGameUIBase m_ui;
    private RectTransform m_bubble;
    private UISpriteSequencePlayer[] m_sequences;
    private Vector2 m_anchorMin, m_anchorMax, m_pivot, m_sizeDelta;
    private Vector3 m_position;
    private bool m_presenting;

    public override void InitState()
    {
        m_ui = GetComponent<MiniGameUIBase>();
        GetComponent<MiniGameUIExplanationState>()?.HideCallIcon();
        m_ui.EnterPhase(MiniGameUIBase.Phase.Result);
        ShowBubble();
    }

    public override void UpdateState()
    {
        UpdateBubble();
        if (!m_ui.IsExternallyDriven && m_ui.RemainingSeconds <= 0f)
            m_ui.CompleteResult();
    }

    private void ShowBubble()
    {
        m_bubble = m_speechBubble != null ? m_speechBubble
            : GetComponent<MiniGameUIExplanationState>()?.SpeechBubble;
        if (m_bubble == null || !(m_bubble.parent is RectTransform)) return;
        m_anchorMin = m_bubble.anchorMin;
        m_anchorMax = m_bubble.anchorMax;
        m_pivot = m_bubble.pivot;
        m_sizeDelta = m_bubble.sizeDelta;
        m_position = m_bubble.anchoredPosition3D;
        Vector2 size = m_bubble.rect.size;
        m_bubble.anchorMin = m_bubble.anchorMax = new Vector2(0.5f, 0f);
        m_bubble.pivot = new Vector2(0.5f, 0f);
        m_bubble.sizeDelta = size;
        m_presenting = true;
        // 登場演出と同時に、先頭から Loop 設定に従って再生する。
        m_bubble.gameObject.SetActive(true);
        m_sequences = m_bubble.GetComponentsInChildren<UISpriteSequencePlayer>(true);
        foreach (UISpriteSequencePlayer sequence in m_sequences) sequence.Stop();
        UpdateBubble();
        foreach (UISpriteSequencePlayer sequence in m_sequences)
            if (sequence.isActiveAndEnabled) sequence.Play();
    }

    private void UpdateBubble()
    {
        if (!m_presenting || m_bubble == null) return;
        var parent = (RectTransform)m_bubble.parent;
        Canvas canvas = m_bubble.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : parent;
        // 親の配置によらず、Canvas の画面下端中央を親のローカル座標へ変換する。
        Vector3 bottom = parent.InverseTransformPoint(canvasRect.TransformPoint(
            new Vector3(canvasRect.rect.center.x, canvasRect.rect.yMin, 0f)));
        float elapsed = m_ui.PhaseElapsedSeconds;
        float progress = m_slideSeconds > 0f ? Mathf.Clamp01(elapsed / m_slideSeconds) : 1f;
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        float height = m_bubble.rect.height * Mathf.Abs(m_bubble.localScale.y);
        m_bubble.localPosition = new Vector3(bottom.x,
            bottom.y + Mathf.Lerp(-height - m_bottomMargin, m_bottomMargin, eased), bottom.z);
    }

    public override void UnInitState() => HideBubble();

    private void OnDisable() => HideBubble();

    private void HideBubble()
    {
        if (!m_presenting) return;
        m_presenting = false;
        if (m_sequences != null)
            foreach (UISpriteSequencePlayer sequence in m_sequences)
                if (sequence != null) sequence.Stop();
        if (m_bubble == null) return;
        m_bubble.gameObject.SetActive(false);
        m_bubble.anchorMin = m_anchorMin;
        m_bubble.anchorMax = m_anchorMax;
        m_bubble.pivot = m_pivot;
        m_bubble.sizeDelta = m_sizeDelta;
        m_bubble.anchoredPosition3D = m_position;
    }
}
