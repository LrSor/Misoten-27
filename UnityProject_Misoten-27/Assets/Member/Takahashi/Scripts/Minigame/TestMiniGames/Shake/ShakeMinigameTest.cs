using UnityEngine;
using UnityEngine.UI;

public class ShakeMinigameTest : MiniGame
{
    [Header("制限時間")]
    [SerializeField] private float m_limit;
    private float m_limitCount;

    [Header("振るオブジェクト")]
    [SerializeField] private ShakeObject m_shakeObject;

    [Header("制限時間表示テキストUI")]
    [SerializeField] private Text m_limitText;
    [Header("振った回数表示テキストUI")]
    [SerializeField] private Text m_shakedCountText;

    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        m_limitText.gameObject.SetActive(false);
        m_shakedCountText.gameObject.SetActive(false);
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        m_limitCount -= Time.deltaTime;

        m_limitText.text = m_limitCount.ToString("Time Limit: 00");
        m_shakedCountText.text = m_shakeObject.GetShakedCount().ToString("Shaked Count: 00");

        if (m_limitCount <= 0)
        {
            //ミニゲーム終了フラグ
            FinishMiniGame();

            //回転数*10をスコアとして取得
            SetScore(m_shakeObject.GetShakedCount() * 10);
        }
    }

    public override void Activate()
    {
        base.Activate();

        m_limitCount = m_limit;

        //カウントUI初期化
        m_limitText.text = m_limitCount.ToString("Time Limit: 00");
        m_shakedCountText.text = m_shakeObject.GetShakedCount().ToString("shakedd Count: 00");

        m_limitText.gameObject.SetActive(true);
        m_shakedCountText.gameObject.SetActive(true);
    }

    public override void Deactivate()
    {
        base.Deactivate();

        m_limitText.gameObject.SetActive(false);
        m_shakedCountText.gameObject.SetActive(false);
    }
}
