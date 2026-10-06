using System.Collections.Generic;
using UnityEngine;

public class PartManager : BaseManager<PartManager>
{
    [System.Serializable]
    public struct GroupPartSpawnSet
    {
        public GroupType group;               // 対象グループ (GroupA ~ GroupE)[cite: 1]
        public List<PartData> partPool;       // そのグループに属するパーツデータ群
        public Transform[] spawnPoints;       // そのグループ用パーツの出現候補地点（複数）
    }

    [Header("グループごとのパーツ設定 (GroupA ~ GroupE の5つ)")]
    [SerializeField] private List<GroupPartSpawnSet> groupPartSpawnSets;

    private HashSet<Transform> usedSpawnPoints = new HashSet<Transform>();
    private Dictionary<Part, Transform> activePartPointMap = new Dictionary<Part, Transform>();
    private Dictionary<GroupType, Transform> lastFreedPointMap = new Dictionary<GroupType, Transform>();

    protected override void OnAwake()
    {
        // 必用に応じてシーン遷移後も残す場合は呼び出します
        // SetDontDestroyOnLoad();
    }

    private void OnEnable()
    {
        Part.OnPartPickedUp += HandlePartPickedUp;
    }

    private void OnDisable()
    {
        Part.OnPartPickedUp -= HandlePartPickedUp;
    }

    private void Start()
    {
        SpawnAllGroupParts();
    }

    public void SpawnAllGroupParts()
    {
        foreach (var set in groupPartSpawnSets)
        {
            for (int i = 0; i < 2; i++)
            {
                SpawnSinglePartForGroup(set.group);
            }
        }
    }

    private void SpawnSinglePartForGroup(GroupType group)
    {
        GroupPartSpawnSet set = groupPartSpawnSets.Find(s => s.group == group);
        if (set.partPool == null || set.partPool.Count == 0 || set.spawnPoints == null || set.spawnPoints.Length == 0) return;

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

        PartData selectedData = set.partPool[Random.Range(0, set.partPool.Count)];

        GameObject partObj = Instantiate(selectedData.partPrefab, chosenSpawnPoint.position, chosenSpawnPoint.rotation);
        Part part = partObj.GetComponent<Part>();
        if (part != null)
        {
            part.Initialize(selectedData);
            activePartPointMap[part] = chosenSpawnPoint;
        }
    }

    private void HandlePartPickedUp(Part pickedPart)
    {
        if (pickedPart == null) return;

        GroupType group = pickedPart.Data.group;

        if (activePartPointMap.TryGetValue(pickedPart, out Transform usedPoint))
        {
            if (usedPoint != null)
            {
                usedSpawnPoints.Remove(usedPoint);
                lastFreedPointMap[group] = usedPoint;
            }
            activePartPointMap.Remove(pickedPart);
        }

        SpawnSinglePartForGroup(group);
    }
}
