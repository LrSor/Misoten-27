using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sawada.Cordex
{
    public sealed class PlayerLobbyView : MonoBehaviour
    {
        public static PlayerLobbyView Active { get; private set; }
        [SerializeField] Button copyButton, exitButton, readyButton;
        [SerializeField] TMP_Text roomNumber, readyLabel;
        [SerializeField] GameObject[] playerModels = new GameObject[4];
        [SerializeField] TMP_Text[] playerLabels = new TMP_Text[4];
        readonly TMP_Text[] playerStates = new TMP_Text[4];
        TMP_Text statusLabel;

        public bool HasValidReferences => copyButton != null && exitButton != null && readyButton != null &&
            roomNumber != null && readyLabel != null && playerModels.Length == 4 && playerModels.All(m => m != null) &&
            playerLabels.Length == 4 && playerLabels.All(l => l != null);

        void Awake()
        {
            Active = this;
            if (!HasValidReferences) { Debug.LogError("[TitleLobby] Lobby UI references are missing.", this); enabled = false; return; }
            copyButton.onClick.AddListener(Copy);
            exitButton.onClick.AddListener(Leave);
            readyButton.onClick.AddListener(Ready);
            roomNumber.enableAutoSizing = true;
            roomNumber.fontSizeMin = 18;
            roomNumber.fontSizeMax = roomNumber.fontSize;
            for (int i = 0; i < 4; i++)
            {
                var label = Instantiate(playerLabels[i], playerLabels[i].transform.parent);
                label.name = "LobbyState" + (i + 1);
                label.rectTransform.anchoredPosition += new Vector2(0, -55);
                label.rectTransform.sizeDelta = new Vector2(650, 45);
                label.fontSize = 28;
                label.raycastTarget = false;
                playerStates[i] = label;
            }
            statusLabel = Instantiate(readyLabel, transform);
            statusLabel.name = "LobbyStatus";
            var rect = statusLabel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, 140);
            rect.sizeDelta = new Vector2(1600, 45);
            rect.localScale = Vector3.one;
            statusLabel.fontSize = 28;
            statusLabel.color = Color.white;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.raycastTarget = false;
            Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            var flow = TitleLobbyFlow.Current;
            bool connected = flow != null && flow.IsInRoom;
            bool editable = connected && !flow.IsLoading && !flow.IsLeaving;
            copyButton.interactable = connected;
            readyButton.interactable = editable;
            exitButton.interactable = flow != null && !flow.IsLoading && !flow.IsLeaving;
            roomNumber.text = connected ? flow.RoomCode : "---";
            readyLabel.text = connected && flow.IsPlayerPrepared(flow.PlayerNumber) ? "準備解除" : "準備完了";
            for (int i = 0; i < 4; i++)
            {
                int id = i + 1;
                bool present = connected && flow.IsPlayerPresent(id);
                playerModels[i].SetActive(present);
                playerLabels[i].text = "プレイヤー" + id;
                bool prepared = present && flow.IsPlayerPrepared(id);
                playerStates[i].text = !present ? "参加待ち" :
                    (id == flow.PlayerNumber ? "あなた / " : "") + (id == 1 ? "Host / " : "Client / ") +
                    (!flow.IsPlayerInLobby(id) ? "結果確認中" : prepared ? "準備完了" : "準備中");
                playerStates[i].color = prepared ? new Color(.35f, 1f, .45f) : Color.white;
            }
            statusLabel.text = !connected ? "部屋に接続していません。退出でタイトルに戻ります。" :
                flow.IsLoading ? "全員のゲーム読み込みを待っています…" :
                flow.IsLeaving ? "退出しています…" : "接続している全員が準備完了するとゲームを開始します。";
        }

        void Copy() => TitleLobbyFlow.Current?.CopyRoomCode();
        void Leave() => TitleLobbyFlow.Current?.LeaveRoom();
        void Ready() => TitleLobbyFlow.Current?.TogglePrepared();

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (copyButton != null) copyButton.onClick.RemoveListener(Copy);
            if (exitButton != null) exitButton.onClick.RemoveListener(Leave);
            if (readyButton != null) readyButton.onClick.RemoveListener(Ready);
        }
    }
}
