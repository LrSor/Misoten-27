using UnityEngine;
using UnityEngine.UI;

public class FindLightImage : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("ポインターのターゲット")]
    [SerializeField] private Transform m_targetTransfrom;
    private RectTransform m_rectTransform;
    [Header("ポインターの感度")]
    [SerializeField] private float m_scale = 50.0f;

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_rectTransform = GetComponent<RectTransform>();
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        gameObject.SetActive(false);   
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        //位置補正(通常時は座標0からの距離を参照するため)
        Vector3 pos = m_targetTransfrom.position - m_miniGame.transform.position;

        float x = pos.x * m_scale;
        float y = pos.z * -m_scale;

        m_rectTransform.anchoredPosition = new Vector3(x, y, 0);
    }
}
