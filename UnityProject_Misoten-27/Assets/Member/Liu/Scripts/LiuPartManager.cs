using System.Collections.Generic;
using UnityEngine;

public class LiuPartManager : BaseManager<LiuPartManager>
{
    [System.Serializable]
    public struct GroupPartSpawnSet
    {
        public GroupType group;               // 対象グループ (GroupA ~ GroupE)[cite: 1]
        public List<PartData> partPool;       // そのグループに属するパーツデータ群
        public Transform[] spawnPoints;       // そのグループ用パーツの出現候補地点（複数）
    }

    [Header("グループごとの運動データ設定")]
    [SerializeField] private List<GroupPartSpawnSet> groupPartSpawnSets;

    private HashSet<Transform> usedSpawnPoints = new HashSet<Transform>();
    private Dictionary<Part, Transform> activePartPointMap = new Dictionary<Part, Transform>();
    // 出現中の運動データを外部から読み取れるようにする。
    public IEnumerable<Part> ActiveParts => activePartPointMap.Keys;

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

    // 各グループに登録された運動データを、種類ごとに1個生成する。
    public void SpawnAllGroupParts()
    {
        if (groupPartSpawnSets == null)
        {
            return;
        }

        foreach (GroupPartSpawnSet set in groupPartSpawnSets)
        {
            // 運動データが未設定のグループは読み飛ばす。
            if (set.partPool == null || set.partPool.Count == 0)
            {
                Debug.LogWarning(
                    $"[LiuPartManager] {set.group} の運動データが未設定です。");
                continue;
            }

            // ランダムに種類を選ばず、登録された種類を順番に生成する。
            foreach (PartData data in set.partPool)
            {
                TrySpawnPart(set, data);
            }
        }
    }

   

    // 指定した種類の運動データを、空いている地点に生成する。
    private bool TrySpawnPart(
        GroupPartSpawnSet set,
        PartData data,
        Transform excludedPoint = null)
    {
        // データ、グループ、Prefab、出現地点の設定を確認する。
        if (data == null
            || data.group != set.group
            || data.partPrefab == null
            || set.spawnPoints == null)
        {
            Debug.LogWarning(
                "[LiuPartManager] 運動データの設定を確認してください。");
            return false;
        }

        // PrefabのルートにはPartコンポーネントが必要。
        if (data.partPrefab.GetComponent<Part>() == null)
        {
            Debug.LogWarning(
                "[LiuPartManager] PrefabにPartがありません："
                + data.partName);
            return false;
        }

        // 同じ種類が出現中の場合は、重複して生成しない。
        foreach (var pair in activePartPointMap)
        {
            if (pair.Key != null && pair.Key.Data == data)
            {
                return false;
            }
        }

        List<Transform> availablePoints = new List<Transform>();

        foreach (Transform point in set.spawnPoints)
        {
            // 使用中の地点と、取得直前の地点を候補から除外する。
            if (point == null
                || point == excludedPoint
                || usedSpawnPoints.Contains(point))
            {
                continue;
            }

            // 同じ地点の重複登録を除く。
            if (!availablePoints.Contains(point))
            {
                availablePoints.Add(point);
            }
        }

        // 別の空き地点がなければ、設定を見直せるように警告する。
        if (availablePoints.Count == 0)
        {
            Debug.LogWarning(
                "[LiuPartManager] 空きスポーン地点がありません："
                + data.partName);
            return false;
        }

        Transform chosenPoint =
            availablePoints[Random.Range(0, availablePoints.Count)];

        GameObject partObject = Instantiate(
            data.partPrefab,
            chosenPoint.position,
            chosenPoint.rotation);

        Part part = partObject.GetComponent<Part>();
        part.Initialize(data);

        // 使用中の地点と、生成したオブジェクトの対応を記録する。
        usedSpawnPoints.Add(chosenPoint);
        activePartPointMap[part] = chosenPoint;

        Debug.Log(
            $"[LiuPartManager] [{data.group}] {data.partName} を "
            + $"{chosenPoint.name} に生成しました。");

        return true;
    }

    // 取得された運動データを、同じ種類のまま別の地点に再生成する。
    private void HandlePartPickedUp(Part pickedPart)
    {
        if (pickedPart == null || pickedPart.Data == null)
        {
            return;
        }

        // このManagerが生成したオブジェクトだけを処理する。
        if (!activePartPointMap.TryGetValue(
            pickedPart, out Transform usedPoint))
        {
            return;
        }

        // 取得された種類を保存する。
        PartData data = pickedPart.Data;

        // 古い登録を解除してから、新しいオブジェクトを生成する。
        activePartPointMap.Remove(pickedPart);

        if (usedPoint != null)
        {
            usedSpawnPoints.Remove(usedPoint);
        }

        if (groupPartSpawnSets == null)
        {
            Debug.LogWarning(
                "[LiuPartManager] グループ設定がありません。");
            return;
        }

        // この運動データが登録されているグループ設定を探す。
        int setIndex = groupPartSpawnSets.FindIndex(
            set => set.group == data.group
                && set.partPool != null
                && set.partPool.Contains(data)
        );

        if (setIndex < 0)
        {
            Debug.LogWarning(
                "[LiuPartManager] 再生成するデータの設定がありません："
                + data.partName);
            return;
        }

        // 取得直前の地点を除外し、同じ種類を再生成する。
        TrySpawnPart(groupPartSpawnSets[setIndex], data, usedPoint);
    }
}
