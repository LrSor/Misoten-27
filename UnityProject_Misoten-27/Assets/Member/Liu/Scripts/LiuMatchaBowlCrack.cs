using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class LiuMatchaBowlCrack : MonoBehaviour
{
    [SerializeField] private Color m_crackColor = new Color(0.15f, 0.08f, 0.05f);

    private LiuMatchaSpeedJudge m_speedJudge;
    private MeshRenderer m_crackRenderer;
    private Mesh m_crackMesh;

    private void Awake()
    {
        var game = GetComponentInParent<LiuMatchaGame>();
        if (game != null)
            m_speedJudge = game.GetComponentInChildren<LiuMatchaSpeedJudge>(true);

        var bowlRenderer = GetComponentInChildren<MeshRenderer>(true);
        if (m_speedJudge == null || bowlRenderer == null || bowlRenderer.sharedMaterial == null)
        {
            Debug.LogWarning("LiuMatchaMiniGame 内の LiuMatchaBowl に取り付けてください。", this);
            enabled = false;
            return;
        }

        var crack = new GameObject("LiuMatchaRimCrack");
        crack.layer = bowlRenderer.gameObject.layer;
        crack.transform.SetParent(transform, false);
        m_crackMesh = CreateCrackMesh(GetBowlBounds(bowlRenderer));
        crack.AddComponent<MeshFilter>().sharedMesh = m_crackMesh;
        m_crackRenderer = crack.AddComponent<MeshRenderer>();
        m_crackRenderer.sharedMaterial = bowlRenderer.sharedMaterial;
        m_crackRenderer.renderingLayerMask = bowlRenderer.renderingLayerMask;
        m_crackRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_crackRenderer.receiveShadows = false;
        var colors = new MaterialPropertyBlock();
        colors.SetColor("_BaseColor", m_crackColor);
        colors.SetColor("_Color", m_crackColor);
        m_crackRenderer.SetPropertyBlock(colors);
        m_crackRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        bool tooFast = m_speedJudge.isActiveAndEnabled
            && m_speedJudge.CurrentState == LiuMatchaSpeedJudge.SpeedState.TooFast;
        if (m_crackRenderer.enabled != tooFast)
            m_crackRenderer.enabled = tooFast;
    }

    private Bounds GetBowlBounds(MeshRenderer bowlRenderer)
    {
        // インポート時の軸やスケールを含めて、茶碗のローカル座標に変換する。
        Bounds source = bowlRenderer.localBounds;
        Matrix4x4 toBowl = transform.worldToLocalMatrix * bowlRenderer.localToWorldMatrix;
        var bounds = new Bounds(toBowl.MultiplyPoint3x4(source.center), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3(
                (corner & 1) == 0 ? source.min.x : source.max.x,
                (corner & 2) == 0 ? source.min.y : source.max.y,
                (corner & 4) == 0 ? source.min.z : source.max.z);
            bounds.Encapsulate(toBowl.MultiplyPoint3x4(point));
        }
        return bounds;
    }

    private static Mesh CreateCrackMesh(Bounds bowl)
    {
        // 六角形の白い縁に沿う、先端が細い一本のヒビと短い枝。
        // x,z はそれぞれ中心から縁までの半径を 1 とした値。
        Vector2[] outline =
        {
            new Vector2(0.992f,  0.26f),
            new Vector2(0.923f,  0.14f), new Vector2(0.946f,  0.14f),
            new Vector2(0.942f,  0.08f), new Vector2(0.966f,  0.08f),
            new Vector2(0.894f,  0.00f), new Vector2(0.919f,  0.00f),
            new Vector2(0.914f, -0.07f), new Vector2(0.930f, -0.07f),
            new Vector2(0.882f, -0.20f),
            new Vector2(0.921f, -0.015f), new Vector2(0.949f, 0.012f),
            new Vector2(0.953f, -0.002f), new Vector2(0.987f, -0.035f)
        };
        var vertices = new Vector3[outline.Length];
        float surfaceY = bowl.max.y + 0.006f * bowl.size.y;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vector3(
                bowl.center.x + outline[i].x * bowl.extents.x, surfaceY,
                bowl.center.z + outline[i].y * bowl.extents.z);
        }
        var mesh = new Mesh { name = "Matcha bowl rim crack" };
        mesh.vertices = vertices;
        mesh.triangles = new[]
        {
            0, 2, 1,  1, 2, 3,  2, 4, 3,  3, 4, 5,  4, 6, 5,
            5, 6, 7,  6, 8, 7,  7, 8, 9,  6, 11, 10,  11, 12, 10,  11, 13, 12
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDisable()
    {
        if (m_crackRenderer != null)
            m_crackRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (m_crackRenderer != null)
            Destroy(m_crackRenderer.gameObject);
        if (m_crackMesh != null)
            Destroy(m_crackMesh);
    }
}
