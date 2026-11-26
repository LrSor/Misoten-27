using TMPro;
using UnityEngine;

public class DisplayTimer : MonoBehaviour
{
    TMP_Text timerText;
    int displayedSeconds = -1;

    void Awake()
    {
        timerText = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        displayedSeconds = -1;
    }

    void LateUpdate()
    {
        var manager = GameTimeManager.Instance;
        if (manager == null || timerText == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, manager.GameTimer));
        if (seconds == displayedSeconds) return;
        displayedSeconds = seconds;
        timerText.SetText("{0:0}", seconds);
    }
}
