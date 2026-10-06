using System.Collections.Generic;
using System;
using UnityEngine;

public class Axe : MiniGameObject
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joyconL;
    //private Joycon m_joyconR;
    private Joycon.Button? m_pressedButtonL;
    //private Joycon.Button? m_pressedButtonR;
    //============================================================

    [SerializeField] MiniGame m_miniGame;
    [SerializeField] GameObject m_gameClearObject;

    public override void Activate()
    {
        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joyconL = m_joycons.Find(c => c.isLeft);
        //m_joyconR = m_joycons.Find(c => !c.isLeft);
        //============================================================
    }

    public override void Deactivate()
    {
        m_joyconL = null;

        transform.localEulerAngles = Vector3.zero;
    }

    public override void UpdateObject()
    {
        //--------------------------------------------
        // JoyConの加速度を取得
        //--------------------------------------------
        Vector3 accel = m_joyconL.GetAccel();
        // accel.y が 1.0 に近いほど「振り下ろし」と判定

        //--------------------------------------------
        // 加速度に応じてオブジェクトの角度を補正
        //--------------------------------------------
        // y軸加速度が 1 の時 → z軸角度 -95°
        // 0 の時 → z軸角度 0°
        // 線形補間で滑らかに変化させる
        float targetZ = Mathf.Lerp(0f, -95f, Mathf.Clamp01(-accel.y));

        // 現在角度からスムーズに補正（好みで調整）
        float smoothZ = Mathf.LerpAngle(transform.localEulerAngles.z, targetZ, Time.deltaTime * 10f);

        transform.localEulerAngles = new Vector3(
            transform.localEulerAngles.x,
            transform.localEulerAngles.y,
            smoothZ
        );
    }

    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject == m_gameClearObject)
        {
            Debug.Log("col");
            m_miniGame.FinishMiniGame();
        }
    }
}
