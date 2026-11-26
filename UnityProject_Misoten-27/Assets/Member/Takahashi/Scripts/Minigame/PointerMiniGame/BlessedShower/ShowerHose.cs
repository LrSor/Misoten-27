using UnityEngine;

public class ShowerHose : MiniGameObject
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

    //オブジェクト初期化関数
    public override void Activate()
    {
       
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {

    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        //回転
        RotateByPointer();
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

        if (pos.x < -m_moveLimit.x || m_moveLimit.x < pos.x)
        {
            intersectPoint.x = x0.x + m_moveLimit.x * Mathf.Sign(pos.x);
        }
        if (pos.z < -m_moveLimit.z || m_moveLimit.z < pos.z)
        {
            intersectPoint.z = x0.z + m_moveLimit.z * Mathf.Sign(pos.z);
        }

        transform.position = intersectPoint;

    }

    private void RotateByPointer()
    {
        gameObject.transform.forward = m_startPoint.transform.forward;
    }
}
