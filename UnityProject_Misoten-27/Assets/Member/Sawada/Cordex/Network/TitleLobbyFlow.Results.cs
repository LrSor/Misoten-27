using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sawada.Cordex
{
    public sealed partial class TitleLobbyFlow
    {
        public const string ResultScene = "Result_Kari";
        bool returningFromGame, atLobby;
        int[] lobbyMembers = Array.Empty<int>();
        public IReadOnlyList<ScoreRankingCoordinator.Entry> ResultEntries { get; private set; }
            = Array.Empty<ScoreRankingCoordinator.Entry>();
        public bool IsPlayerInLobby(int id) => lobbyMembers.Contains(id);

        internal static TitleLobbyFlow GetOrCreate()
        {
            if (instance == null) new GameObject("TitleLobbyFlow").AddComponent<TitleLobbyFlow>();
            return instance;
        }

        internal void AcceptMatchResult(bool asHost, bool online, int playerId, RelayTestTransport transport,
            INetworkTestPeer hostPeer, INetworkTestPeer[] peers, int[] ids, ScoreRankingCoordinator.Entry[] entries,
            string room, int port)
        {
            host = asHost; internet = online; localId = playerId;
            relay = transport; server = hostPeer;
            busy = inRoom = returningFromGame = true;
            loading = handedOff = menu = localReady = leaving = hostPrepared = atLobby = false;
            game = null;
            if (!string.IsNullOrEmpty(room)) code = room;
            if (port > 0) portInput = port.ToString();
            ResultEntries = entries.Select(e => new ScoreRankingCoordinator.Entry
                { id = e.id, score = e.score, rank = e.rank, submitted = e.submitted }).ToArray();
            members = asHost ? new[] { 1 }.Concat(ids).OrderBy(id => id).ToArray()
                : entries.Select(e => e.id).OrderBy(id => id).ToArray();
            preparedMembers = lobbyMembers = Array.Empty<int>();
            float now = Time.realtimeSinceStartup;
            began = lastReceive = now;
            heartbeat = 0f;
            rosterRevision++;
            for (int i = 0; i < peers.Length; i++)
                guests.Add(new Guest { peer = peers[i], id = ids[i], admissionOrder = ++nextAdmission, received = now });
            Debug.Log("[MatchResult] RESULT P" + localId + " entries=" + ResultEntries.Count);
            LoadScene(ResultScene);
        }

        void ArrivedInLobby()
        {
            atLobby = true;
            if (host)
            {
                if (!internet && listener == null)
                {
                    try
                    {
                        listener = new TcpListener(IPAddress.Any, int.Parse(portInput));
                        listener.Start(8);
                    }
                    catch (Exception e) { Fail("Could not reopen the LAN room: " + e.Message); return; }
                }
                BroadcastRoom();
            }
            else Send(server, new Packet { kind = "lobby-arrived" });
            Debug.Log("[MatchResult] LOBBY P" + localId);
        }

        public void ReturnFromResult()
        {
            if (SceneManager.GetActiveScene().name != ResultScene || (sceneLoad != null && !sceneLoad.isDone)) return;
            if (!busy) { LoadScene(LobbyScene); return; }
            SetStatus("Returning to the room lobby.");
            LoadScene(LobbyScene);
        }
    }
}
