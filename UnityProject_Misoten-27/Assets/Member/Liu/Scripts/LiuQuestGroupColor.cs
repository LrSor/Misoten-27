using TMPro;
using UnityEngine;

public class LiuQuestGroupColor : MonoBehaviour
{
    [Tooltip("クエストの上に表示する感嘆符のテキスト")]
    [SerializeField] private TMP_Text questMark;

    private Quest m_quest;
    private LIU_QUEST_STATUS m_questStatus;

    private void Awake()
    {
        // 同じオブジェクトのクエストと受注状態を取得する。
        m_quest = GetComponent<Quest>();
        m_questStatus = GetComponent<LIU_QUEST_STATUS>();
    }

    private void LateUpdate()
    {
        if (m_quest == null || m_quest.Data == null
            || questMark == null)
        {
            return;
        }

        Color markColor = GetGroupColor(m_quest.Data.group);

        // 受注中はグループカラーを暗くする。
        if (m_questStatus != null && m_questStatus.IsAccepted)
        {
            markColor = Color.Lerp(markColor, Color.black, 0.65f);
        }

        if (questMark.color != markColor)
        {
            questMark.color = markColor;
        }
    }

    // グループに対応する共通カラーを返す。
    public static Color GetGroupColor(GroupType group)
    {
        switch (group)
        {
            case GroupType.GroupA:
                return Color.red;

            case GroupType.GroupB:
                return Color.blue;

            case GroupType.GroupC:
                return Color.green;

            default:
                return Color.white;
        }
    }
}
