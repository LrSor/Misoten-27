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
    //============================================================

    private bool m_isActiveFlag = false;
    private bool m_isFinishFlag = false;
    private int m_score = 0;
    private bool m_isGameClearFlag = true;


    // ============================================================ 追加 Sawada
    // 星評価（未設定なら星3固定）
    protected int? m_stars = null;

    // 終了時に通信管理が読み取り、参加者のUIへ渡す
    public virtual int? NetworkStars => m_stars;

    // 残り時間（負数なら時間なし）
    protected float m_remainingSeconds = -1f;

    // 通信管理が定期的に読み取り、参加者のUIへ渡す
    public virtual float NetworkRemainingSeconds => m_remainingSeconds;

    [SerializeField] MiniGameObject[] m_objects;

    public bool IsNetworkControlled { get; private set; }
    public bool IsNetworkAuthority { get; private set; }

    public bool IsActive => m_isActiveFlag;
    public MiniGameObject[] NetworkObjects => m_objects ?? Array.Empty<MiniGameObject>();
    public MiniGameNetworkInput NetworkInput { get; private set; }
    public void ConfigureNetwork(bool controlled, bool authority)
    {
        if (IsNetworkControlled == controlled && IsNetworkAuthority == authority) return;
        IsNetworkControlled = controlled;
        IsNetworkAuthority = authority;
        NetworkInput = default;
        foreach (var obj in NetworkObjects)
            if (obj != null) obj.ConfigureNetwork(controlled, authority);
    }
    public void SetNetworkInput(MiniGameNetworkInput input)
    {
        NetworkInput = input;
        foreach (var obj in NetworkObjects)
            if (obj != null) obj.SetNetworkInput(input);
    }
    // ============================================================


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
        if (IsNetworkControlled && !IsNetworkAuthority) return;
        m_isActiveFlag = true;
        m_isGameClearFlag = true;
        m_isFinishFlag = false;
        m_score = 0;

        //ミニゲーム用オブジェクトの初期化
        foreach (var obj in NetworkObjects)
        {
            if (obj != null) obj.Activate();
        }
    }

    public virtual void Deactivate()
    {
        //ミニゲーム実行中のフラグを偽に変更
        m_isActiveFlag = false;

        //ミニゲーム用オブジェクトの終了処理
        foreach (var obj in NetworkObjects)
        {
            if (obj != null) obj.Deactivate();
        }
    }

    public void FinishMiniGame(bool isClear = true) //引数でゲームクリアの成否を取得
    {
        if (IsNetworkControlled && (!IsNetworkAuthority || !m_isActiveFlag || m_isFinishFlag)) return;
        //ミニゲーム終了フラグ
        m_isFinishFlag = true;

        //ゲームクリアフラグ
        m_isGameClearFlag = isClear;
    }

    public bool GetIsFinish()
    {
        return m_isFinishFlag;
    }

    public int GetScore()
    {
        return m_score;
    }

    public void SetScore(int score)
    {
        if (IsNetworkControlled && (!IsNetworkAuthority || !m_isActiveFlag)) return;
        m_score = score;
    }

    public bool GetIsGameClear()
    {
        return m_isGameClearFlag;
    }

    void Update()
    {
        //ミニゲーム実行中フラグがたっている場合、ミニゲーム更新関数を実行
        if (!m_isActiveFlag || m_isFinishFlag || (IsNetworkControlled && !IsNetworkAuthority))
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
        foreach (var obj in NetworkObjects)
        {
            if (obj != null) obj.UpdateObject();
        }
    }
}
