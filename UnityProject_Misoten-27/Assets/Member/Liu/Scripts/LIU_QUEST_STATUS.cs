using UnityEngine;

public class LIU_QUEST_STATUS : MonoBehaviour
{
    [Header("受注状態")]
    [SerializeField] private bool m_acceptFlag;

    private GameObject m_ownerPlayer;

    // このクエストが受注されているか。
    public bool IsAccepted => m_acceptFlag;

    // このクエストを受注したプレイヤー。
    public GameObject OwnerPlayer => m_ownerPlayer;

    // 未受注の場合のみ、指定プレイヤーが受注する。
    public bool TryAccept(GameObject player)
    {
        if (player == null || m_acceptFlag)
        {
            return false;
        }

        m_ownerPlayer = player;
        m_acceptFlag = true;
        return true;
    }

    // 終了・キャンセル時に受注状態を解除する。
    public void ReleaseQuest()
    {
        m_ownerPlayer = null;
        m_acceptFlag = false;
    }
}
