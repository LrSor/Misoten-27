using System.Collections.Generic;
using UnityEngine;

//==================================================
// 「神速クッキング」のまな板（食材・包丁・切れ端の見た目）
//
// ・食材は、残りの量に合わせて左から短くなっていく
//   → 食材の長さを見れば、あとどれくらいで終わるかがわかる
// ・1 回振るごとに包丁が上下に動き、切れ端（輪切り）が出て倒れる
// ・まっすぐ切れたときだけ、切り口からキラキラ（FX_Sparkle）がパッと飛び散る
// ・食材は「右端」を固定して、左側から切っていく
//
// ■ 本番のモデルに差し替えるとき
//   ・食材：「食材」の子のモデルを差し替える。「食材」の大きさ X が 1 のときに、長さが 1m になるようにする
//   ・切れ端：「切れ端のひな形」の中身を差し替える
//   ・包丁：「包丁」の子のモデルを差し替える（「包丁」の位置が刃の下の端）
//==================================================
public class COOKING_BOARD : MonoBehaviour
{
    [Header("食材")]
    [Tooltip("短くしていく食材（大きさ X = 長さ）")]
    [SerializeField]
    private Transform m_food;

    [Tooltip("食材の長さ（m）")]
    [SerializeField]
    private float m_foodLength = 0.9f;

    [Tooltip("食材の右端の位置（まな板から見た X）")]
    [SerializeField]
    private float m_foodRightX = 0.45f;

    [Tooltip("短くなるときの、なめらかさ")]
    [SerializeField]
    private float m_shrinkSmoothness = 12f;

    [Header("包丁")]
    [SerializeField]
    private Transform m_knife;

    [Tooltip("包丁を上げたときの高さ（まな板から見た Y）")]
    [SerializeField]
    private float m_knifeUpY = 0.3f;

    [Tooltip("包丁を下ろしたときの高さ（まな板から見た Y）")]
    [SerializeField]
    private float m_knifeDownY = 0.03f;

    [Tooltip("包丁を 1 回下ろして戻るまでの時間（秒）")]
    [SerializeField]
    private float m_chopTime = 0.15f;

    [Tooltip("食材の切り口から、包丁をどれだけ左に置くか（m）")]
    [SerializeField]
    private float m_knifeOffsetX = 0.01f;

    [Header("切れ端")]
    [Tooltip("切れ端のひな形（非表示にしておく。これをコピーして出す）")]
    [SerializeField]
    private GameObject m_sliceTemplate;

    [Tooltip("切れ端の厚さ（m）。ブレて 0.5 点のときは半分の厚さ")]
    [SerializeField]
    private float m_sliceThickness = 0.018f;

    [Tooltip("出しておける切れ端の数（超えたら古いものから消す）")]
    [SerializeField]
    private int m_maxSlices = 80;

    [Tooltip("切れ端が倒れるまでの時間（秒）")]
    [SerializeField]
    private float m_fallTime = 0.2f;

    [Header("まっすぐ切れたときのキラキラ")]
    [Tooltip("切り口から出すキラキラ（空欄なら出さない）")]
    [SerializeField]
    private SPARKLE_PARTICLE m_cutSparkle;

    [Tooltip("まっすぐ切れたとき、1 回で出す星の数")]
    [SerializeField]
    private int m_cutSparkleCount = 6;

    [Tooltip("どれだけパッと光らせるか（0～1）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_cutSparkleFlash = 0.8f;

    private class SLICE
    {
        public Transform m_transform;
        public Vector3 m_startPosition;
        public Vector3 m_endPosition;
        public Quaternion m_startRotation;
        public Quaternion m_endRotation;
        public float m_timer;
    }

    private readonly List<SLICE> m_slices = new List<SLICE>();

    private float m_targetRest = 1f;
    private float m_rest = 1f;
    private float m_chopTimer = 999f;
    private Vector3 m_foodStartScale;
    private bool m_hasFoodStart;

    // 今の食材の左端（切り口）の X
    float CutX
    {
        get { return m_foodRightX - m_foodLength * m_rest; }
    }

    void Awake()
    {
        if (m_food != null)
        {
            m_foodStartScale = m_food.localScale;
            m_hasFoodStart = true;
        }

        if (m_sliceTemplate != null)
        {
            m_sliceTemplate.SetActive(false);
        }
    }

    // 残りの量（1 = まるごと、0 = 全部切った）
    public void SetRest(float rest)
    {
        m_targetRest = Mathf.Clamp01(rest);
    }

    // 1 回切る（isStraight：まっすぐ振れたか）
    public void Chop(float point, bool isStraight = false)
    {
        m_chopTimer = 0f;

        if (isStraight)
        {
            PlayCutSparkle();
        }

        // 切り口の位置に包丁を合わせる（次に食材が短くなる分も先に見込む）
        SpawnSlice(Mathf.Max(0.3f, point));
    }

    // 最初の状態に戻す
    public void ResetBoard()
    {
        foreach (SLICE slice in m_slices)
        {
            if (slice.m_transform != null)
            {
                Destroy(slice.m_transform.gameObject);
            }
        }

        m_slices.Clear();

        m_targetRest = 1f;
        m_rest = 1f;
        m_chopTimer = 999f;

        UpdateFood();
        UpdateKnife();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        // 食材を短くする
        m_rest = Mathf.Lerp(m_rest, m_targetRest, 1f - Mathf.Exp(-deltaTime * m_shrinkSmoothness));

        if (Mathf.Abs(m_rest - m_targetRest) < 0.0005f)
        {
            m_rest = m_targetRest;
        }

        UpdateFood();

        // 包丁を動かす
        m_chopTimer += deltaTime;
        UpdateKnife();

        // 切れ端を倒す
        foreach (SLICE slice in m_slices)
        {
            if (slice.m_transform == null || slice.m_timer >= m_fallTime)
            {
                continue;
            }

            slice.m_timer += deltaTime;

            float t = Mathf.Clamp01(slice.m_timer / Mathf.Max(0.01f, m_fallTime));

            slice.m_transform.localPosition = Vector3.Lerp(slice.m_startPosition, slice.m_endPosition, t);
            slice.m_transform.localRotation = Quaternion.Slerp(slice.m_startRotation, slice.m_endRotation, t);
        }
    }

    void UpdateFood()
    {
        if (m_food == null || !m_hasFoodStart)
        {
            return;
        }

        float length = m_foodLength * m_rest;

        m_food.gameObject.SetActive(length > 0.001f);

        Vector3 scale = m_foodStartScale;
        scale.x = Mathf.Max(0.0001f, length);
        m_food.localScale = scale;

        Vector3 position = m_food.localPosition;
        position.x = m_foodRightX - length * 0.5f;
        m_food.localPosition = position;
    }

    void UpdateKnife()
    {
        if (m_knife == null)
        {
            return;
        }

        // 下ろして、すぐ戻す
        float t = Mathf.Clamp01(m_chopTimer / Mathf.Max(0.01f, m_chopTime));
        float down = t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f;

        Vector3 position = m_knife.localPosition;
        position.x = CutX - m_knifeOffsetX;
        position.y = Mathf.Lerp(m_knifeUpY, m_knifeDownY, Mathf.Clamp01(down));
        m_knife.localPosition = position;
    }

    // 切り口の位置に移して、キラキラをまとめて出す
    void PlayCutSparkle()
    {
        if (m_cutSparkle == null || m_food == null)
        {
            return;
        }

        m_cutSparkle.transform.localPosition =
            new Vector3(CutX, m_food.localPosition.y + 0.03f, m_food.localPosition.z);

        m_cutSparkle.Burst(m_cutSparkleCount);
        m_cutSparkle.Flash(m_cutSparkleFlash);
    }

    void SpawnSlice(float point)
    {
        if (m_sliceTemplate == null || m_food == null)
        {
            return;
        }

        // 多すぎたら古いものから消す
        while (m_slices.Count >= Mathf.Max(1, m_maxSlices))
        {
            if (m_slices[0].m_transform != null)
            {
                Destroy(m_slices[0].m_transform.gameObject);
            }

            m_slices.RemoveAt(0);
        }

        GameObject sliceObject = Instantiate(m_sliceTemplate, transform);
        sliceObject.name = "Slice";
        sliceObject.SetActive(true);

        Transform sliceTransform = sliceObject.transform;

        // 厚さ（ブレたときは薄い）
        Vector3 scale = m_sliceTemplate.transform.localScale;
        scale.x = m_sliceThickness * Mathf.Clamp(point, 0.3f, 1.5f);
        sliceTransform.localScale = scale;

        float cutX = CutX;
        float height = m_food.localPosition.y;

        SLICE slice = new SLICE();
        slice.m_transform = sliceTransform;
        slice.m_startPosition = new Vector3(cutX, height, m_food.localPosition.z);
        slice.m_startRotation = m_sliceTemplate.transform.localRotation;

        // 左に少し転がって、横に倒れる
        slice.m_endPosition = new Vector3(
            cutX - Random.Range(0.04f, 0.1f),
            height * 0.35f,
            m_food.localPosition.z + Random.Range(-0.08f, 0.08f));
        slice.m_endRotation =
            Quaternion.Euler(0f, Random.Range(-40f, 40f), Random.Range(70f, 90f)) * slice.m_startRotation;

        sliceTransform.localPosition = slice.m_startPosition;
        sliceTransform.localRotation = slice.m_startRotation;

        m_slices.Add(slice);
    }

    void OnValidate()
    {
        m_foodLength = Mathf.Max(0.01f, m_foodLength);
        m_chopTime = Mathf.Max(0.01f, m_chopTime);
        m_maxSlices = Mathf.Max(1, m_maxSlices);
    }
}
