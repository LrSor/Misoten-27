using UnityEngine;

public class FlowerBed : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================
    [Header("ミニゲーム本体")] //不要なら削除しても良し
    [SerializeField] MiniGame m_miniGame;

    [Header("水やりプログレスバー")]
    [SerializeField] WateringProgress m_progressBar;

    [Header("水やり秒数")]
    [SerializeField] float m_wateringTime = 3.0f;

    private float m_wateringCount = 0.0f;

    [Header("咲いている花オブジェクト")]
    [SerializeField] GameObject m_flowerBlessed;
    [Header("枯れている花オブジェクト")]
    [SerializeField] GameObject m_flowerWither;

    private bool m_isWateringFlag = false;
    private bool m_isBlessedFlag = false;

    [Header("枯れるまでの時間")]
    [SerializeField] float m_witherTime = 10.0f;
    private float m_witherCount = 0.0f;

    private void Start()
    {
        m_flowerBlessed.SetActive(false);
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_progressBar.gameObject.SetActive(false);

        m_flowerBlessed.SetActive(false);
        m_flowerWither.SetActive(true);

        m_isWateringFlag = false;
        m_isBlessedFlag = false;

        m_wateringCount = 0.0f;
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {

    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        //咲いている場合、枯れるまでのカウントダウンを始める
        if(m_isBlessedFlag)
        {

        }

        if (m_wateringCount > 0 && !m_isWateringFlag)
        {
            m_wateringCount -= Time.deltaTime;
            m_progressBar.UpdateProgress(m_wateringCount / m_wateringTime);
            if (m_wateringCount <= 0)
            {
                m_progressBar.FinishProgress();
                m_isWateringFlag = false;
            }
        }
    }

    public void OnWaterHit()
    {
        if(m_isBlessedFlag)
        {
            return;
        }

        if(!m_isWateringFlag)
        {
            m_isWateringFlag = true;

            m_progressBar.StartProgress();
        }

        //水やり進行
        m_wateringCount += Time.deltaTime;

        //プログレスバー更新
        m_progressBar.UpdateProgress(m_wateringCount / m_wateringTime);

        if(m_wateringCount >= m_wateringTime)
        {
            m_isBlessedFlag = true;
            m_isWateringFlag = false;

            m_progressBar.FinishProgress();

            m_flowerBlessed.SetActive(true);
            m_flowerWither.SetActive(false);

            m_miniGame.GetComponent<BlessedShower>().CheckBlessedFlowers();
        }
    }

    public bool GetIsBlessed()
    {
        return m_isBlessedFlag;
    }

    public void SetIsWatering(bool watering)
    {
        m_isWateringFlag = watering;
    }
}
