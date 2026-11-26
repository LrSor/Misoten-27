using UnityEngine;
using UnityEngine.UI;

public class WateringProgress : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("プログレスバー")]
    [SerializeField] private Image m_progressBar;
    [Header("プログレスバーの最大スケール")]
    [SerializeField] private float m_progressBarScale;
    private float m_progress;

    [Header("ポインターのターゲット")]
    [SerializeField] private Transform m_targetTransfrom;
    private RectTransform m_rectTransform;
    [Header("ポインターターゲット用Plane")]
    public GameObject m_plane;
    [Header("ポインターの感度")]
    [SerializeField] private float m_scale = 50.0f;
    [Header("ポインターからの位置補正")]
    [SerializeField] private Vector2 m_correctPosition;

    private void Start()
    {
        
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_rectTransform = GetComponent<RectTransform>();

        m_progress = 0;
        
        gameObject.SetActive(false);
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        gameObject.SetActive(false);
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        
    }

    public void StartProgress()
    {
        gameObject.SetActive(true);
        m_rectTransform = GetComponent<RectTransform>();

        //位置補正(花壇は動かないので一回のみ)
        Vector3 pos = m_targetTransfrom.position - m_miniGame.transform.position;
        float x = pos.x * m_scale + m_correctPosition.x;
        float y = pos.z * -m_scale + m_correctPosition.y;

        m_rectTransform.anchoredPosition = new Vector3(x, y, 0);

        m_progressBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_progress * m_progressBarScale);
    }

    public void UpdateProgress(float progress)
    {
        m_progress = progress;
        m_progressBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_progress * m_progressBarScale);
    }

    public void FinishProgress()
    {
        gameObject.SetActive(false);
        m_progressBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);

        Debug.Log("FinishProgrress");
    }
}
