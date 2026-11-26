using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sawada.Cordex
{
    public sealed class MatchResultView : MonoBehaviour
    {
        [SerializeField] Button exitButton;
        [SerializeField] TMP_Text[] ranks = new TMP_Text[4];
        [SerializeField] GameObject[] models = new GameObject[4];
        public bool HasValidReferences => exitButton != null && ranks.Length == 4 && ranks.All(r => r != null)
            && models.Length == 4 && models.All(m => m != null);

        void Awake()
        {
            if (!HasValidReferences) { Debug.LogError("[MatchResult] Result UI references are missing.", this); enabled = false; return; }
            exitButton.onClick.AddListener(ReturnToLobby);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var entries = TitleLobbyFlow.Current?.ResultEntries;
            for (int i = 0; i < 4; i++)
            {
                var entry = entries?.FirstOrDefault(e => e.id == i + 1);
                ranks[i].text = entry != null && entry.submitted && entry.rank > 0 ? entry.rank + "位" : "--";
                models[i].SetActive(entry != null);
            }
        }

        void ReturnToLobby()
        {
            if (TitleLobbyFlow.Current != null) TitleLobbyFlow.Current.ReturnFromResult();
            else if (Application.CanStreamedLevelBeLoaded(TitleLobbyFlow.LobbyScene))
                SceneManager.LoadSceneAsync(TitleLobbyFlow.LobbyScene);
        }

        void OnDestroy()
        {
            if (exitButton != null) exitButton.onClick.RemoveListener(ReturnToLobby);
        }
    }
}
