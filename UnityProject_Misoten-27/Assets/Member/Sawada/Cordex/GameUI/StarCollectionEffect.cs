using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>PlayerDisplay 側で、ミニゲームの Canvas に依存せず星を回収する。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class StarCollectionEffect : MonoBehaviour
{
    [SerializeField] private RectTransform m_destination;
    private const float PulseHalfSeconds = 0.05f;

    private sealed class Collection
    {
        public Texture2D m_texture;
        public Rect[] m_stars;
        public Vector2 m_screenSize;
        public float m_angle, m_delay, m_duration, m_arcHeightRatio;
    }

    private readonly Queue<Collection> m_pending = new Queue<Collection>();
    private readonly List<RawImage> m_images = new List<RawImage>();
    private readonly Vector3[] m_corners = new Vector3[4];
    private bool m_isPlaying;
    private Vector3 m_originalScale;
    private bool m_hasOriginalScale;
    private bool m_collectionInProgress;

    public event Action<int> CollectionStarted;
    public event Action StarArrived;
    public event Action<bool> CollectionEnded;

    public void Play(Texture2D texture, Rect[] stars, float angle, float delay, float duration, float arcHeightRatio)
    {
        if (texture == null || stars == null || stars.Length == 0) return;
        if (!isActiveAndEnabled || m_destination == null || !m_destination.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("星の回収演出または移動先 Image が未設定・非表示です。", this);
            return;
        }
        m_pending.Enqueue(new Collection
        {
            m_texture = texture,
            m_stars = (Rect[])stars.Clone(),
            m_screenSize = new Vector2(Screen.width, Screen.height),
            m_angle = angle,
            m_delay = Mathf.Max(0f, delay),
            m_duration = Mathf.Max(0.01f, duration),
            m_arcHeightRatio = Mathf.Max(0f, arcHeightRatio)
        });
        if (m_isPlaying) return;
        m_isPlaying = true;
        StartCoroutine(PlayPending());
    }

    private IEnumerator PlayPending()
    {
        while (m_pending.Count > 0)
        {
            Collection collection = m_pending.Dequeue();
            if (!TryGetDestinationRect(out _)) break;
            m_collectionInProgress = true;
            CollectionStarted?.Invoke(collection.m_stars.Length);
            m_originalScale = m_destination.localScale;
            m_hasOriginalScale = true;
            foreach (Rect star in collection.m_stars)
            {
                var owner = new GameObject("FlyingStar", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                owner.transform.SetParent(transform, false);
                RawImage image = owner.GetComponent<RawImage>();
                image.texture = collection.m_texture;
                image.raycastTarget = false;
                m_images.Add(image);
                SetImageRect(image, ResizeForScreen(star, collection.m_screenSize), collection.m_angle);
            }

            float waitingSince = Time.unscaledTime;
            while (Time.unscaledTime - waitingSince < collection.m_delay)
            {
                for (int i = 0; i < m_images.Count; i++)
                    SetImageRect(m_images[i], ResizeForScreen(collection.m_stars[i], collection.m_screenSize), collection.m_angle);
                yield return null;
            }

            for (int i = 0; i < m_images.Count; i++)
            {
                float startedAt = Time.unscaledTime;
                while (true)
                {
                    if (!TryGetDestinationRect(out Rect destination))
                    {
                        ClearImages();
                        RestoreScale();
                        m_pending.Clear();
                        m_isPlaying = false;
                        EndCollection(false);
                        yield break;
                    }
                    float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / collection.m_duration);
                    Rect start = ResizeForScreen(collection.m_stars[i], collection.m_screenSize);
                    Vector2 control = (start.center + destination.center) * 0.5f;
                    // GUI と同じ画面左上原点。制御点を上側に置いて弧を描く。
                    control.y = Mathf.Min(start.center.y, destination.center.y) -
                        Mathf.Min(Screen.width, Screen.height) * collection.m_arcHeightRatio;
                    Vector2 center = Vector2.Lerp(Vector2.Lerp(start.center, control, progress),
                        Vector2.Lerp(control, destination.center, progress), progress);
                    Vector2 size = Vector2.Lerp(start.size, destination.size, progress);
                    SetImageRect(m_images[i], new Rect(center - size * 0.5f, size), collection.m_angle);
                    for (int j = i + 1; j < m_images.Count; j++)
                        SetImageRect(m_images[j], ResizeForScreen(collection.m_stars[j], collection.m_screenSize), collection.m_angle);
                    if (progress >= 1f) break;
                    yield return null;
                }

                m_images[i].gameObject.SetActive(false);
                StarArrived?.Invoke();
                float arrivedAt = Time.unscaledTime;
                while (Time.unscaledTime - arrivedAt < PulseHalfSeconds * 2f)
                {
                    if (m_destination == null) break;
                    float age = Time.unscaledTime - arrivedAt;
                    float pulse = age < PulseHalfSeconds ? age / PulseHalfSeconds : 2f - age / PulseHalfSeconds;
                    m_destination.localScale = m_originalScale * Mathf.Lerp(1f, 1.12f, pulse);
                    yield return null;
                }
                RestoreScale();
            }
            ClearImages();
            m_hasOriginalScale = false;
            EndCollection(true);
        }
        m_isPlaying = false;
    }

    private static Rect ResizeForScreen(Rect rect, Vector2 originalScreen)
    {
        Vector2 ratio = new Vector2(Screen.width / Mathf.Max(1f, originalScreen.x),
            Screen.height / Mathf.Max(1f, originalScreen.y));
        return new Rect(Vector2.Scale(rect.position, ratio), Vector2.Scale(rect.size, ratio));
    }

    private bool TryGetDestinationRect(out Rect rect)
    {
        rect = default;
        if (m_destination == null || !m_destination.gameObject.activeInHierarchy) return false;
        Canvas canvas = m_destination.GetComponentInParent<Canvas>();
        if (canvas == null || !canvas.isActiveAndEnabled) return false;
        Camera camera = GetCanvasCamera(canvas);
        m_destination.GetWorldCorners(m_corners);
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        foreach (Vector3 corner in m_corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            point.y = Screen.height - point.y;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private void SetImageRect(RawImage image, Rect rect, float angle)
    {
        var parent = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || image == null) return;
        Camera camera = GetCanvasCamera(canvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            new Vector2(rect.xMin, Screen.height - rect.yMax), camera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            new Vector2(rect.xMax, Screen.height - rect.yMin), camera, out Vector2 max);
        RectTransform target = image.rectTransform;
        target.localPosition = (min + max) * 0.5f;
        Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        float aspect = (float)image.texture.width / image.texture.height;
        if (size.x > size.y * aspect) size.x = size.y * aspect;
        else size.y = size.x / aspect;
        target.sizeDelta = size;
        // IMGUI の正方向は時計回り、RectTransform の Z 軸は反時計回り。
        target.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        Canvas root = canvas.rootCanvas;
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera ?? Camera.main;
    }

    private void RestoreScale()
    {
        if (m_hasOriginalScale && m_destination != null) m_destination.localScale = m_originalScale;
    }

    private void ClearImages()
    {
        foreach (RawImage image in m_images)
            if (image != null)
            {
                image.gameObject.SetActive(false);
                Destroy(image.gameObject);
            }
        m_images.Clear();
    }

    private void OnDisable()
    {
        CancelCollection();
    }

    public void CancelCollection()
    {
        StopAllCoroutines();
        ClearImages();
        RestoreScale();
        m_hasOriginalScale = false;
        m_pending.Clear();
        m_isPlaying = false;
        EndCollection(false);
    }

    private void EndCollection(bool completed)
    {
        if (!m_collectionInProgress) return;
        m_collectionInProgress = false;
        CollectionEnded?.Invoke(completed);
    }
}
