using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaterSpline : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("ホースオブジェクト")]
    [SerializeField] private Transform m_shootPoint;

    [Header("Water")]
    [SerializeField] private float m_distance = 5f;
    [SerializeField] private float m_height = 2f;
    [SerializeField] private int m_segmentCount = 30;
    [SerializeField] private LayerMask m_hitLayer;

    private LineRenderer m_lineRenderer;

    [SerializeField]
    private Transform m_endPoint;

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_lineRenderer = GetComponent<LineRenderer>();
        m_lineRenderer.positionCount = m_segmentCount;
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {

    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        DrawWater();
    }

    private void DrawWater()
    {
        if (m_shootPoint == null) return;

        Vector3 startPos = m_shootPoint.position;
        Vector3 forward = m_shootPoint.forward;

        Vector3 prevPos = startPos;

        FlowerBed waterHitFlowerBed = null;

        for (int i = 0; i < m_segmentCount; i++)
        {
            float t = i / (float)(m_segmentCount - 1);

            float x = t * m_distance;
            float y = 4f * m_height * t * (1f - t);

            Vector3 point =
                startPos +
                forward * x +
                -Vector3.forward * y;


            m_lineRenderer.SetPosition(i, point);

            if (i > 0)
            {
                Vector3 dir = point - prevPos;
                float len = dir.magnitude;

                if (Physics.Raycast(
                        prevPos,
                        dir.normalized,
                        out RaycastHit hit,
                        len,
                        m_hitLayer))
                {
                    FlowerBed target =
                        hit.collider.GetComponent<FlowerBed>();

                    if (target != null)
                    {
                        target.OnWaterHit();
                        waterHitFlowerBed = target;
                    }

                    break;
                }
            }

            m_miniGame.GetComponent<BlessedShower>().NotWaterHit(waterHitFlowerBed);

            prevPos = point;
        }
    }

}
