using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackCat : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("ライトオブジェクト")]
    [SerializeField] GameObject m_lightObject;

    [Header("ターゲット用Plane")]
    [SerializeField] GameObject m_plane;

    [Header("オブジェクトの移動範囲を設定")]
    [SerializeField] Vector3 m_moveLimit;

    [Header("通常時の移動速度")]
    [SerializeField] float m_moveSpeed;
    [Header("照射されている際の移動速度")]
    [SerializeField] float m_searchedMoveSpeed;

    [Header("目的地更新間隔")]
    [SerializeField] float m_targetChangeInterval = 2.0f;
    [Header("照射中目的地更新間隔")]
    [SerializeField] float m_searchedtargetChangeInterval = 1.0f;

    [Header("照射時の急旋回確率")]
    [SerializeField] float m_escapeDirectionChangeRate = 0.2f;

    private bool m_isSearched = false;

    private Vector3 m_startPosition;
    private Vector3 m_targetPosition;
    private float m_targetTimer;

    [Header("黒猫の目")]
    [SerializeField] GameObject m_eye;
    [Header("黒猫の目用光源")]
    [SerializeField] GameObject m_eyeGlow;
    [Header("目が光る時間")]
    [SerializeField] float m_eyeGlowTime;
    private bool m_eyeGlowingFlag = false;
    private float m_eyeGlowCount = 0;

    [Header("猫用の汗")]
    [SerializeField] private SweatImage m_sweat;

    //オブジェクト初期化関数
    public override void Activate()
    {
        gameObject.GetComponent<Renderer>().enabled = false;
        m_isSearched = false;

        m_startPosition = m_plane.transform.position;

        SetRandomTarget();

        //目の処理
        m_eye.GetComponent<Renderer>().enabled = false;
        m_eyeGlow.SetActive(false);
        m_eyeGlowingFlag = false;
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        gameObject.GetComponent<Renderer>().enabled = false;

        //目の処理
        m_eye.GetComponent<Renderer>().enabled = false;
        m_eyeGlow.SetActive(false);
        m_eyeGlowingFlag = false;
    }
    public override void UpdateObject()
    {
        m_targetTimer += Time.deltaTime;

        if(m_eyeGlowingFlag)
        {
            m_eyeGlowCount += Time.deltaTime;
            if(m_eyeGlowCount >= m_eyeGlowTime)
            {
                GlowEyeFinish();
            }
        }

        float moveSpeed = m_isSearched
            ? m_searchedMoveSpeed
            : m_moveSpeed;

        //==============================
        // 通常時
        //==============================
        if (!m_isSearched)
        {
            MoveNormal();
        }
        //==============================
        // 照射時(関数を分けるかもしれないので)
        //==============================
        else
        {
            MoveSearched();
        }

        //==============================
        // 移動
        //==============================
        transform.position = Vector3.MoveTowards(
            transform.position,
            m_targetPosition,
            moveSpeed * Time.deltaTime);

        // 進行方向を向く(一旦オミット。必要なら)
        //Vector3 dir = m_targetPosition - transform.position;
        //dir.y = 0;
        //
        //if (dir.sqrMagnitude > 0.01f)
        //{
        //    transform.rotation =
        //        Quaternion.Slerp(
        //            transform.rotation,
        //            Quaternion.LookRotation(dir),
        //            5f * Time.deltaTime);
        //}
    }

    //============================================================
    // ランダム目的地設定
    //============================================================
    private void SetRandomTarget()
    {
        m_targetPosition = new Vector3(
            m_startPosition.x + Random.Range(-m_moveLimit.x, m_moveLimit.x),
            transform.position.y,
            m_startPosition.z + Random.Range(-m_moveLimit.z, m_moveLimit.z));

        m_targetTimer = 0f;
    }

    //============================================================
    // 移動範囲制限
    //============================================================
    private void ClampTargetPosition()
    {
        m_targetPosition = new Vector3(
            Mathf.Clamp(
                m_targetPosition.x,
                m_startPosition.x - m_moveLimit.x,
                m_startPosition.x + m_moveLimit.x),

            transform.position.y,

            Mathf.Clamp(
                m_targetPosition.z,
                m_startPosition.z - m_moveLimit.z,
                m_startPosition.z + m_moveLimit.z));
    }

    public void SetSearched(bool searched)
    {
        gameObject.GetComponent<Renderer>().enabled = searched;
        m_isSearched = searched;

        m_eye.GetComponent<Renderer>().enabled = searched;

        m_sweat.gameObject.SetActive(searched);

        GlowEyeFinish();

        //照射された瞬間方向転換を行う
        if(m_isSearched)
        {
            SetRandomTarget();

            bool isLeft = m_targetPosition.x < transform.position.x;
            m_sweat.SetSwetDirection(isLeft);
        }
    }

    private void MoveNormal()
    {
        float targetChangeInterval = m_isSearched
            ? m_searchedtargetChangeInterval
            : m_targetChangeInterval;

        if (m_targetTimer >= targetChangeInterval ||
                Vector3.Distance(transform.position, m_targetPosition) < 0.3f)
        {
            SetRandomTarget();
        }
    }

    private void MoveSearched()
    {
        float targetChangeInterval = m_isSearched
            ? m_searchedtargetChangeInterval
            : m_targetChangeInterval;

        if (m_targetTimer >= targetChangeInterval ||
                Vector3.Distance(transform.position, m_targetPosition) < 0.3f)
        {
            SetRandomTarget();

            bool isLeft = m_targetPosition.x < transform.position.x;
            m_sweat.SetSwetDirection(isLeft);
        }
    }

    public void GlowEye()
    {
        if(m_isSearched || m_eyeGlowingFlag)
        {
            return;
        }

        m_eye.GetComponent<Renderer>().enabled = true;
        m_eyeGlow.SetActive(true);
        m_eyeGlowingFlag = true;
        m_eyeGlowCount = 0;
    }

    private void GlowEyeFinish()
    {
        if(!m_isSearched)
        { 
            m_eye.GetComponent<Renderer>().enabled = false; 
        }
        m_eyeGlow.SetActive(false);
        m_eyeGlowingFlag = false;
        m_eyeGlowCount = 0;
    }

    public bool GetIsGlowing()
    {
        return m_eyeGlowingFlag;
    }
}
