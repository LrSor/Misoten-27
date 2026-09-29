using UnityEngine;
using System;

public class Quest : MonoBehaviour
{
    public QuestData Data { get; private set; }

    // クエストクリア時に発行するイベント（引数：クリアされたQuest）
    public static event Action<Quest> OnQuestCleared;

    public void Initialize(QuestData data)
    {
        Data = data;
    }

    // クエストクリア時に呼ぶ関数（ミニゲーム成功時など）
    public void ClearQuest()
    {
        // クリアイベントを発行
        OnQuestCleared?.Invoke(this);

        // オブジェクトの破棄（プール運用にする場合は非アクティブ化）
        Destroy(gameObject);
    }

    // ギズモ
    private void OnDrawGizmos()
    {
        if (Data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);

#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 8;

        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"【Quest】\n{Data.questTitle}\n({Data.group})", style);
#endif
    }
}
