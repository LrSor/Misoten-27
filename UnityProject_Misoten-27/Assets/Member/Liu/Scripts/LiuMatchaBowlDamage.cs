using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LiuMatchaSpeedJudge))]
public class LiuMatchaBowlDamage : MonoBehaviour
{
    [Header("破損設定（仮）")]
    [Tooltip("速すぎる状態が何秒連続すると破損するか")]
    [SerializeField, Min(0.1f)]
    private float m_breakAfterSeconds = 3f;

    [Header("実行中の確認用")]
    [SerializeField] private float m_overSpeedTime;
    [SerializeField] private bool m_isBroken;

    private LiuMatchaSpeedJudge m_speedJudge;
    private bool m_judgeDisabledByDamage;

    public bool IsBroken => m_isBroken;

    private void Awake()
    {
        m_speedJudge = GetComponent<LiuMatchaSpeedJudge>();

        m_overSpeedTime = 0f;
        m_isBroken = false;
        m_judgeDisabledByDamage = false;
    }

    private void LateUpdate()
    {
        // 破損後は、停止状態になっても破損を解除しない。
        if (m_isBroken)
        {
            return;
        }

        // 判定が無効な間は連続時間をリセットする。
        if (m_speedJudge == null ||
            !m_speedJudge.isActiveAndEnabled)
        {
            m_overSpeedTime = 0f;
            return;
        }

        // 速すぎる状態が途切れたら、連続時間をゼロにする。
        if (m_speedJudge.CurrentState !=
            LiuMatchaSpeedJudge.SpeedState.TooFast)
        {
            m_overSpeedTime = 0f;
            return;
        }

        float limit = Mathf.Max(0.1f, m_breakAfterSeconds);

        m_overSpeedTime = Mathf.Min(
            m_overSpeedTime + Time.deltaTime,
            limit);

        if (m_overSpeedTime >= limit)
        {
            BreakBowl();
        }
    }

    private void BreakBowl()
    {
        // 破損処理は一度だけ実行する。
        if (m_isBroken)
        {
            return;
        }

        m_isBroken = true;

        // 判定を無効にすると、既存のOnDisableで茶筅も停止する。
        m_judgeDisabledByDamage = true;
        m_speedJudge.enabled = false;

        Debug.Log(
            $"[抹茶] 茶碗が破損しました。" +
            $"速すぎる状態が{m_overSpeedTime:F2}秒続きました。",
            this);
    }

    [ContextMenu("テスト：破損状態をリセット")]
    public void ResetForNewRound()
    {
        m_overSpeedTime = 0f;
        m_isBroken = false;

        if (m_speedJudge == null)
        {
            m_speedJudge = GetComponent<LiuMatchaSpeedJudge>();
        }

        // このスクリプトが無効にした場合だけ元へ戻す。
        if (m_judgeDisabledByDamage && m_speedJudge != null)
        {
            m_speedJudge.enabled = true;
        }

        m_judgeDisabledByDamage = false;

        if (m_speedJudge != null)
        {
            m_speedJudge.SetSpeed(0f);
        }

        LiuMatchaWhiskMotion motion =
            GetComponent<LiuMatchaWhiskMotion>();

        if (motion != null)
        {
            motion.ResetMotion();
        }
    }

    private void OnDisable()
    {
        // 無効化では入力を再開させない。
        if (!m_isBroken)
        {
            m_overSpeedTime = 0f;
        }
    }
}
