using UnityEngine;

//==================================================
// フィールドの目印（出現地点のマーカー・グリッド線）の表示を切り替える
//
// ・TestField（フィールドツールで作ったフィールドのいちばん上）に付ける
// ・編集中：種類ごとに表示・非表示を選べる（Inspector のチェック）
// ・再生中：マーカーを自動で隠す（ゲーム画面には出さない）
//   ※ 再生をやめると元の表示に戻る
//
// ・消すのは見た目（Renderer）だけ。出現地点そのものは残るので、
//   クエストや部品の出現には影響しない
// ・安井さんの CustomSpawnPoint が出している Scene ビューの文字や球は
//   ここでは消せない → Scene ビュー右上の「Gizmos」の一覧で
//   CustomSpawnPoint のチェックを外すと消える
//==================================================
[ExecuteAlways]
public class FIELD_MARKER_VIEW : MonoBehaviour
{
    [Header("編集中に表示するもの")]
    [Tooltip("プレイヤーの出現地点（白）")]
    [SerializeField]
    private bool m_showPlayer = true;

    [Tooltip("クエストの出現地点（グループの色・大きい円）")]
    [SerializeField]
    private bool m_showQuest = true;

    [Tooltip("部品の出現地点（グループの色・小さい円）")]
    [SerializeField]
    private bool m_showPart = true;

    [Tooltip("NPC の出現地点（灰色）")]
    [SerializeField]
    private bool m_showNpc = true;

    [Tooltip("地面のグリッド線（10m ごと）")]
    [SerializeField]
    private bool m_showGrid = true;

    [Header("再生中")]
    [Tooltip("再生中は出現地点のマーカーを隠す")]
    [SerializeField]
    private bool m_hideMarkersInPlay = true;

    [Tooltip("再生中はグリッド線も隠す")]
    [SerializeField]
    private bool m_hideGridInPlay = false;

    // フィールドツールが作る名前
    const string PLAYER_PATH = "SpawnPoints/Player";
    const string QUEST_PATH = "SpawnPoints/Quest";
    const string PART_PATH = "SpawnPoints/Part";
    const string NPC_PATH = "SpawnPoints/NPC";
    const string GRID_PATH = "Grid";

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        bool hideMarkers =
            Application.isPlaying && m_hideMarkersInPlay;

        bool hideGrid =
            Application.isPlaying && m_hideGridInPlay;

        SetVisible(PLAYER_PATH, m_showPlayer && !hideMarkers);
        SetVisible(QUEST_PATH, m_showQuest && !hideMarkers);
        SetVisible(PART_PATH, m_showPart && !hideMarkers);
        SetVisible(NPC_PATH, m_showNpc && !hideMarkers);
        SetVisible(GRID_PATH, m_showGrid && !hideGrid);
    }

    void SetVisible(string path, bool isVisible)
    {
        Transform target = transform.Find(path);

        if (target == null)
        {
            return;
        }

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled != isVisible)
            {
                renderer.enabled = isVisible;
            }
        }
    }
}
