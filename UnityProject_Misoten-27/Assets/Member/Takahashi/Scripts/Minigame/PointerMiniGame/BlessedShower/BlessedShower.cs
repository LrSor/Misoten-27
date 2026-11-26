using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlessedShower : MiniGame
{
    [Header("制限時間")]
    [SerializeField] private float m_limit;
    private float m_limitCount;
    public override float NetworkRemainingSeconds => Mathf.Max(0f, m_limitCount); // 変更 Sawada

    [Header("制限時間表示テキストUI")]
    [SerializeField] private Text m_limitText;

    [Header("太陽用光源オブジェクト")]
    [SerializeField] GameObject m_sun;

    [Header("花壇オブジェクト")]
    [SerializeField] FlowerBed[] m_flowerBeds;


    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);
        m_sun.SetActive(true);
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

        m_sun.SetActive(true);
    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        //以下に必要な処理を追加
        m_limitText.gameObject.SetActive(false);

        //m_sun.SetActive(false);
    }

    public void CheckBlessedFlowers()
    {
        foreach(FlowerBed flowerBed in m_flowerBeds)
        {
            if(!flowerBed.GetIsBlessed())
            {
                return;
            }
        }

        //終了処理
        //ミニゲーム終了フラグ
        FinishMiniGame(true);

        //スコア取得(今回は残り時間*100の小数点以下切り捨てとする)
        float score = m_limitCount * 100;
        SetScore((int)score);
    }

    public void NotWaterHit(FlowerBed hitFlowerBed)
    {
        foreach(FlowerBed flowerBed in m_flowerBeds)
        {
            if(flowerBed == hitFlowerBed)
            {
                break;
            }

            flowerBed.SetIsWatering(false);
        }
    }
}
