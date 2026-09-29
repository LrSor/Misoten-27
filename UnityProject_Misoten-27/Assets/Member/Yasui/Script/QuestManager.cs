using System.Collections.Generic;
using UnityEngine;

public class QuestManager : BaseManager<QuestManager>
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
    private Dictionary<GroupType, Quest> activeQuests = new Dictionary<GroupType, Quest>();
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
            SpawnQuestForGroup(set.group);
        }
    }

    private void SpawnQuestForGroup(GroupType group)
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

        QuestData selectedData = set.questPool[Random.Range(0, set.questPool.Count)];

        GameObject questObj = Instantiate(selectedData.questPrefab, chosenSpawnPoint.position, chosenSpawnPoint.rotation);
        Quest quest = questObj.GetComponent<Quest>();
        if (quest != null)
        {
            quest.Initialize(selectedData);
            activeQuests[group] = quest;
        }
    }

    private void HandleQuestCleared(Quest clearedQuest)
    {
        if (clearedQuest == null) return;

        GroupType clearedGroup = clearedQuest.Data.group;

        Transform freedPoint = clearedQuest.transform;
        GroupSpawnSet set = groupSpawnSets.Find(s => s.group == clearedGroup);

        foreach (var pt in set.spawnPoints)
        {
            if (pt != null && Vector3.Distance(pt.position, freedPoint.position) < 0.1f)
            {
                usedSpawnPoints.Remove(pt);
                lastFreedPointMap[clearedGroup] = pt;
                break;
            }
        }

        if (activeQuests.ContainsKey(clearedGroup))
        {
            activeQuests.Remove(clearedGroup);
        }

        SpawnQuestForGroup(clearedGroup);
    }
}
