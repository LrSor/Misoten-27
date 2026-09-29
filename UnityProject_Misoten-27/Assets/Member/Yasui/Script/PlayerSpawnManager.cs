using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawnManager : BaseManager<PlayerSpawnManager>
{
    [Header("生成設定")]
    [Tooltip("生成するプレイヤーのプレハブ（1種類のみの場合、または配列で個別設定）")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("各プレイヤー専用のプレハブを指定したい場合（4要素）。未設定の要素はplayerPrefabを使用します")]
    [SerializeField] private GameObject[] playerPrefabs = new GameObject[4];

    [Header("スポーン地点 (4名分)")]
    [Tooltip("プレイヤー1〜4の出現位置（Transform4つ）")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    [Header("オプション")]
    [Tooltip("Start時に自動で4人をスポーンさせるか")]
    [SerializeField] private bool autoSpawnOnStart = true;

    // 生成されたプレイヤーの参照リスト
    private List<GameObject> spawnedPlayers = new List<GameObject>();

    /// <summary>
    /// 生成されたプレイヤー群への読み取り専用プロパティ
    /// </summary>
    public IReadOnlyList<GameObject> SpawnedPlayers => spawnedPlayers;

    protected override void OnAwake()
    {
        // 必要に応じてシーン遷移後も残す場合は呼び出します
        // SetDontDestroyOnLoad();
    }

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnAllPlayers();
        }
    }

    /// <summary>
    /// 指定された4つのスポーン地点にプレイヤーを生成します
    /// </summary>
    public void SpawnAllPlayers()
    {
        // 既存のプレイヤーがいる場合は事前に削除（再リスポーン用）
        DespawnAllPlayers();

        for (int i = 0; i < 4; i++)
        {
            // スポーン地点の設定チェック
            Transform spawnPoint = GetSpawnPoint(i);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[PlayerSpawnManager] プレイヤー {i + 1} のスポーン地点が設定されていません。");
                continue;
            }

            // 使用するプレハブの決定（個別設定があれば優先、なければデフォルト）
            GameObject prefabToSpawn = GetPlayerPrefab(i);
            if (prefabToSpawn == null)
            {
                Debug.LogError($"[PlayerSpawnManager] プレイヤー {i + 1} のプレハブが設定されていません。");
                continue;
            }

            // スポーン処理
            GameObject playerObj = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            playerObj.name = $"Player_{i + 1}";
            spawnedPlayers.Add(playerObj);
        }

        Debug.Log($"[PlayerSpawnManager] プレイヤー {spawnedPlayers.Count} 名をスポーンしました。");
    }

    /// <summary>
    /// 生成したすべてのプレイヤーを削除します
    /// </summary>
    public void DespawnAllPlayers()
    {
        foreach (var player in spawnedPlayers)
        {
            if (player != null)
            {
                Destroy(player);
            }
        }
        spawnedPlayers.Clear();
    }

    private Transform GetSpawnPoint(int index)
    {
        if (spawnPoints != null && index < spawnPoints.Length)
        {
            return spawnPoints[index];
        }
        return null;
    }

    private GameObject GetPlayerPrefab(int index)
    {
        if (playerPrefabs != null && index < playerPrefabs.Length && playerPrefabs[index] != null)
        {
            return playerPrefabs[index];
        }
        return playerPrefab;
    }
}
