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
        MoveByPointer();
    }

    private void MoveByPointer()
    {
        var n = m_plane.transform.up;
        var x = m_plane.transform.position;
        var x0 = m_startPoint.transform.position;
        var m = m_startPoint.transform.forward;
        var h = Vector3.Dot(n, x);

        var intersectPoint = x0 + ((h - Vector3.Dot(n, x0)) / (Vector3.Dot(n, m))) * m;

        transform.position = intersectPoint;
    }
}
