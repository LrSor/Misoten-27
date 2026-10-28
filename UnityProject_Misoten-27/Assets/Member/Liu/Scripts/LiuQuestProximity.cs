using System.Collections.Generic;
using UnityEngine;

public class LiuQuestProximity : MonoBehaviour
{
    private Quest ownerQuest;
    private LiuQuestPanelUI panelUI;

    // 範囲内にいるプレイヤーのコライダーを記録する
    private readonly HashSet<Collider> playerColliders =
        new HashSet<Collider>();

    private void Awake()
    {
        // 親オブジェクトのクエストと、シーン内のUIを取得する
        ownerQuest = GetComponentInParent<Quest>();
        panelUI = FindFirstObjectByType<LiuQuestPanelUI>();

        if (ownerQuest == null || panelUI == null)
        {
            Debug.LogError(
                "[LiuQuestProximity] Quest または LiuQuestPanelUI が見つかりません。",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 範囲内で生成された場合や、再有効化された場合にも対応する
        RegisterPlayer(other);
    }

    private void RegisterPlayer(Collider other)
    {
        if (!isActiveAndEnabled || !IsPlayer(other))
        {
            return;
        }

        // 同じコライダーは重複して登録しない
        if (playerColliders.Add(other))
        {
            NotifyPanel();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (playerColliders.Remove(other))
        {
            NotifyPanel();
        }
    }

    private void LateUpdate()
    {
        // 消滅・無効化されたプレイヤーの記録を取り除く
        int removedCount = playerColliders.RemoveWhere(
            collider => collider == null
                || !collider.enabled
                || !collider.gameObject.activeInHierarchy
                || !IsPlayer(collider)
        );

        if (removedCount > 0)
        {
            NotifyPanel();
        }
    }

    private bool IsPlayer(Collider other)
    {
        PlayerSpawnManager manager = PlayerSpawnManager.Instance;

        if (manager == null || other == null)
        {
            return false;
        }

        // PlayerSpawnManagerが生成したプレイヤーだけを対象にする
        foreach (GameObject player in manager.SpawnedPlayers)
        {
            if (player != null
                && other.transform.IsChildOf(player.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyPanel()
    {
        if (ownerQuest != null && panelUI != null)
        {
            panelUI.SetQuestInRange(
                ownerQuest,
                playerColliders.Count > 0
            );
        }
    }

    private void OnDisable()
    {
        // クエストが消えた場合も、範囲の登録を解除する
        playerColliders.Clear();
        NotifyPanel();
    }
}
