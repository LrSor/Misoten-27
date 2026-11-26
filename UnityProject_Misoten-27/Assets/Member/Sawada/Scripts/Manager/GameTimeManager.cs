using UnityEngine;
using UnityEngine.SceneManagement;
using Sawada.Cordex;

public class GameTimeManager : BaseManager<GameTimeManager>
{
    float m_timeScale = 1f;
    [SerializeField] float m_time = 0f;
    float m_timer;
    NetworkTestSession m_session;
    public bool HasEnded { get; private set; }

    public float GameTimer => m_timer;
    public float GameTimeScale
    {
        get => m_timeScale;
        set => m_timeScale = value;
    }

    private void Start()
    {
        m_timer = m_time;
        m_session = FindFirstObjectByType<NetworkTestSession>();
    }

    private void FixedUpdate()
    {
        if (HasEnded || (m_session != null && !m_session.CanAdvanceMatchTimer)) return;
        m_timer = Mathf.Max(0f, m_timer - Time.fixedDeltaTime * m_timeScale);
        if (m_timer > 0f || (m_session != null && !m_session.IsHost)) return;
        HasEnded = true;
        if (m_session != null) m_session.FinishTimedMatch();
        else if (Application.CanStreamedLevelBeLoaded(TitleLobbyFlow.ResultScene))
            SceneManager.LoadSceneAsync(TitleLobbyFlow.ResultScene);
        else Debug.LogError("Result_Kari is missing from the build.");
    }

    public void SynchronizeTime(float remaining)
    {
        if (!HasEnded) m_timer = Mathf.Max(0f, remaining);
    }

    public void CompleteFromHost()
    {
        m_timer = 0f;
        HasEnded = true;
    }
}
