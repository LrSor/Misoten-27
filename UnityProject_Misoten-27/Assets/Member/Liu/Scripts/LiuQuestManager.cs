using System.Collections.Generic;
using UnityEngine;

public class LiuQuestManager : BaseManager<LiuQuestManager>
{

    // クエストごとに出現可能な地点を設定する
    [System.Serializable]
    public class QuestSpawnSet
    {
        [Tooltip("出現させるクエストのデータ")]
        public QuestData quest;

        [Tooltip("このクエストが出現できる地点")]
        public Transform[] spawnPoints;
    }

    [Header("クエストごとの出現設定")]
    [SerializeField]
    private List<QuestSpawnSet> questSpawnSets =
        new List<QuestSpawnSet>();

    private HashSet<Transform> usedSpawnPoints = new HashSet<Transform>();
    

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

    // このクエストに設定された地点から、使用可能な地点を取得する
    private List<Transform> GetAvailableSpawnPoints(
        QuestSpawnSet set, Transform excludedPoint)
    {
        List<Transform> availablePoints = new List<Transform>();

        // 出現地点が未設定の場合は空のリストを返す
        if (set == null || set.spawnPoints == null)
        {
            return availablePoints;
        }

        foreach (Transform point in set.spawnPoints)
        {
            // 未設定・直前の地点・使用中の地点は候補から除外する
            if (point == null
                || point == excludedPoint
                || usedSpawnPoints.Contains(point))
            {
                continue;
            }

            // 同じ地点が重複登録されていても一度だけ追加する
            if (!availablePoints.Contains(point))
            {
                availablePoints.Add(point);
            }
        }

        return availablePoints;
    }

    private void SpawnInitialQuests()
    {
        if (questSpawnSets == null)
        {
            return;
        }

        // 初期生成が完了したグループを記録する
        HashSet<GroupType> initializedGroups = new HashSet<GroupType>();

        foreach (QuestSpawnSet set in questSpawnSets)
        {
            // 未設定の項目は読み飛ばす
            if (set == null || set.quest == null)
            {
                continue;
            }

            // グループは各クエストのデータから取得する
            GroupType group = set.quest.group;

            // 同じグループの初期生成は一度だけ行う
            if (!initializedGroups.Add(group))
            {
                continue;
            }

            // 各グループに異なるクエストを2個生成する
            for (int i = 0; i < 2; i++)
            {
                SpawnQuestForGroup(group);
            }
        }
    }

    private void SpawnQuestForGroup(
        GroupType group, QuestData excludedQuest = null)
    {
        if (questSpawnSets == null)
        {
            return;
        }

        // 出現中の種類と、このグループのクエスト数を確認する
        HashSet<QuestData> activeData = new HashSet<QuestData>();
        int activeCount = 0;

        foreach (var pair in activeQuestPointMap)
        {
            Quest currentQuest = pair.Key;

            if (currentQuest == null || currentQuest.Data == null)
            {
                continue;
            }

            activeData.Add(currentQuest.Data);

            if (currentQuest.Data.group == group)
            {
                activeCount++;
            }
        }

        // 同じグループは最大2個までにする
        if (activeCount >= 2)
        {
            return;
        }

        // 直前にクリアされたクエストの生成地点を取得する
        lastFreedPointMap.TryGetValue(
            group, out Transform lastFreedPoint);

        List<QuestSpawnSet> candidates = new List<QuestSpawnSet>();

        foreach (QuestSpawnSet set in questSpawnSets)
        {
            if (set == null || set.quest == null)
            {
                continue;
            }

            QuestData data = set.quest;

            // 同グループの未出現クエストだけを候補にする
            // 直前にクリアした種類も除外する
            if (data.group != group
                || data == excludedQuest
                || activeData.Contains(data)
                || data.questPrefab == null)
            {
                continue;
            }

            // PrefabにはQuestコンポーネントが必要
            if (data.questPrefab.GetComponent<Quest>() == null)
            {
                continue;
            }

            // このクエスト自身の設定に空き地点があるか確認する
            if (GetAvailableSpawnPoints(set, lastFreedPoint).Count == 0)
            {
                continue;
            }

            // 同じクエストの重複登録は候補に追加しない
            if (!candidates.Exists(item => item.quest == data))
            {
                candidates.Add(set);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning(
                $"[LiuQuestManager] {group} に生成可能な候補がありません。" +
                "クエストの種類、Prefab、空きスポーン地点を確認してください。");
            return;
        }

        // スコア方式が決まるまでは候補からランダムに選ぶ
        QuestSpawnSet selectedSet =
            candidates[Random.Range(0, candidates.Count)];

        // 選ばれたクエストに設定された地点から選ぶ
        List<Transform> availablePoints =
            GetAvailableSpawnPoints(selectedSet, lastFreedPoint);

        Transform chosenPoint =
            availablePoints[Random.Range(0, availablePoints.Count)];

        QuestData selectedData = selectedSet.quest;

        GameObject questObj = Instantiate(
            selectedData.questPrefab,
            chosenPoint.position,
            chosenPoint.rotation);

        Quest quest = questObj.GetComponent<Quest>();
        quest.Initialize(selectedData);


        // 使用中の地点と、クエストの生成元を記録する
        usedSpawnPoints.Add(chosenPoint);
        activeQuestPointMap[quest] = chosenPoint;


        Debug.Log(
            $"[LiuQuestManager] [{group}] {selectedData.questTitle} を " +
            $"{chosenPoint.name} に生成しました。");
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



  
        // クリアした種類を除外して、同じグループから補充する
        SpawnQuestForGroup(clearedGroup, clearedQuest.Data);
    }
}
