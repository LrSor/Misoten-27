using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LiuMatchaWhiskMotion),
                  typeof(LiuMatchaSpeedJudge),
                  typeof(LiuMatchaBowlDamage))]
public class LiuMatchaWhiskController : MiniGameObject
{
    private LiuMatchaWhiskMotion m_motion;
    private LiuMatchaSpeedJudge m_speedJudge;
    private LiuMatchaBowlDamage m_bowlDamage;

    [Tooltip("今回かき混ぜた回転数（確認用）")]
    [SerializeField] private float m_stirredTurns;
    public float StirredTurns => m_stirredTurns;
    private void Awake()
    {
        m_motion = GetComponent<LiuMatchaWhiskMotion>();
        m_speedJudge = GetComponent<LiuMatchaSpeedJudge>();
        m_bowlDamage = GetComponent<LiuMatchaBowlDamage>();

        // 開始の合図までは茶筅の操作を止める。
        SetWhiskEnabled(false);
    }

    public override void Activate()
    {
        base.Activate();

        m_stirredTurns = 0f;

        // 前回の破損と回転速度をリセットしてから操作を開始する。
        m_bowlDamage.ResetForNewRound();
        SetWhiskEnabled(true);
    }

    public override void UpdateObject()
    {
        base.UpdateObject();

        if (m_bowlDamage.IsBroken)
            return;

        // 回転速度（周／秒）に経過時間を掛けて、回転数を加算する。
        m_stirredTurns += m_speedJudge.CurrentSpeed * Time.deltaTime;
    }

    public override void Deactivate()
    {
        base.Deactivate();
        SetWhiskEnabled(false);
    }

    private void SetWhiskEnabled(bool enabledForRound)
    {
        m_motion.enabled = enabledForRound;
        m_speedJudge.enabled = enabledForRound;
        m_bowlDamage.enabled = enabledForRound;
    }
}
