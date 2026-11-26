using System;
using System.Linq;
using UnityEngine;

namespace Sawada.Cordex
{
    public sealed partial class NetworkTestSession
    {
        bool waitingForLobby, launchedFromLobby;
        internal bool LobbyWorldReady => world != null;
        internal string LobbyCatalog => SessionCatalog;

        internal void AdoptLobbyConnection(bool asHost, bool online, int playerId,
            RelayTestTransport transport, INetworkTestPeer hostPeer, INetworkTestPeer[] peers, int[] ids)
        {
            if (!waitingForLobby || Busy || world == null || peers.Length != ids.Length)
                throw new InvalidOperationException("The game session cannot adopt this lobby connection.");
            internetMode = online;
            relay = transport;
            server = hostPeer;
            hosting = asHost;
            localId = playerId;
            roomAnnounced = asHost && online;
            launchedFromLobby = true;
            float now = Time.realtimeSinceStartup;
            lastReceive = started = now;
            world.SetHost(asHost);
            if (asHost)
            {
                states.Add(1, SpawnState(1));
                for (int i = 0; i < peers.Length; i++)
                {
                    connections.Add(new Connection { peer = peers[i], id = ids[i], lastReceive = now });
                    states.Add(ids[i], SpawnState(ids[i]));
                }
            }
            waitingForLobby = false;
            SetStatus("Game started from Player Lobby as P" + playerId);
        }
    }
}
