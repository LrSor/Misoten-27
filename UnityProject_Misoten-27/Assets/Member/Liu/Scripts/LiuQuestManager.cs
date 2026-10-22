using System.Collections.Generic;
using UnityEngine;

public class LiuQuestManager : BaseManager<LiuQuestManager>
{
    [System.Serializable]
    public struct GroupSpawnSet
    {
        public GroupType group;               // 対象グループ (GroupA ~ GroupE)[cite: 1]
        public List<QuestData> questPool;       // このグループに属するクエスト一覧
        public Transform[] spawnPoints;   // このグループの出現候補地点（複数）
    }

    [Header("グループごとの設定 (GroupA ~ GroupE の5つ)")]
    [SerializeField] private List<GroupSpawnSet> groupSpawnSets;

    private HashSet<Transform> usedSpawnPoints = new HashSet<Transform>();
    // クエストと記録スコアの組み合わせ
    [System.Serializable]
    public class QuestScoreEntry
    {
        // スコアを記録するクエスト
        public QuestData quest;

        // 現在の記録スコア
        public int score;
    }

    [Header("クエストごとの記録スコア")]
    [SerializeField]
    private List<QuestScoreEntry> questScores = new List<QuestScoreEntry>();
    private Dictionary<GroupType, Quest> activeQuests = new Dictionary<GroupType, Quest>();
    // 各クエストが生成された元の地点を記録する
    private Dictionary<Quest, Transform> activeQuestPointMap =
        new Dictionary<Quest, Transform>();
    private Dictionary<GroupType, Transform> lastFreedPointMap = new Dictionary<GroupType, Transform>();

    protected override void OnAwake()
    {
        // 必用に応じてシーン遷移後も残す場合は呼び出します
        // SetDontDestroyOnLoad(); 
    }

    private void OnEnable()
    {
        Quest.OnQuestCleared += HandleQuestCleared;
    }

    private void OnDisable()
    {
        Quest.OnQuestCleared -= HandleQuestCleared;
    }

    private void Start()
    {
        SpawnInitialQuests();
    }

    private void SpawnInitialQuests()
    {
        foreach (var set in groupSpawnSets)
        {
            // 各グループにクエストを2個生成する
            for (int i = 0; i < 2; i++)
            {
                SpawnQuestForGroup(set.group);
            }
        }
    }
    // 指定したクエストの記録スコアを取得する
    private int GetQuestScore(QuestData quest)
    {
        foreach (var entry in questScores)
        {
            if (entry != null && entry.quest == quest)
            {
                return entry.score;
            }
        }

        // 未登録のクエストは0点として扱う
        return 0;
    }

    // 同じグループから最低スコアのクエストを選ぶ
    private QuestData SelectLowestScoreQuest(
      GroupSpawnSet set, QuestData excludedQuest = null)
    {
        int lowestScore = int.MaxValue;
        List<QuestData> candidates = new List<QuestData>();

        foreach (var data in set.questPool)
        {
            // 未設定・別グループ・Prefab未設定のデータは除外する
            if (data == null || data.group != set.group
                || data.questPrefab == null)
            {
                continue;
            }
            // 直前にクリアした種類は候補から除外する
            if (data == excludedQuest)
            {
                continue;
            }

            // フィールドに出現中の種類は重複させない
            bool alreadySpawned = false;

            foreach (var pair in activeQuestPointMap)
            {
                if (pair.Key != null && pair.Key.Data == data)
                {
                    alreadySpawned = true;
                    break;
                }
            }

            if (alreadySpawned)
            {
                continue;
            }
            int score = GetQuestScore(data);

            // より低いスコアが見つかったら候補を入れ替える
            if (score < lowestScore)
            {
                lowestScore = score;
                candidates.Clear();
            }

            // 最低スコアと同点のクエストを候補に追加する
            if (score == lowestScore && !candidates.Contains(data))
            {
                candidates.Add(data);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // 同点の候補からランダムに選ぶ
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void SpawnQuestForGroup(
      GroupType group, QuestData excludedQuest = null)
    {
        GroupSpawnSet set = groupSpawnSets.Find(s => s.group == group);
        if (set.questPool == null || set.questPool.Count == 0 || set.spawnPoints == null || set.spawnPoints.Length == 0) return;

        lastFreedPointMap.TryGetValue(group, out Transform lastFreedPoint);

        List<Transform> availablePoints = new List<Transform>();
        foreach (var pt in set.spawnPoints)
        {
            if (pt != null && !usedSpawnPoints.Contains(pt) && pt != lastFreedPoint)
            {
                availablePoints.Add(pt);
            }
        }

        if (availablePoints.Count == 0)
        {
            foreach (var pt in set.spawnPoints)
            {
                if (pt != null && !usedSpawnPoints.Contains(pt))
                {
                    availablePoints.Add(pt);
                }
            }
        }

        Transform chosenSpawnPoint = (availablePoints.Count > 0)
            ? availablePoints[Random.Range(0, availablePoints.Count)]
            : set.spawnPoints[Random.Range(0, set.spawnPoints.Length)];

        if (chosenSpawnPoint == null) return;

        usedSpawnPoints.Add(chosenSpawnPoint);

        // 最低スコアのクエストを選択する
        QuestData selectedData = SelectLowestScoreQuest(set, excludedQuest);

        if (selectedData == null)
        {
            // 生成できない場合は予約した地点を解放する
            usedSpawnPoints.Remove(chosenSpawnPoint);
            Debug.LogWarning($"[LiuQuestManager] {group} に生成可能なクエストがありません。");
            return;
        }

        GameObject questObj = Instantiate(selectedData.questPrefab, chosenSpawnPoint.position, chosenSpawnPoint.rotation);
        Quest quest = questObj.GetComponent<Quest>();
        if (quest != null)
        {
            quest.Initialize(selectedData);
            activeQuests[group] = quest;

            // 移動後も元の生成地点を確認できるようにする
            activeQuestPointMap[quest] = chosenSpawnPoint;
        }
    }
    // 新しいスコアを記録してからクエストをクリアする
    public void CompleteQuest(Quest quest, int newScore)
    {
        if (quest == null || quest.Data == null)
        {
            return;
        }

        // このManagerが生成したクエストだけを処理する
        if (!activeQuestPointMap.ContainsKey(quest))
        {
            return;
        }

        // 対象クエストのスコア記録を探す
        QuestScoreEntry entry = questScores.Find(
            item => item != null && item.quest == quest.Data);

        // 記録がなければ新しく追加する
        if (entry == null)
        {
            entry = new QuestScoreEntry();
            entry.quest = quest.Data;
            questScores.Add(entry);
        }

        // 保存済みのスコアを上回った場合だけ更新する
        if (newScore > entry.score)
        {
            entry.score = newScore;

            Debug.Log(
                $"[LiuQuestManager] {quest.Data.questTitle} の最高スコアを {entry.score} 点に更新しました。");
        }
        else
        {
            // 同点または低いスコアの場合は、以前の記録を維持する
            Debug.Log(
                $"[LiuQuestManager] {quest.Data.questTitle} の今回のスコアは {newScore} 点です。最高スコア {entry.score} 点を維持します。");
        }
        // 更新後にイベントを発行し、次のクエストを生成する
        quest.ClearQuest();
    }
    // 実行中のAグループのクエストを60点でクリアするテスト
    [ContextMenu("テスト：Aグループを3点でクリア")]
    private void TestCompleteGroupA()
    {
        // 編集中に実行しないようにする
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Playモードで実行してください。");
            return;
        }

        // 現在出現しているAグループのクエストを取得する
        if (!activeQuests.TryGetValue(GroupType.GroupA, out Quest quest)
            || quest == null)
        {
            Debug.LogWarning("GroupAのクエストが見つかりません。");
            return;
        }

        // 成績の更新とクリア処理をまとめて実行する
        CompleteQuest(quest, 3);
    }
    // クエストを移動させてからクリアし、生成地点の管理を確認する
    [ContextMenu("テスト：Aグループを移動してクリア")]
    private void TestMoveAndCompleteGroupA()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Playモードで実行してください。");
            return;
        }

        // 現在のクエストと元の生成地点を取得する
        if (!activeQuests.TryGetValue(GroupType.GroupA, out Quest quest)
            || quest == null
            || !activeQuestPointMap.TryGetValue(quest, out Transform originalPoint)
            || originalPoint == null)
        {
            Debug.LogWarning("GroupAのクエストまたは生成地点が見つかりません。");
            return;
        }

        // 生成地点を残したまま、クエスト本体だけを移動する
        quest.transform.position += Vector3.right * 2f;

        Debug.Log(
            $"[移動テスト] 元の生成地点: {originalPoint.position} / 移動後: {quest.transform.position}");

        // 移動したクエストを60点でクリアする
        CompleteQuest(quest, 60);

        // 新しいクエストが別の地点に生成されたか確認する
        if (activeQuests.TryGetValue(GroupType.GroupA, out Quest nextQuest)
            && nextQuest != null
            && activeQuestPointMap.TryGetValue(nextQuest, out Transform nextPoint)
            && nextPoint != null)
        {
            if (nextPoint != originalPoint
                && !usedSpawnPoints.Contains(originalPoint))
            {
                Debug.Log("[移動テスト成功] 元の地点を解放し、別の地点に生成しました。");
            }
            else
            {
                Debug.LogWarning("[移動テスト失敗] 元の地点の解放と生成先を確認してください。");
            }
        }
        else
        {
            Debug.LogWarning("[移動テスト失敗] 次のクエストが生成されませんでした。");
        }
    }
    private void HandleQuestCleared(Quest clearedQuest)
    {
        if (clearedQuest == null || clearedQuest.Data == null)
        {
            return;
        }

        GroupType clearedGroup = clearedQuest.Data.group;

        // このManagerが生成したクエストのクリアだけを受け付ける
        if (!activeQuestPointMap.ContainsKey(clearedQuest))
        {
            return;
        }

        // 現在位置ではなく、記録した元の生成地点を解放する
        if (activeQuestPointMap.TryGetValue(
            clearedQuest, out Transform usedPoint))
        {
            if (usedPoint != null)
            {
                usedSpawnPoints.Remove(usedPoint);
                lastFreedPointMap[clearedGroup] = usedPoint;
            }

            activeQuestPointMap.Remove(clearedQuest);
        }

        activeQuests.Remove(clearedGroup);

  
        // クリアした種類を除外して、同じグループから補充する
        SpawnQuestForGroup(clearedGroup, clearedQuest.Data);
    }
}
