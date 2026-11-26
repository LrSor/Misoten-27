using System.Linq;
using UnityEngine;

namespace Sawada.Cordex
{
    public sealed partial class NetworkTestSession
    {
        bool matchEnding, matchClockSaved;
        int finalRankingRound;
        float matchTimeSendAt, matchPreviousScale, matchPreviousFixedDelta;
        GameTimeManager matchTimer;
        public bool CanAdvanceMatchTimer => !waitingForLobby && localId > 0 && Busy && !matchEnding;

        public void FinishTimedMatch()
        {
            if (!hosting || matchEnding || waitingForLobby) return;
            FreezeMatch();
            BroadcastRanking(new Message { kind = "match-finish" });
        }

        void FreezeMatch()
        {
            if (matchEnding) return;
            rankingSlow.Restore();
            matchPreviousScale = Time.timeScale;
            matchPreviousFixedDelta = Time.fixedDeltaTime;
            matchClockSaved = matchEnding = true;
            Time.timeScale = 0f;
            if (matchTimer == null) matchTimer = FindFirstObjectByType<GameTimeManager>();
            matchTimer?.CompleteFromHost();
            Debug.Log("[MatchResult] TIME UP P" + localId);
        }

        void TickTimedMatch(float now)
        {
            if (matchTimer == null) matchTimer = FindFirstObjectByType<GameTimeManager>();
            if (hosting && !matchEnding && matchTimer != null && localId > 0 && now >= matchTimeSendAt)
            {
                matchTimeSendAt = now + .25f;
                BroadcastRanking(new Message { kind = "match-time", time = matchTimer.GameTimer });
            }
            if (!matchEnding) return;
            Time.timeScale = 0f;
            if (!hosting) return;
            if (finalRankingRound == 0 && !rankingWaiting && !ranking.IsCollecting)
            {
                BeginRanking(now);
                finalRankingRound = rankingId;
            }
            else if (finalRankingRound > 0 && completedRankingId == finalRankingRound && !rankingWaiting)
            {
                var result = LastRanking.ToArray();
                BroadcastRanking(new Message { kind = "match-result", ranking = result });
                TransferToResults(result);
            }
        }

        void TransferToResults(ScoreRankingCoordinator.Entry[] result)
        {
            bool asHost = hosting, online = internetMode;
            int playerId = localId;
            var transport = relay;
            var hostPeer = server;
            var active = connections.Where(c => c.id > 0 && c.closeAt == 0 && !c.peer.Closed).ToArray();
            var peers = active.Select(c => c.peer).ToArray();
            var ids = active.Select(c => c.id).ToArray();
            string room = relay != null ? relay.JoinCode : hostLanCode;
            int port = listener != null ? ((System.Net.IPEndPoint)listener.LocalEndpoint).Port : 0;
            // The persistent flow takes ownership. StopSession must never dispose these peers.
            relay = null; server = null;
            foreach (var c in connections.Where(c => !active.Contains(c))) c.peer.Dispose();
            connections.Clear();
            StopSession("Showing match results");
            waitingForLobby = true;
            TitleLobbyFlow.GetOrCreate().AcceptMatchResult(asHost, online, playerId, transport, hostPeer, peers, ids, result, room, port);
        }

        void RestoreMatchClock()
        {
            if (matchClockSaved)
            {
                Time.timeScale = matchPreviousScale;
                Time.fixedDeltaTime = matchPreviousFixedDelta;
            }
            matchClockSaved = matchEnding = false;
            finalRankingRound = 0;
        }
    }
}
