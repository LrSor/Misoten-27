using System.Collections.Generic;
using UnityEngine;

public class LiuQuestPanelUI : MonoBehaviour
{
    [Tooltip("表示・非表示を切り替えるクエスト情報パネル")]
    [SerializeField] private GameObject panelRoot;

    // プレイヤーが範囲内にいるクエストを記録する
    private readonly HashSet<Quest> nearbyQuests = new HashSet<Quest>();

    private void Awake()
    {
        // 開始時は範囲内のクエストがないため、パネルを非表示にする
        RefreshPanel();
    }

    // 検知スクリプトから、範囲への出入りを通知する
    public void SetQuestInRange(Quest quest, bool isInRange)
    {
        if (quest == null)
        {
            return;
        }

        if (isInRange)
        {
            nearbyQuests.Add(quest);
        }
        else
        {
            nearbyQuests.Remove(quest);
        }

        RefreshPanel();
    }

    private void LateUpdate()
    {
        // 消滅・非アクティブ化したクエストを記録から除く
        int removedCount = nearbyQuests.RemoveWhere(
            quest => quest == null || !quest.gameObject.activeInHierarchy
        );

        if (removedCount > 0)
        {
            RefreshPanel();
        }
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        // 範囲内にクエストが一つ以上ある場合だけ表示する
        bool shouldShow = nearbyQuests.Count > 0;

        if (panelRoot.activeSelf != shouldShow)
        {
            panelRoot.SetActive(shouldShow);
        }
    }
}
