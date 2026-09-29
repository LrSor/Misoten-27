using UnityEngine;

public class CustomSpawnPoint : MonoBehaviour
{
    public enum PointType { Quest, Part }

    [Header("ポイントの種類")]
    public PointType pointType = PointType.Quest;

    [Header("クエスト用設定")]
    public GroupType groupType = GroupType.GroupA;

    [Header("ラベル表示名")]
    public string pointName = "Point 1";
    [Range(8, 30)] public int fontSize = 14;

    // Gizmos（Sceneビュー上での表示描画）
    private void OnDrawGizmos()
    {
        // 1. タイプやグループに応じた色の設定
        Gizmos.color = GetColor();

        // 2. 位置に球体（アイコン）を描画
        Gizmos.DrawSphere(transform.position, 0.25f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        // 3. UnityEditor上でのテキストラベル表示（エディタ再生前でも表示）
#if UNITY_EDITOR
        string labelText = (pointType == PointType.Quest)
            ? $"[{groupType}] {pointName}"
            : $"[Part] {pointName}";

        GUIStyle style = new GUIStyle();
        style.normal.textColor = GetColor();
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = fontSize;

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, labelText, style);
#endif
    }

    private Color GetColor()
    {
        if (pointType == PointType.Part) return Color.cyan; // 部品はシアン色

        // クエストグループ別の色分け
        switch (groupType)
        {
            case GroupType.GroupA: return Color.red;      // GroupA: 赤
            case GroupType.GroupB: return Color.blue;     // GroupB: 青
            case GroupType.GroupC: return Color.green;    // GroupC: 緑
            case GroupType.GroupD: return Color.yellow;   // GroupD: 黄
            case GroupType.GroupE: return Color.magenta;  // GroupE: マゼンタ
            default: return Color.white;
        }
    }
}
