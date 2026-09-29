using UnityEngine;
using UnityEngine.InputSystem;

public class GameTester : MonoBehaviour
{
    private void Update()
    {
        // キーボードが接続されていない場合は処理しない
        if (Keyboard.current == null) return;

        // 【テスト1】[数字キー 1] で GroupA のクエストを強制的クリアしてみる
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SimulateClearQuest(GroupType.GroupA);
        }

        // 【テスト2】[数字キー 2] で GroupB のクエストを強制的クリアしてみる
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SimulateClearQuest(GroupType.GroupB);
        }

        // 【テスト3】[数字キー 3] で GroupC のクエストを強制的クリアしてみる
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SimulateClearQuest(GroupType.GroupC);
        }

        // 【テスト4】[数字キー 4] で GroupD のクエストを強制的クリアしてみる
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SimulateClearQuest(GroupType.GroupD);
        }

        // 【テスト5】[数字キー 5] で GroupE のクエストを強制的クリアしてみる
        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            SimulateClearQuest(GroupType.GroupE);
        }

        // 【テスト6】[数字キー 6] で GroupA の部品を取得してみる
        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            SimulatePickUpPart(GroupType.GroupA);
        }

        // 【テスト7】[数字キー 7] で GroupB の部品を取得してみる
        if (Keyboard.current.digit7Key.wasPressedThisFrame)
        {
            SimulatePickUpPart(GroupType.GroupB);
        }

        // 【テスト8】[数字キー 8] で GroupC の部品を取得してみる
        if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            SimulatePickUpPart(GroupType.GroupC);
        }

        // 【テスト9】[数字キー 9] で GroupD の部品を取得してみる
        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            SimulatePickUpPart(GroupType.GroupD);
        }

        // 【テスト10】[数字キー 0] で GroupE の部品を取得してみる
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            SimulatePickUpPart(GroupType.GroupE);
        }

        // 【テスト11】[Space] キーで現在フィールドにあるクエスト一覧をログ表示
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            LogActiveStatus();
        }
    }

    // 指定したグループのクエストをクリア処理（擬似クリア）
    private void SimulateClearQuest(GroupType group)
    {
        Quest[] allQuests = FindObjectsByType<Quest>(FindObjectsSortMode.None);
        foreach (var q in allQuests)
        {
            if (q.Data != null && q.Data.group == group)
            {
                Debug.Log($"<color=yellow>【テスト】{group} のクエスト [{q.Data.questTitle}] をクリア処理します。</color>");
                q.ClearQuest();
                return;
            }
        }
        Debug.LogWarning($"フィールド上に {group} のクエストが見つかりませんでした。");
    }

    // 指定したグループのパーツを取得処理（擬似拾い＆再生成）
    private void SimulatePickUpPart(GroupType group)
    {
        Part[] allParts = FindObjectsByType<Part>(FindObjectsSortMode.None);
        foreach (var p in allParts)
        {
            if (p.Data != null && p.Data.group == group)
            {
                Debug.Log($"<color=cyan>【テスト】{group} のパーツ [{p.Data.partName}] (ID: {p.Data.partID}) をプレイヤーが取得しました。</color>");
                p.PickUp(); // Part.cs 内でイベント通知 ➔ PartManager が自動再生成
                return;
            }
        }
        Debug.LogWarning($"フィールド上に {group} のパーツが見つかりませんでした。");
    }

    // 現在のフィールド状態を出力
    private void LogActiveStatus()
    {
        Quest[] quests = FindObjectsByType<Quest>(FindObjectsSortMode.None);
        Part[] parts = FindObjectsByType<Part>(FindObjectsSortMode.None);

        Debug.Log("================ 現在のフィールド状態 ================");

        // クエスト一覧
        Debug.Log($"<color=yellow>・アクティブなクエスト数: {quests.Length} 個 (目標: 5個)</color>");
        foreach (var q in quests)
        {
            Debug.Log($"   └ [{q.Data.group}] {q.Data.questTitle} (位置: {q.transform.position})");
        }

        // パーツ一覧
        Debug.Log($"<color=cyan>・アクティブなパーツ数: {parts.Length} 個</color>");
        foreach (var p in parts)
        {
            Debug.Log($"   └ [{p.Data.group}] {p.Data.partName} (位置: {p.transform.position})");
        }
        Debug.Log("=================================================");
    }
}
