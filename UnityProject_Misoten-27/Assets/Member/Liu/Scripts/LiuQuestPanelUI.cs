using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class LiuQuestPanelUI : MonoBehaviour
{
    [Tooltip("表示・非表示を切り替えるクエスト情報パネル")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("グループカラーを反映するパネル背景")]
    [SerializeField] private Image panelBackground;

    [Tooltip("クエスト名を表示するテキスト")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("適正運動データの名称を表示するテキスト")]
    [SerializeField] private TMP_Text recommendedPartText;
    [Tooltip("受注操作と受注状態を表示するテキスト")]
    [SerializeField] private TMP_Text m_acceptText;
    // 現在パネルに表示しているクエスト。
    private Quest m_displayQuest;

    [Tooltip("Eキーで受注するテスト用プレイヤーの名前")]
    [SerializeField] private string m_keyboardPlayerName = "Player_1";
    // 範囲に入った順番でクエストを記録する
    private readonly List<Quest> nearbyQuests = new List<Quest>();

    private void Awake()
    {
        // 開始時はパネルを非表示にする
        RefreshPanel();
    }

    // 検知スクリプトから範囲への出入りを受け取る
    public void SetQuestInRange(Quest quest, bool isInRange)
    {
        if (quest == null)
        {
            return;
        }

        if (isInRange)
        {
            // 同じクエストを重複して登録しない
            if (!nearbyQuests.Contains(quest))
            {
                nearbyQuests.Add(quest);
            }
        }
        else
        {
            nearbyQuests.Remove(quest);
        }

        RefreshPanel();
    }

    private void Update()
    {
        // トランジション中は受注しない。
        if (LIU_SCENE_CONTROLLER.IsTransitioning)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        // EキーとQキーのどちらも押されていなければ処理しない。
        if (keyboard == null
            || (!keyboard.eKey.wasPressedThisFrame
                && !keyboard.qKey.wasPressedThisFrame))
        {
            return;
        }
        // 最新の表示対象を確認する。
        RefreshPanel();

        if (panelRoot == null || !panelRoot.activeInHierarchy
            || m_displayQuest == null)
        {
            return;
        }

        PlayerSpawnManager manager = PlayerSpawnManager.Instance;

        if (manager == null)
        {
            return;
        }

        // Eキーを担当するテスト用プレイヤーを取得する。
        GameObject player = null;

        foreach (GameObject candidate in manager.SpawnedPlayers)
        {
            if (candidate != null && candidate.name == m_keyboardPlayerName)
            {
                player = candidate;
                break;
            }
        }

        LiuQuestProximity proximity =
            m_displayQuest.GetComponentInChildren<LiuQuestProximity>();

        // 他のプレイヤーが近づいているだけでは受注できない。
        if (proximity == null || !proximity.IsPlayerInRange(player))
        {
            return;
        }

        LIU_QUEST_STATUS questStatus =
            m_displayQuest.GetComponent<LIU_QUEST_STATUS>();

        if (questStatus == null)
        {
            return;
        }

        // テスト用：受注した本人だけがQキーで解除できる。
        if (keyboard.qKey.wasPressedThisFrame)
        {
            if (questStatus.IsAccepted
                && questStatus.OwnerPlayer == player)
            {
                questStatus.ReleaseQuest();

                Debug.Log(
                    player.name + " がクエストの受注を解除しました。"
                );

                RefreshPanel();
            }

            // 同じフレームで再受注しない。
            return;
        }

        // 未受注の場合だけ受け付ける。
        if (questStatus.TryAccept(player))
        {
            Debug.Log(
                player.name + " が「"
                + m_displayQuest.Data.questTitle + "」を受注しました。"
            );

            RefreshPanel();
        }
    }

    private void LateUpdate()
    {
        // 消滅・非アクティブ化したクエストを除く
        nearbyQuests.RemoveAll(
            quest => quest == null || !quest.gameObject.activeInHierarchy
        );

        RefreshPanel();
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        Quest displayQuest = null;

        // 後から範囲に入ったクエストを優先して表示する
        for (int i = nearbyQuests.Count - 1; i >= 0; i--)
        {
            Quest quest = nearbyQuests[i];

            if (quest != null
                && quest.gameObject.activeInHierarchy
                && quest.Data != null)
            {
                displayQuest = quest;
                break;
            }
        }

        bool shouldShow = displayQuest != null;
        m_displayQuest = displayQuest;

        // 表示中のクエストの受注状態を取得する。
        LIU_QUEST_STATUS questStatus = shouldShow
            ? displayQuest.GetComponent<LIU_QUEST_STATUS>()
            : null;

        // 受注状態に合わせて操作案内を変更する。
        if (m_acceptText != null)
        {
            if (!shouldShow)
            {
                m_acceptText.text = string.Empty;
            }
            else if (questStatus == null)
            {
                m_acceptText.text = "受注設定を確認してください";
            }
            else if (questStatus.IsAccepted)
            {
                // テスト対象のプレイヤー本人にだけ解除操作を案内する。
                bool ownerFlag = questStatus.OwnerPlayer != null
                    && questStatus.OwnerPlayer.name == m_keyboardPlayerName;

                m_acceptText.text = ownerFlag
                    ? "受注中 / Q：解除"
                    : "他のプレイヤーが受注中";
            }
            else
            {
                m_acceptText.text = "E：クエストを受注";
            }
        }

        // グループカラーを暗くして、白い文字を読みやすくする
        if (shouldShow && panelBackground != null)
        {
            Color groupColor =
                LiuQuestGroupColor.GetGroupColor(displayQuest.Data.group);

            Color backgroundColor = Color.Lerp(
                new Color(0.08f, 0.08f, 0.08f),
                groupColor,
                0.4f
            );
            // 受注中はパネル背景も暗くする。
            if (questStatus != null && questStatus.IsAccepted)
            {
                backgroundColor = Color.Lerp(
                    backgroundColor, Color.black, 0.65f);
            }

            backgroundColor.a = 0.92f;

            // 色が変わった場合だけ背景を更新する
            if (panelBackground.color != backgroundColor)
            {
                panelBackground.color = backgroundColor;
            }
        }

        // クエストデータから正式な名称を取得する
        if (titleText != null)
        {
            string newTitle = shouldShow
                ? displayQuest.Data.questTitle
                : string.Empty;

            // 内容が変わった場合だけ文字を更新する
            if (titleText.text != newTitle)
            {
                titleText.text = newTitle;
            }
        }
        // クエストに設定された適正運動データの名称を表示する
        if (recommendedPartText != null)
        {
            string newPartText = string.Empty;

            if (shouldShow)
            {
                PartData recommendedPart =
                    displayQuest.Data.recommendedPart;

                // データや名称が未設定の場合は、その状態を表示する
                string partName = "未設定";

                if (recommendedPart != null
                    && !string.IsNullOrWhiteSpace(recommendedPart.partName))
                {
                    partName = recommendedPart.partName;
                }

                newPartText = "適正運動データ：\n" + partName;
            }

            // 内容が変わった場合だけ文字を更新する
            if (recommendedPartText.text != newPartText)
            {
                recommendedPartText.text = newPartText;
            }
        }

        // 表示できるクエストがある場合だけパネルを表示する
        if (panelRoot.activeSelf != shouldShow)
        {
            panelRoot.SetActive(shouldShow);
        }
    }
}
