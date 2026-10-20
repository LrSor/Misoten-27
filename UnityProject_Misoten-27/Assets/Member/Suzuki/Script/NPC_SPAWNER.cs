using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//==================================================
// NPC_VANISH を出現させ続けるスポナー
//
// ・出現地点ごとに目的地（消える場所）を決めておく（ルート）
// ・NPC の人数が最大人数より少なくなったら、一定間隔で補充する
// ・出現地点の近くに NPC がいるときは、その地点からは出さない
//
// 使い方:
//   1. 空の GameObject にこのスクリプトを付ける
//   2. m_npcPrefabs に NPC_VANISH 付きのプレハブを入れる（複数ならランダム）
//   3. m_routes に「出現地点」と「目的地」の組を登録する
//==================================================
public class NPC_SPAWNER : MonoBehaviour
{
    // 出現地点と目的地の組
    [System.Serializable]
    private class SPAWN_ROUTE
    {
        [Tooltip("NPC が出現する場所")]
        public Transform m_spawnPoint;

        [Tooltip("この地点から出た NPC が向かって消える場所")]
        public Transform m_destination;
    }

    [Header("出現させる NPC")]
    [Tooltip("NPC_VANISH が付いたプレハブ。複数入れるとランダムに選ぶ")]
    [SerializeField]
    private NPC_VANISH[] m_npcPrefabs;

    [Header("ルート（出現地点 → 目的地）")]
    [SerializeField]
    private SPAWN_ROUTE[] m_routes;

    [Header("人数")]
    [Tooltip("同時にいる NPC の最大人数")]
    [SerializeField]
    private int m_maxCount = 5;

    [Header("補充の間隔（秒）")]
    [SerializeField]
    private float m_minInterval = 1f;

    [SerializeField]
    private float m_maxInterval = 3f;

    [Header("出現地点の混雑チェック")]
    [Tooltip("出現地点からこの距離以内に NPC がいたら、その地点からは出さない")]
    [SerializeField]
    private float m_spawnClearRadius = 1.5f;

    [Tooltip("出現地点付近の NavMesh を探す半径")]
    [SerializeField]
    private float m_navMeshSampleDistance = 2f;

    // 今いる NPC
    private readonly List<NPC_VANISH> m_aliveNpcs =
        new List<NPC_VANISH>();

    // 出現できるルート（毎回 new しないよう使い回す）
    private readonly List<SPAWN_ROUTE> m_availableRoutes =
        new List<SPAWN_ROUTE>();

    private float m_spawnTimer;

    void Start()
    {
        if (m_npcPrefabs == null ||
            m_npcPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "出現させる NPC のプレハブが設定されていません: " + name);

            enabled = false;
            return;
        }

        // Hierarchy（シーン上）の NPC が入っていないか確認する
        // シーン上の NPC を入れると、その NPC が消えた時点で
        // 参照が無くなり、それ以降出現しなくなる
        foreach (NPC_VANISH prefab in m_npcPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning(
                    "m_npcPrefabs に空の欄があります: " + name);
            }
            else if (prefab.gameObject.scene.IsValid())
            {
                Debug.LogError(
                    "m_npcPrefabs に Hierarchy（シーン上）の NPC「" + prefab.name +
                    "」が入っています。Project ウィンドウのプレハブを入れてください: " + name,
                    this);
            }
        }

        if (m_routes == null ||
            m_routes.Length == 0)
        {
            Debug.LogWarning(
                "ルートが設定されていません: " + name);

            enabled = false;
            return;
        }

        // 最初の1人はすぐに出す
        m_spawnTimer = 0f;
    }

    void Update()
    {
        // 削除済みの NPC をリストから外す（念のため）
        m_aliveNpcs.RemoveAll(npc => npc == null);

        // 最大人数に達している間はタイマーを止めておく
        if (m_aliveNpcs.Count >= m_maxCount)
        {
            return;
        }

        m_spawnTimer -= Time.deltaTime;

        if (m_spawnTimer > 0f)
        {
            return;
        }

        if (TrySpawn())
        {
            m_spawnTimer =
                Random.Range(
                    m_minInterval,
                    m_maxInterval);
        }
        else
        {
            // 全部の出現地点が混んでいたら、少し待って再挑戦
            m_spawnTimer = 0.5f;
        }
    }

    //==================================================
    // 出現
    //==================================================

    bool TrySpawn()
    {
        // 空いているルートを集める
        m_availableRoutes.Clear();

        foreach (SPAWN_ROUTE route in m_routes)
        {
            if (route == null ||
                route.m_spawnPoint == null ||
                route.m_destination == null)
            {
                continue;
            }

            if (IsSpawnPointCrowded(route.m_spawnPoint.position))
            {
                continue;
            }

            m_availableRoutes.Add(route);
        }

        if (m_availableRoutes.Count == 0)
        {
            return false;
        }

        SPAWN_ROUTE selectedRoute =
            m_availableRoutes[
                Random.Range(0, m_availableRoutes.Count)];

        // 出現位置を NavMesh 上に合わせる（ずれていると NavMeshAgent が動けない）
        NavMeshHit navMeshHit;

        if (!NavMesh.SamplePosition(
            selectedRoute.m_spawnPoint.position,
            out navMeshHit,
            m_navMeshSampleDistance,
            NavMesh.AllAreas))
        {
            Debug.LogWarning(
                "出現地点が NavMesh 上にありません: "
                + selectedRoute.m_spawnPoint.name);

            return false;
        }

        // 目的地の方を向いて出現させる
        Vector3 lookDirection =
            selectedRoute.m_destination.position -
            navMeshHit.position;

        lookDirection.y = 0f;

        Quaternion rotation =
            lookDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDirection)
                : selectedRoute.m_spawnPoint.rotation;

        NPC_VANISH prefab =
            PickPrefab();

        if (prefab == null)
        {
            Debug.LogError(
                "出現させられるプレハブがありません（参照が消えています）。スポナーを停止します: " + name,
                this);

            enabled = false;
            return false;
        }

        // スポナーの子にしてヒエラルキーを散らからないようにする
        NPC_VANISH npc =
            Instantiate(
                prefab,
                navMeshHit.position,
                rotation,
                transform);

        // Start より前に目的地を渡す
        npc.SetDestination(selectedRoute.m_destination);

        npc.Vanished += OnNpcVanished;

        m_aliveNpcs.Add(npc);

        return true;
    }

    // 空でないプレハブからランダムに1つ選ぶ（全部空なら null）
    NPC_VANISH PickPrefab()
    {
        int validCount = 0;

        foreach (NPC_VANISH prefab in m_npcPrefabs)
        {
            if (prefab != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int pickIndex =
            Random.Range(0, validCount);

        foreach (NPC_VANISH prefab in m_npcPrefabs)
        {
            if (prefab == null)
            {
                continue;
            }

            if (pickIndex == 0)
            {
                return prefab;
            }

            pickIndex--;
        }

        return null;
    }

    // 出現地点の近くに NPC がいるか
    bool IsSpawnPointCrowded(Vector3 spawnPosition)
    {
        float clearRadiusSqr =
            m_spawnClearRadius * m_spawnClearRadius;

        foreach (NPC_VANISH npc in m_aliveNpcs)
        {
            if (npc == null)
            {
                continue;
            }

            if ((npc.transform.position - spawnPosition).sqrMagnitude
                < clearRadiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    //==================================================
    // NPC が消えたとき
    //==================================================

    void OnNpcVanished(NPC_VANISH npc)
    {
        npc.Vanished -= OnNpcVanished;

        m_aliveNpcs.Remove(npc);

        // プレハブ側が「非表示にするだけ」の設定でも、
        // 非表示の NPC がたまり続けないように削除する
        Destroy(npc.gameObject);
    }

    //==================================================
    // Inspector の入力ミス対策
    //==================================================

    void OnValidate()
    {
        m_maxCount = Mathf.Max(0, m_maxCount);

        m_minInterval = Mathf.Max(0f, m_minInterval);
        m_maxInterval = Mathf.Max(m_minInterval, m_maxInterval);

        m_spawnClearRadius = Mathf.Max(0f, m_spawnClearRadius);
        m_navMeshSampleDistance = Mathf.Max(0.1f, m_navMeshSampleDistance);
    }

    //==================================================
    // デバッグ表示：ルートを表示（スポナーを選択したときだけ）
    //==================================================

    void OnDrawGizmosSelected()
    {
        if (m_routes == null)
        {
            return;
        }

        foreach (SPAWN_ROUTE route in m_routes)
        {
            if (route == null ||
                route.m_spawnPoint == null)
            {
                continue;
            }

            // 出現地点（混雑チェックの範囲）
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                route.m_spawnPoint.position,
                m_spawnClearRadius);

            if (route.m_destination == null)
            {
                continue;
            }

            // 出現地点 → 目的地
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                route.m_spawnPoint.position,
                route.m_destination.position);

            Gizmos.DrawWireSphere(
                route.m_destination.position,
                0.5f);
        }
    }
}
