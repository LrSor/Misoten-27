using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

//ミニゲーム基底クラス
public class MiniGame : MonoBehaviour
{
    //============================================================
    // JoyCon関連
    //============================================================
    //protected static readonly Joycon.Button[] m_buttons =
    //    Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    //protected List<Joycon> m_joycons;
    //protected Joycon m_joyconL;
    ////protected Joycon m_joyconR;
    //protected Joycon.Button? m_pressedButtonL;
    ////protected Joycon.Button? m_pressedButtonR;
    //============================================================

    protected bool m_isActive = false;
    protected bool m_isFinish = false;
    protected int m_score = 0;

    [SerializeField] MiniGameObject[] m_objects;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        ////============================================================
        //// JoyCon初期化
        ////============================================================
        //m_joycons = JoyconManager.Instance.j;
        
        //if (m_joycons == null || m_joycons.Count <= 0) return;
        
        //m_joyconL = m_joycons.Find(c => c.isLeft);
        ////m_joyconR = m_joycons.Find(c => !c.isLeft);
        ////============================================================
    }
    public virtual void Activate()
    {
        //変数初期化
        m_isActive = true;
        m_isFinish = false;
        m_score = 0;

        //ミニゲーム用オブジェクトの初期化
        foreach (var obj in m_objects)
        {
            obj.Activate();
        }
    }

    public virtual void Deactivate()
    {
        //ミニゲーム実行中のフラグを偽に変更
        m_isActive = false;

        //ミニゲーム用オブジェクトの終了処理
        foreach (var obj in m_objects)
        {
            obj.Deactivate();
        }
    }

    public void FinishMiniGame()
    {
        //ミニゲーム終了フラグ
        m_isFinish = true;
    }

    public bool GetIsFinish()
    {
        return m_isFinish;
    }

    public int GetScore()
    {
        return m_score;
    }

    public void SetScore(int score)
    {
        m_score = score;
    }

    void Update()
    {
        //ミニゲーム実行中フラグがたっている場合、ミニゲーム更新関数を実行
        if (!m_isActive)
        {
            return;
        }

        UpdateMiniGame();

    }

    protected virtual void UpdateMiniGame()
    {
        // ミニゲーム用のゲームマネージャー的役割
        // 制限時間のタイマーとかはここでいいかも

        //ミニゲーム用オブジェクトの更新
        foreach (var obj in m_objects)
        {
            obj.UpdateObject();
        }
    }
}
