using System;
using System.Collections.Generic;
using System.Linq;

namespace Sawada.Cordex
{
    // Host owns the participant snapshot and accepts one answer per player and round.
    public sealed class ScoreRankingCoordinator
    {
        public const float SlowScale = .2f;
        public const float MinimumDuration = 1f;
        public const float SubmissionTimeout = 3f;

        [Serializable] public sealed class Entry
        {
            public int id, score, rank;
            public bool submitted;
        }

        readonly HashSet<int> participants = new HashSet<int>();
        readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        float startedAt;
        public int RoundId { get; private set; }
        public bool IsCollecting { get; private set; }

        public bool Begin(IEnumerable<int> playerIds, float now)
        {
            if (IsCollecting || RoundId == int.MaxValue) return false;
            participants.Clear();
            participants.UnionWith(playerIds);
            if (participants.Count == 0) return false;
            scores.Clear();
            RoundId++;
            startedAt = now;
            IsCollecting = true;
            return true;
        }

        public void Submit(int roundId, int playerId, int score)
        {
            if (!IsCollecting || roundId != RoundId || !participants.Contains(playerId)
                || scores.ContainsKey(playerId) || score < 0) return;
            scores.Add(playerId, score);
        }

        public void RemoveDisconnected(int playerId)
        {
            // Keep the participant visible as missing, even if they answered before leaving.
            scores.Remove(playerId);
        }

        public bool TryFinish(float now, out Entry[] result)
        {
            result = null;
            if (!IsCollecting || now - startedAt < MinimumDuration
                || (scores.Count != participants.Count && now - startedAt < SubmissionTimeout)) return false;
            result = participants.Select(id => new Entry { id = id,
                submitted = scores.ContainsKey(id), score = scores.TryGetValue(id, out int score) ? score : 0 })
                .OrderByDescending(e => e.submitted).ThenByDescending(e => e.score).ThenBy(e => e.id).ToArray();
            int rank = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (!result[i].submitted) continue;
                if (i == 0 || result[i].score != result[i - 1].score) rank = i + 1;
                result[i].rank = rank;
            }
            IsCollecting = false;
            return true;
        }

        public void Reset()
        {
            IsCollecting = false;
            RoundId = 0;
            participants.Clear();
            scores.Clear();
        }
    }
}
