using UnityEngine;

public class FindLight : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("ポインターオブジェクト")]
    public GameObject m_startPoint;
    [Header("ポインターターゲット用Plane")]
    public GameObject m_plane;

    [Header("オブジェクトの移動範囲を設定")]
    [SerializeField] Vector3 m_moveLimit;

    [Header("黒猫オブジェクト")]
    [SerializeField] GameObject m_blackCat;

    [Header("照射時間")]
    [SerializeField] float m_shineTime;
    private float m_shineCount = 0;

    private bool m_shineOnFlag = false;

    [Header("プログレスバー本体")]
    [SerializeField] ShineOnProgress m_shineProgress;

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_shineCount = 0;
        m_shineOnFlag = false;
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {

    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        //移動
        MoveByPointer();

        //黒猫との接触中フラグが建っている場合、カウントする
        if(m_shineOnFlag)
        {
            m_shineCount += Time.deltaTime;

            //プログレスバー更新
            m_shineProgress.UpdateProgress(m_shineCount / m_shineTime);

            //一定時間照射でゲームクリア
            if (m_shineCount >= m_shineTime)
            {
                //ミニゲーム終了フラグ
                m_miniGame.FinishMiniGame(true);

                //スコア取得(今回は残り時間*100の小数点以下切り捨てとする)
                float score = m_miniGame.GetComponent<FindCat>().GetTime() * 100;
                m_miniGame.SetScore((int)score);
            }
        }
        else 
        {
            if(m_shineCount > 0)
            {
                m_shineCount -= Time.deltaTime;
                m_shineProgress.UpdateProgress(m_shineCount / m_shineTime);
                if(m_shineCount <= 0)
                {
                    m_shineProgress.FinishProgress();
                }
            }
        }
    }

    private void MoveByPointer()
    {
        var n = m_plane.transform.up;
        var x = m_plane.transform.position;
        var x0 = m_startPoint.transform.position;
        var m = m_startPoint.transform.forward;
        var h = Vector3.Dot(n, x);

        var intersectPoint = x0 + ((h - Vector3.Dot(n, x0)) / (Vector3.Dot(n, m))) * m;
        //インスペクタービューにて移動範囲を設定する
        Vector3 pos = intersectPoint - m_miniGame.transform.position;

        if(pos.x < -m_moveLimit.x || m_moveLimit.x < pos.x)
        {
            intersectPoint.x = x0.x + m_moveLimit.x * Mathf.Sign(pos.x);
        }
        if (pos.z < -m_moveLimit.z || m_moveLimit.z < pos.z)
        {
            intersectPoint.z = x0.z + m_moveLimit.z * Mathf.Sign(pos.z);
        }

        transform.position = intersectPoint;

    }

    private void OnTriggerEnter(Collider other)
    {
        //黒猫と重なった場合、接触中フラグを建てる
        if(other.gameObject == m_blackCat)
        {
            m_shineOnFlag = true;

            m_blackCat.GetComponent<BlackCat>().SetSearched(true);

            m_shineProgress.StartProgress();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //黒猫が離れた場合、接触中フラグを折る
        if (other.gameObject == m_blackCat)
        {
            m_shineOnFlag = false;

            m_blackCat.GetComponent<BlackCat>().SetSearched(false);

        }
    }
}
