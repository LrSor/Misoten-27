using System;
using System.Collections.Generic;
using UnityEngine;

//==================================================
// 「突風バルーンキャッチ」の風船の飛び方
//
// ・風船は画面の外から流れてきて、真ん中を通って反対側へ抜けていく（1 回 = 1 回のチャンス）
// ・何回流れてくるか、それぞれの向き・速さ・高さ・ゆれ方・どこで向きが変わるか（突風）は、
//   「流れ方」のリストで調整できる
//     例）「向きが変わる X」に 2 を入れると、X = 2 まで来たところで突風が吹いて逆向きに戻っていく
// ・X は、このオブジェクトから見た左右の位置（0 が真ん中）
//==================================================
public class BALLOON_FLIGHT : MonoBehaviour
{
    [Serializable]
    public class PASS
    {
        [Tooltip("左から来る（オフなら右から）")]
        public bool m_fromLeft = true;

        [Tooltip("流れる速さ（m/秒）")]
        public float m_speed = 3f;

        [Tooltip("高さ（m）")]
        public float m_height = 4.5f;

        [Tooltip("上下にゆれる幅（m）")]
        public float m_waveHeight = 0.4f;

        [Tooltip("上下にゆれる速さ")]
        public float m_waveSpeed = 2f;

        [Tooltip("ここまで来たら突風で向きが変わる X（順番に。空なら変わらない）")]
        public float[] m_turnAtX = new float[0];
    }

    [Header("風船")]
    [SerializeField]
    private Transform m_balloon;

    [Header("流れ方（1 つ = 1 回のチャンス）")]
    [SerializeField]
    private List<PASS> m_passes = new List<PASS>();

    [Header("場所")]
    [Tooltip("画面の外の X（ここから出てきて、ここを越えると抜けたことにする）")]
    [SerializeField]
    private float m_outsideX = 11f;

    [Tooltip("風船が流れる奥行き（このオブジェクトから見た Z）")]
    [SerializeField]
    private float m_depth = 8f;

    [Tooltip("次の風船が来るまでの間（秒）")]
    [SerializeField]
    private float m_passInterval = 1.2f;

    private int m_passIndex;
    private int m_turnIndex;
    private float m_direction;
    private float m_x;
    private float m_time;
    private float m_waitTimer;
    private bool m_isFlying;
    private bool m_isRunning;
    private bool m_isCaught;

    public int PassIndex { get { return m_passIndex; } }
    public int PassCount { get { return m_passes.Count; } }
    public bool IsFlying { get { return m_isFlying && !m_isCaught; } }
    public bool IsAllDone { get { return m_isRunning && !m_isCaught && m_passIndex >= m_passes.Count; } }
    public float X { get { return m_x; } }
    public float Depth { get { return m_depth; } }
    public Transform Balloon { get { return m_balloon; } }

    // 今の風船の高さ（風船がいないときは次の風船の高さ）
    public float Height
    {
        get
        {
            PASS pass = GetPass(Mathf.Min(m_passIndex, m_passes.Count - 1));
            return pass != null ? pass.m_height : 4.5f;
        }
    }

    void Awake()
    {
        Stop();
    }

    // 1 回目の風船から始める
    public void StartFlights()
    {
        m_isRunning = true;
        m_isCaught = false;
        m_passIndex = 0;
        m_waitTimer = m_passInterval;
        m_isFlying = false;

        SetBalloonVisible(false);
    }

    // 止めて、風船を隠す
    public void Stop()
    {
        m_isRunning = false;
        m_isFlying = false;
        m_isCaught = false;

        SetBalloonVisible(false);
    }

    // つかまった（ここからは BALLOON_LASSO が風船を動かす）
    public void Caught()
    {
        m_isCaught = true;
        m_isFlying = false;
    }

    // 真ん中の判定エリア（X が ±halfWidth 以内）にいるか
    public bool IsInZone(float halfWidth)
    {
        return IsFlying && Mathf.Abs(m_x) <= halfWidth;
    }

    // BALLOON_GAME から毎フレーム呼ぶ
    public void Tick(float deltaTime)
    {
        if (!m_isRunning || m_isCaught)
        {
            return;
        }

        if (!m_isFlying)
        {
            if (m_passIndex >= m_passes.Count)
            {
                return;
            }

            m_waitTimer -= deltaTime;

            if (m_waitTimer <= 0f)
            {
                BeginPass();
            }

            return;
        }

        PASS pass = GetPass(m_passIndex);

        if (pass == null)
        {
            return;
        }

        m_time += deltaTime;
        m_x += m_direction * pass.m_speed * deltaTime;

        // 突風で向きが変わる
        if (pass.m_turnAtX != null && m_turnIndex < pass.m_turnAtX.Length)
        {
            float turnX = pass.m_turnAtX[m_turnIndex];

            if ((m_direction > 0f && m_x >= turnX) ||
                (m_direction < 0f && m_x <= turnX))
            {
                m_direction = -m_direction;
                m_turnIndex++;
            }
        }

        float y = pass.m_height + Mathf.Sin(m_time * pass.m_waveSpeed) * pass.m_waveHeight;

        if (m_balloon != null)
        {
            m_balloon.localPosition = new Vector3(m_x, y, m_depth);
            m_balloon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(m_time * 3f) * 10f - m_direction * 8f);
        }

        // 反対側へ抜けた
        if (Mathf.Abs(m_x) > m_outsideX + 0.5f && m_x * m_direction > 0f)
        {
            m_isFlying = false;
            m_passIndex++;
            m_waitTimer = m_passInterval;

            SetBalloonVisible(false);
        }
    }

    void BeginPass()
    {
        PASS pass = GetPass(m_passIndex);

        if (pass == null)
        {
            return;
        }

        m_isFlying = true;
        m_turnIndex = 0;
        m_time = 0f;
        m_direction = pass.m_fromLeft ? 1f : -1f;
        m_x = -m_direction * m_outsideX;

        SetBalloonVisible(true);
    }

    PASS GetPass(int index)
    {
        if (index < 0 || index >= m_passes.Count)
        {
            return null;
        }

        return m_passes[index];
    }

    void SetBalloonVisible(bool isVisible)
    {
        if (m_balloon != null)
        {
            m_balloon.gameObject.SetActive(isVisible);
        }
    }

    // Scene ビューに、風船の通り道を表示する
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        foreach (PASS pass in m_passes)
        {
            if (pass == null)
            {
                continue;
            }

            Vector3 from = transform.TransformPoint(new Vector3(-m_outsideX, pass.m_height, m_depth));
            Vector3 to = transform.TransformPoint(new Vector3(m_outsideX, pass.m_height, m_depth));
            Gizmos.DrawLine(from, to);

            if (pass.m_turnAtX == null)
            {
                continue;
            }

            foreach (float turnX in pass.m_turnAtX)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(turnX, pass.m_height, m_depth)), 0.3f);
            }
        }
    }
}
