using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>分割済み Sprite を、指定した秒数で1ループ再生する UI 用コンポーネント。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class UISpriteSequencePlayer : MonoBehaviour
{
    [Header("分割済みの画像（Sprite Mode: Multiple）")]
    [SerializeField] private Texture2D m_spriteSheet;
    [SerializeField, HideInInspector] private Sprite[] m_frames = new Sprite[0];
    [Header("1ループの秒数")]
    [SerializeField, Min(0.01f)] private float m_durationSeconds = 2f;
    [SerializeField] private bool m_loop = true;
    [SerializeField] private bool m_playOnEnable = true;
    [SerializeField] private bool m_useUnscaledTime = true;

    private Image m_image;
    private double m_elapsed;
    private bool m_isLooping;
    public bool IsPlaying { get; private set; }
    public bool HasCompleted { get; private set; }
    public int CurrentFrameIndex { get; private set; }
    public float DurationSeconds => Mathf.Max(0.01f, m_durationSeconds);

    private void Awake() => m_image = GetComponent<Image>();

    private void OnEnable()
    {
        if (m_playOnEnable) Play();
        else Stop();
    }

    private void OnDisable() => Stop();

    private void Update()
    {
        Advance(m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    public void Play()
    {
        StartPlayback(m_loop);
    }

    /// <summary>Loop 設定にかかわらず1回再生する。再生できなければ false。</summary>
    public bool PlayOnce() => StartPlayback(false);

    private bool StartPlayback(bool loop)
    {
        Stop();
        if (m_frames == null || m_frames.Length == 0)
        {
            Debug.LogWarning("再生する Sprite がありません。Sprite Sheet に分割済みの画像を指定してください。", this);
            return false;
        }
        m_isLooping = loop;
        IsPlaying = true;
        return true;
    }

    public void Stop()
    {
        IsPlaying = false;
        HasCompleted = false;
        m_elapsed = 0d;
        ShowFrame(0);
    }

    // 経過時間から求めるので、フレーム落ちしても1ループの所要時間を維持する。
    internal void Advance(float deltaSeconds)
    {
        if (!IsPlaying) return;
        if (m_frames == null || m_frames.Length == 0)
        {
            Stop();
            return;
        }
        m_elapsed += Math.Max(0d, deltaSeconds);
        double duration = DurationSeconds;
        if (!m_isLooping && m_elapsed >= duration)
        {
            ShowFrame(m_frames.Length - 1);
            IsPlaying = false;
            HasCompleted = true;
            return;
        }
        if (m_isLooping) m_elapsed %= duration;
        int index = (int)(m_elapsed / duration * m_frames.Length);
        ShowFrame(Math.Min(index, m_frames.Length - 1));
    }

    private void ShowFrame(int index)
    {
        CurrentFrameIndex = index;
        if (m_image == null) m_image = GetComponent<Image>();
        if (m_image != null && m_frames != null && m_frames.Length > index)
            m_image.sprite = m_frames[index];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_durationSeconds = Mathf.Max(0.01f, m_durationSeconds);
        if (m_spriteSheet == null)
        {
            m_frames = new Sprite[0];
            return;
        }
        string path = UnityEditor.AssetDatabase.GetAssetPath(m_spriteSheet);
        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite) sprites.Add(sprite);
        // sample_2 が sample_10 より前になるよう、末尾の連番で並べる。
        sprites.Sort((a, b) =>
        {
            int comparison = FrameNumber(a.name).CompareTo(FrameNumber(b.name));
            return comparison != 0 ? comparison : string.CompareOrdinal(a.name, b.name);
        });
        m_frames = sprites.ToArray();
    }

    private static int FrameNumber(string name)
    {
        int start = name.Length;
        while (start > 0 && char.IsDigit(name[start - 1])) start--;
        return int.TryParse(name.Substring(start), out int number) ? number : int.MaxValue;
    }
#endif
}
