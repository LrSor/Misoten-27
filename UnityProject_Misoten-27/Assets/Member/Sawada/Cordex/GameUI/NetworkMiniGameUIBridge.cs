using System.Linq;
using UnityEngine;
using Sawada.Cordex;

/// <summary>Connects the local participant's Host-confirmed session to an existing UI instance.</summary>
[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public sealed class NetworkMiniGameUIBridge : MonoBehaviour
{
    private NetworkTestSession m_session;
    private MiniGameUIBase m_target;
    private PlayerScore m_score;
    private StarCollectionEffect m_collection;
    private Canvas m_scoreCanvas;
    private int m_game, m_round, m_sequence;

    public bool IsReady => isActiveAndEnabled && m_session != null && m_session.isActiveAndEnabled
        && m_target != null && m_target.isActiveAndEnabled;

    public void Configure(NetworkTestSession session)
    {
        if (m_session != null) m_session.MiniGameStateChanged -= ReceiveState;
        m_session = session;
        if (m_session != null) m_session.MiniGameStateChanged += ReceiveState;
        FindTarget();
    }

    private void FindTarget()
    {
        if (m_session == null || m_target != null) return;
        var candidates = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MiniGameUIBase>(true))
            .Where(ui => ui.isActiveAndEnabled).ToArray();
        if (candidates.Length != 1) return;
        m_target = candidates[0];
        if (m_target != null) { m_target.SetExternalControl(true); m_score = null; }
    }

    private void BindScore()
    {
        var source = m_session != null ? m_session.LocalMiniGamePlayerScore : null;
        if (source == null) return;
        var canvas = source.GetPlayerCanvas();
        if (canvas == null) return;
        if (source == m_score && canvas.rootCanvas == m_scoreCanvas) return;
        m_score = source; m_scoreCanvas = canvas.rootCanvas;
        m_collection = canvas.rootCanvas.GetComponentInChildren<StarCollectionEffect>(true);
        if (m_target != null) m_target.BindStarCollectionEffect(m_collection);
    }

    private void Update()
    {
        if (m_session == null) return;
        if (!m_session.isActiveAndEnabled)
        {
            ResetPresentation();
            if (m_target != null) m_target.SetExternalControl(false);
            return;
        }
        FindTarget();
        if (m_target != null) m_target.SetExternalControl(true);
        BindScore();
        if (m_session.LocalMiniGamePlayerId == 0) { ResetPresentation(); return; }
        var state = m_session.LocalMiniGame;
        if (state != null) ReceiveState(state);
    }

    public void ReceiveState(NetworkTestSession.MiniGamePacket state)
    {
        if (m_session == null || state == null || state.m_player != m_session.LocalMiniGamePlayerId) return;
        if (m_round > 0 && state.m_session < m_round) return;
        FindTarget();
        if (!IsReady) return;
        BindScore();
        bool sameRound = state.m_game == m_game && state.m_session == m_round;
        if (sameRound && state.m_sequence <= m_sequence) return;
        if (state.m_phase == NetworkTestSession.MiniGamePhase.Closed)
        {
            if (!sameRound) return;
            m_sequence = state.m_sequence;
            m_target.CloseExternalGame(state.m_completed);
            return;
        }
        if (!sameRound)
        {
            // A new game cannot inherit the previous result's collection permission.
            m_target.CloseExternalGame(false);
            m_game = state.m_game; m_round = state.m_session;
        }
        m_sequence = state.m_sequence;
        var phase = state.m_phase == NetworkTestSession.MiniGamePhase.Countdown ? MiniGameUIBase.Phase.Countdown
            : state.m_phase == NetworkTestSession.MiniGamePhase.Playing ? MiniGameUIBase.Phase.Playing : MiniGameUIBase.Phase.Result;
        m_target.ApplyExternalPhase(phase, state.m_remaining, state.m_duration, state.m_stars, state.m_clear, state.m_hasTimer);
    }

    public void ResetPresentation()
    {
        if (m_target != null) m_target.CloseExternalGame(false);
        if (m_collection != null) m_collection.CancelCollection();
        m_game = m_round = m_sequence = 0;
        m_score = null; m_scoreCanvas = null; m_collection = null;
    }

    private void OnEnable()
    {
        if (m_session != null)
        {
            m_session.MiniGameStateChanged -= ReceiveState;
            m_session.MiniGameStateChanged += ReceiveState;
            FindTarget();
            if (m_target != null) m_target.SetExternalControl(true);
        }
    }

    private void OnDisable()
    {
        if (m_session != null) m_session.MiniGameStateChanged -= ReceiveState;
        ResetPresentation();
        if (m_target != null) m_target.SetExternalControl(false);
    }
}
