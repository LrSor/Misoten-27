using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class LiuPartPickupController : MonoBehaviour
{
    [Header("取得テスト設定")]
    [SerializeField] private bool m_pickupFlag = true;
    [SerializeField] private Key m_pickupKey = Key.F;

    [Tooltip("キーボードで操作するテスト用プレイヤー")]
    [SerializeField]
    private string m_keyboardPlayerName = "Player_1";

    [Header("取得案内")]
    [SerializeField] private TMP_Text m_pickupText;

    private void Awake()
    {
        // 開始時は取得案内を非表示にする。
        UpdatePickupText(null);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        PlayerSpawnManager playerManager = PlayerSpawnManager.Instance;
        LiuPartManager partManager = LiuPartManager.Instance;

        if (!m_pickupFlag
            || m_pickupKey == Key.None
            || keyboard == null
            || playerManager == null
            || partManager == null
            || LIU_SCENE_CONTROLLER.IsTransitioning)
        {
            UpdatePickupText(null);
            return;
        }

        // キーボードを担当するプレイヤーを探す。
        GameObject player = null;

        foreach (GameObject candidate in playerManager.SpawnedPlayers)
        {
            if (candidate != null
                && candidate.activeInHierarchy
                && candidate.name == m_keyboardPlayerName)
            {
                player = candidate;
                break;
            }
        }

        if (player == null)
        {
            UpdatePickupText(null);
            return;
        }

        Part nearestPart = null;
        float nearestDistance = float.MaxValue;

        // 検知範囲内にある運動データの中から、最も近いものを選ぶ。
        foreach (Part part in partManager.ActiveParts)
        {
            if (part == null
                || part.Data == null
                || !part.gameObject.activeInHierarchy)
            {
                continue;
            }

            LiuPartProximity area = part.GetComponent<LiuPartProximity>();

            if (area == null || !area.IsPlayerInRange(player))
            {
                continue;
            }

            float distance =
                (part.transform.position - player.transform.position)
                .sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPart = part;
            }
        }

        // 表示対象と取得対象を同じ運動データにする。
        UpdatePickupText(nearestPart);

        if (nearestPart == null
            || !keyboard[m_pickupKey].wasPressedThisFrame)
        {
            return;
        }

        // テスト取得：スロットへの格納は後で接続する。
        Debug.Log(
            $"[LiuPartPickupController] {player.name} が "
            + $"「{nearestPart.Data.partName}」をテスト取得しました。");

        UpdatePickupText(null);

        // 既存の取得イベントを通して、同じ種類を別地点に再生成する。
        nearestPart.PickUp();
    }

    private void UpdatePickupText(Part part)
    {
        if (m_pickupText == null)
        {
            return;
        }

        bool shouldShow = part != null && part.Data != null;

        string message = shouldShow
            ? $"{m_pickupKey}：取得　{part.Data.partName}"
            : string.Empty;

        // 内容が変わったときだけ文字を更新する。
        if (m_pickupText.text != message)
        {
            m_pickupText.text = message;
        }

        if (m_pickupText.gameObject.activeSelf != shouldShow)
        {
            m_pickupText.gameObject.SetActive(shouldShow);
        }
    }

    private void OnDisable()
    {
        // コントローラー停止時に案内を残さない。
        UpdatePickupText(null);
    }
}
