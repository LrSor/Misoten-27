using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FindCat : MiniGame
{
    [Header("制限時間")]
    [SerializeField] private float m_limit;
    private float m_limitCount;

    [Header("制限時間表示テキストUI")]
    [SerializeField] private Text m_limitText;

    [Header("黒猫オブジェクト")]
    [SerializeField] BlackCat m_blackCat;

    [Header("ヒント:目が光るタイミング")]
    [SerializeField] List<int> m_eyeGlowTiming;

    [Header("黒背景")]
    [SerializeField] private Image m_backGround;
    [Header("窓枠")]
    [SerializeField] private Image m_frame;
    [Header("テスト:ライト用イラスト")]
    [SerializeField] private Image m_light;
    [Header("テスト:黒猫用イラスト")]
    [SerializeField] private Image m_blackCatImage;

    [Header("テスト:2Dへ切り替え")]
    [SerializeField] bool m_is2D;


    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);

        m_backGround.gameObject.SetActive(false);
        m_frame.gameObject.SetActive(false);
        m_light.gameObject.SetActive(false);
        m_blackCatImage.gameObject.SetActive(false);
    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        //以下に必要な処理を追加
        m_limitCount -= Time.deltaTime;

        m_limitText.text = m_limitCount.ToString("Time Limit: 00");

        //ヒントの表示
        //猫の目を照らす
        foreach (int timing in m_eyeGlowTiming)
        {
            if (timing == (int)m_limitCount && !m_blackCat.GetIsGlowing())
            {
                m_blackCat.GlowEye();
                Debug.Log("目が光った！");
                break;
            }
        }

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


        if (!m_is2D)
            return;
        m_backGround.gameObject.SetActive(true);
        m_frame.gameObject.SetActive(true);
        m_light.gameObject.SetActive(true);
        m_blackCatImage.gameObject.SetActive(true);
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);

        m_backGround.gameObject.SetActive(false);
        m_frame.gameObject.SetActive(false);
        m_light.gameObject.SetActive(false);
        m_blackCatImage.gameObject.SetActive(false);
    }

    public float GetTime()
    {
        return m_limitCount;
    }
}
