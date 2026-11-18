using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LiuPartProximity : MonoBehaviour
{
    private readonly HashSet<Collider> m_nearbyColliders =
        new HashSet<Collider>();

    private Collider m_trigger;

    public Part TargetPart { get; private set; }

    private void Awake()
    {
        // 同じオブジェクトの運動データと検知範囲を取得する。
        TargetPart = GetComponent<Part>();
        m_trigger = GetComponent<Collider>();

        if (TargetPart == null || m_trigger == null)
        {
            Debug.LogError(
                "[LiuPartProximity] PartとColliderがある親オブジェクトに追加してください。",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled)
        {
            m_nearbyColliders.Add(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // 範囲内で生成・再有効化された場合にも対応する。
        OnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        m_nearbyColliders.Remove(other);
    }

    // 指定したプレイヤーが範囲内にいるか確認する。
    public bool IsPlayerInRange(GameObject player)
    {
        if (!isActiveAndEnabled
            || player == null
            || !player.activeInHierarchy
            || m_trigger == null
            || !m_trigger.enabled
            || !m_trigger.isTrigger)
        {
            return false;
        }

        // 消滅・無効化されたコライダーの記録を除く。
        m_nearbyColliders.RemoveWhere(
            item => item == null
                || !item.enabled
                || !item.gameObject.activeInHierarchy);

        foreach (Collider item in m_nearbyColliders)
        {
            if (item.transform.IsChildOf(player.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDisable()
    {
        m_nearbyColliders.Clear();
    }
}
