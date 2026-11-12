using System.Collections.Generic;
using UnityEngine;

//==================================================
// 出てきた部品（運動データ）を見やすくする
//
// ・LiuPartManager（Liu さん）が出した部品（安井さんの PartObject）を、
//   地面から少し浮かせて、ゆっくり回転・上下させる
// ・グループごとに色を付ける（フィールドの目印と同じ色：A 赤 / B 青 / C 緑 / D 黄 / E 紫）
//
// ・シーンに 1 つ置く（PartSystem に付けている）
// ・安井さん・Liu さんのプレハブやデータは変更していない（出てきたものを、その場で見た目だけ変えている）
//==================================================
public class PART_VISUAL : MonoBehaviour
{
    [Header("浮かせ方")]
    [Tooltip("地面からの高さ（m）")]
    [SerializeField]
    private float m_floatHeight = 0.7f;

    [Tooltip("上下に動く幅（m）")]
    [SerializeField]
    private float m_bobHeight = 0.15f;

    [Tooltip("上下に動く速さ")]
    [SerializeField]
    private float m_bobSpeed = 2f;

    [Tooltip("回転の速さ（度/秒）")]
    [SerializeField]
    private float m_rotateSpeed = 90f;

    [Header("色")]
    [Tooltip("グループごとに色を付ける")]
    [SerializeField]
    private bool m_useGroupColor = true;

    [Tooltip("グループ A～E の色")]
    [SerializeField]
    private Color[] m_groupColors =
    {
        Color.red, Color.blue, Color.green, Color.yellow, Color.magenta,
    };

    // URP の Lit / Unlit シェーダーの色、と古いシェーダーの色
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    private class PART_STATE
    {
        public Vector3 m_groundPosition;
        public float m_phase;
    }

    private readonly Dictionary<Part, PART_STATE> m_parts =
        new Dictionary<Part, PART_STATE>();

    private readonly List<Part> m_removeList = new List<Part>();

    void LateUpdate()
    {
        // 新しく出てきた部品を登録する
        foreach (Part part in FindObjectsByType<Part>(FindObjectsSortMode.None))
        {
            if (!m_parts.ContainsKey(part))
            {
                Register(part);
            }
        }

        // 浮かせて回す
        m_removeList.Clear();

        foreach (KeyValuePair<Part, PART_STATE> pair in m_parts)
        {
            Part part = pair.Key;

            // 拾われて消えたもの
            if (part == null)
            {
                m_removeList.Add(part);
                continue;
            }

            PART_STATE state = pair.Value;

            float bob =
                Mathf.Sin(Time.time * m_bobSpeed + state.m_phase) * m_bobHeight;

            part.transform.position =
                state.m_groundPosition + Vector3.up * (m_floatHeight + bob);

            part.transform.Rotate(0f, m_rotateSpeed * Time.deltaTime, 0f, Space.World);
        }

        foreach (Part part in m_removeList)
        {
            m_parts.Remove(part);
        }
    }

    void Register(Part part)
    {
        PART_STATE state = new PART_STATE();

        // 出てきた地点（地面の高さ）
        state.m_groundPosition = part.transform.position;

        // みんな同じ動きにならないように、少しずつずらす
        state.m_phase = Random.Range(0f, Mathf.PI * 2f);

        m_parts.Add(part, state);

        if (m_useGroupColor &&
            part.Data != null)
        {
            SetColor(part, GetGroupColor(part.Data.group));
        }
    }

    Color GetGroupColor(GroupType group)
    {
        int index = (int)group;

        if (m_groupColors != null &&
            index >= 0 &&
            index < m_groupColors.Length)
        {
            return m_groupColors[index];
        }

        return Color.white;
    }

    // マテリアル自体は変えずに、この部品だけ色を変える
    static void SetColor(Part part, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BASE_COLOR_ID, color);
            block.SetColor(COLOR_ID, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
