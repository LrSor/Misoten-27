using System.Collections.Generic;
using UnityEngine;

//==================================================
// 移動時間を計るためのゲート（フィールドの検証用）
//
// ・フィールドの端と端に1つずつ置く（トリガーの Collider が必要）
// ・プレイヤーがどちらかのゲートを通ると計測開始
//   → もう一方のゲートを通ると、かかった時間を Console に表示
// ・どちら向きに走っても計れる。同じゲートを通り直すと計り直し
//==================================================
[RequireComponent(typeof(Collider))]
public class FIELD_TIME_GATE : MonoBehaviour
{
    [Tooltip("このタグのオブジェクトだけ計測する。空欄なら Rigidbody を持つもの全部")]
    [SerializeField]
    private string m_targetTag = "Player";

    // 計測中の情報（誰が・どのゲートから・いつ）
    private struct RECORD
    {
        public FIELD_TIME_GATE m_startGate;
        public float m_startTime;
    }

    private static readonly Dictionary<GameObject, RECORD> s_records =
        new Dictionary<GameObject, RECORD>();

    // ドメインリロード無しで再生した場合に前回の記録が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearRecords()
    {
        s_records.Clear();
    }

    // コンポーネントを付けたときに自動でトリガーにする
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // 体の一部（子オブジェクト）が当たっても本体として扱う
        GameObject target =
            other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

        if (!string.IsNullOrEmpty(m_targetTag) &&
            !target.CompareTag(m_targetTag))
        {
            return;
        }

        RECORD record;

        // 別のゲートから計測中なら、ここでゴール
        if (s_records.TryGetValue(target, out record) &&
            record.m_startGate != this)
        {
            float elapsed = Time.time - record.m_startTime;

            float distance =
                Vector3.Distance(
                    record.m_startGate.transform.position,
                    transform.position);

            Debug.Log(
                "【移動時間】" + target.name + "：" +
                record.m_startGate.name + " → " + name + "　" +
                elapsed.ToString("0.00") + " 秒（ゲート間 " +
                distance.ToString("0.0") + " m、平均 " +
                (distance / Mathf.Max(elapsed, 0.01f)).ToString("0.0") + " m/秒）");

            s_records.Remove(target);
            return;
        }

        // 計測開始（同じゲートを通り直した場合は計り直し）
        record.m_startGate = this;
        record.m_startTime = Time.time;

        s_records[target] = record;

        Debug.Log(
            "【移動時間】" + target.name + "：" + name + " から計測開始");
    }
}
