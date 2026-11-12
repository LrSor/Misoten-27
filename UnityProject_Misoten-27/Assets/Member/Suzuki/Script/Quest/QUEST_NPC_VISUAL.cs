using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

//==================================================
// 出てきたクエストの見た目を NPC にする
//
// ・Liu さんの LiuQuestManager がクエスト（「!」付きの箱）を出したら、
//   その場所に NPC（NPC_Quest）を立たせて、箱を隠す
// ・「!」マークは NPC の頭の上に移す
// ・NPC はクエストの子にするので、クエストをクリアすると一緒に消える
//
// ・シーンに 1 つ置く（QuestSystem に付けている）
// ・Liu さんのプレハブやデータは変更していない（出てきたものを、その場で見た目だけ変えている）
// ・近づいたかの判定（LiuQuestProximity）や挑戦（QUEST_CHALLENGE）はそのまま使える
//==================================================
public class QUEST_NPC_VISUAL : MonoBehaviour
{
    [Tooltip("クエストの場所に立たせる NPC")]
    [SerializeField]
    private GameObject m_npcPrefab;

    [Tooltip("NPC の中心の高さ（NPC_Quest は高さ 2m のカプセルなので 1）")]
    [SerializeField]
    private float m_npcCenterHeight = 1f;

    [Tooltip("「!」マークの高さ（地面から）")]
    [SerializeField]
    private float m_markHeight = 2.8f;

    [Tooltip("元のクエストの箱を隠す")]
    [SerializeField]
    private bool m_hideQuestBox = true;

    // NPC を付け終わったクエスト
    private readonly HashSet<Quest> m_decoratedQuests =
        new HashSet<Quest>();

    // クエストは LiuQuestManager の Start や、クリアされたときに出てくるので、毎フレーム確認する
    void LateUpdate()
    {
        if (m_npcPrefab == null)
        {
            return;
        }

        m_decoratedQuests.RemoveWhere(quest => quest == null);

        Quest[] quests =
            FindObjectsByType<Quest>(FindObjectsSortMode.None);

        foreach (Quest quest in quests)
        {
            if (m_decoratedQuests.Add(quest))
            {
                Decorate(quest);
            }
        }
    }

    void Decorate(Quest quest)
    {
        Transform questTransform = quest.transform;

        // クエストが出た地点（地面の高さ）
        Vector3 groundPosition = questTransform.position;

        // NPC を立たせる（クエストの子にして、クリアで一緒に消えるようにする）
        GameObject npc =
            Instantiate(
                m_npcPrefab,
                groundPosition + Vector3.up * m_npcCenterHeight,
                questTransform.rotation,
                questTransform);

        npc.name = "NPC_Quest";

        // 立っているだけなので、歩く仕組みは止める
        // （有効のままだと、NavMesh の上に勝手に移動してしまう）
        foreach (NavMeshAgent agent in npc.GetComponentsInChildren<NavMeshAgent>(true))
        {
            agent.enabled = false;
        }

        foreach (NPC_BASE npcScript in npc.GetComponentsInChildren<NPC_BASE>(true))
        {
            npcScript.enabled = false;
        }

        // 元の箱（見た目と、ぶつかる当たり判定）を隠す
        // 近づいたかを調べる範囲（トリガー）は残す
        if (m_hideQuestBox)
        {
            Renderer boxRenderer = quest.GetComponent<Renderer>();

            if (boxRenderer != null)
            {
                boxRenderer.enabled = false;
            }

            foreach (Collider collider in quest.GetComponents<Collider>())
            {
                if (!collider.isTrigger)
                {
                    collider.enabled = false;
                }
            }
        }

        // 「!」マークを NPC の頭の上に移す
        foreach (TMP_Text mark in quest.GetComponentsInChildren<TMP_Text>(true))
        {
            if (mark.transform.IsChildOf(npc.transform))
            {
                continue;
            }

            Vector3 markPosition = mark.transform.position;
            markPosition.x = groundPosition.x;
            markPosition.z = groundPosition.z;
            markPosition.y = groundPosition.y + m_markHeight;

            mark.transform.position = markPosition;
        }
    }
}
