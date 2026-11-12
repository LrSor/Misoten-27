using System.Collections.Generic;
using UnityEngine;

public class Balloon : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")]
    [SerializeField] MiniGame m_miniGame;

    [Header("移動設定")]
    [SerializeField] Transform m_startPoint;

    [SerializeField]
    List<Transform> m_wayPoints = new List<Transform>();

    [SerializeField] Transform m_endPoint;

    [Tooltip("スタート地点からゴール地点まで到達する時間(秒)")]
    [SerializeField] float m_moveTime = 10.0f;

    [Header("キャッチ判定")]
    [SerializeField] private GameObject m_catchZone;

    private int m_missCount = 0;
    private bool m_catchFlag = false;

    //移動関連
    private float m_moveSpeed;
    private readonly List<Vector3> m_pathPoints = new();

    private int m_currentTargetIndex;
    private bool m_isMoving;

    //============================================================
    // オブジェクト初期化関数
    //============================================================
    public override void Activate()
    {
        m_pathPoints.Clear();

        // 経路作成
        if (m_startPoint != null)
        {
            m_pathPoints.Add(m_startPoint.position);
        }

        foreach (Transform point in m_wayPoints)
        {
            if (point != null)
            {
                m_pathPoints.Add(point.position);
            }
        }

        if (m_endPoint != null)
        {
            m_pathPoints.Add(m_endPoint.position);
        }

        // 経路が不足している場合
        if (m_pathPoints.Count < 2)
        {
            Debug.LogWarning("Balloon : 移動経路が不足しています");
            return;
        }

        // 開始位置へ移動
        transform.position = m_pathPoints[0];

        // 総距離計算
        float totalDistance = 0f;
        for (int i = 0; i < m_pathPoints.Count - 1; i++)
        {
            totalDistance += Vector3.Distance(
                m_pathPoints[i],
                m_pathPoints[i + 1]);
        }

        // 指定時間で到達する速度を算出
        m_moveSpeed = totalDistance / Mathf.Max(m_moveTime, 0.01f);

        // 最初の目的地は次のポイント
        m_currentTargetIndex = 1;
        m_isMoving = true;

        m_catchFlag = false;
    }

    //============================================================
    // オブジェクト終了関数
    //============================================================
    public override void Deactivate()
    {
        m_isMoving = false;
    }

    //============================================================
    // オブジェクト更新関数
    //============================================================
    public override void UpdateObject()
    {
        if (!m_isMoving)
        {
            return;
        }

        if (m_currentTargetIndex >= m_pathPoints.Count)
        {
            m_isMoving = false;
            return;
        }

        Vector3 targetPos = m_pathPoints[m_currentTargetIndex];

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            m_moveSpeed * Time.deltaTime);

        // 現在の目標地点へ到達
        if (Vector3.Distance(transform.position, targetPos) < 0.01f)
        {
            transform.position = targetPos;
            m_currentTargetIndex++;

            // 全地点到達
            if (m_currentTargetIndex >= m_pathPoints.Count)
            {
                m_isMoving = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == m_catchZone)
        {
            m_catchFlag = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == m_catchZone)
        {
            m_catchFlag = false;
            m_missCount++;
            Debug.Log("miss!!");
        }
    }

    public bool GetCatchFlag()
    {
        return m_catchFlag;
    }

    public int GetMissCount()
    {
        return m_missCount;
    }
}
