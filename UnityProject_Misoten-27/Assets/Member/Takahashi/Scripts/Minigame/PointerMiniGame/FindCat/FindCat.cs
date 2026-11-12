using UnityEngine;
using UnityEngine.UI;

public class FindCat : MiniGame
{
    [Header("制限時間")]
    [SerializeField] private float m_limit;
    private float m_limitCount;

    [Header("制限時間表示テキストUI")]
    [SerializeField] private Text m_limitText;

    [Header("黒背景")]
    [SerializeField] private Image m_backGround;
    [Header("窓枠")]
    [SerializeField] private Image m_frame;


    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        //以下に必要な処理を追加
        m_limitCount -= Time.deltaTime;

        m_limitText.text = m_limitCount.ToString("Time Limit: 00");

        if (m_limitCount <= 0)
        {
            //ミニゲーム終了フラグ
            FinishMiniGame(false);

            //回転数*10をスコアとして取得
            SetScore(0);
        }

    }

    public override void Activate()
    {
        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        //以下に必要な処理を追加
        m_limitCount = m_limit;
        m_limitText.text = m_limitCount.ToString("Time Limit: 00");

        m_limitText.gameObject.SetActive(true);
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);
    }
}
