using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Sawada.Cordex
{
    // Main-thread UTP adapter. Gameplay uses the same messages as the LAN TCP connection.
    internal sealed class RelayTestTransport : IDisposable
    {
        const int MaxMessage = 8192;
        const int MaxQueued = 128;
        static Task initialization;
        NetworkDriver driver;
        NetworkPipeline pipeline;
        readonly Dictionary<NetworkConnection, Peer> peers = new Dictionary<NetworkConnection, Peer>();
        readonly Queue<INetworkTestPeer> accepted = new Queue<INetworkTestPeer>();
        bool disposed, listening;
        Peer client;
        public string JoinCode { get; private set; } = "";
        public string Error { get; private set; }
        public bool Ready => driver.IsCreated && driver.GetRelayConnectionStatus() == RelayConnectionStatus.Established;
        public bool ClientConnected => client != null && client.Connected && !client.Closed;
        public INetworkTestPeer Client => client;

        static async Task InitializeServices()
        {
            // Each executable has its own anonymous identity, including multi-process PC tests.
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions().SetProfile("net" + Guid.NewGuid().ToString("N").Substring(0, 20));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static Task EnsureServices()
        {
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = InitializeServices();
            return initialization;
        }

        void CheckDisposed()
        {
            if (disposed) throw new OperationCanceledException("Relay connection canceled");
        }

        public async Task StartHostAsync()
        {
            await EnsureServices();
            CheckDisposed();
            var allocation = await RelayService.Instance.CreateAllocationAsync(3);
            CheckDisposed();
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            CheckDisposed();
            CreateDriver(allocation.ToRelayServerData("dtls"));
            if (driver.Listen() != 0) throw new InvalidOperationException("Relay listen failed");
            listening = true;
            JoinCode = code;
        }

        public async Task StartClientAsync(string code)
        {
            await EnsureServices();
            CheckDisposed();
            var allocation = await RelayService.Instance.JoinAllocationAsync(code);
            CheckDisposed();
            var data = allocation.ToRelayServerData("dtls");
            CreateDriver(data);
            var connection = driver.Connect(data.Endpoint);
            if (!connection.IsCreated) throw new InvalidOperationException("Relay connect failed");
            client = new Peer(this, connection);
            peers.Add(connection, client);
        }

        void CreateDriver(RelayServerData data)
        {
            var settings = new NetworkSettings(Allocator.Temp);
            try
            {
                settings.WithRelayParameters(ref data)
                    .WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 30,
                        disconnectTimeoutMS: 10000, heartbeatTimeoutMS: 500)
                    .WithFragmentationStageParameters(payloadCapacity: MaxMessage)
                    .WithReliableStageParameters(windowSize: 64);
                driver = NetworkDriver.Create(settings);
            }
            finally { settings.Dispose(); }
            pipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
                throw new InvalidOperationException("Relay local socket bind failed");
        }

        public bool TryAccept(out INetworkTestPeer peer)
        {
            while (accepted.Count > 0)
            {
                peer = accepted.Dequeue();
                if (!peer.Closed) return true;
            }
            peer = null;
            return false;
        }

        public void Tick()
        {
            if (disposed || !driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (driver.GetRelayConnectionStatus() == RelayConnectionStatus.AllocationInvalid)
            { Error = "Relay allocation expired or became unavailable. Create / join a room again."; return; }
            if (listening)
            {
                for (int i = 0; i < 8; i++)
                {
                    var connection = driver.Accept();
                    if (!connection.IsCreated) break;
                    if (peers.Count >= 8) { driver.Disconnect(connection); continue; }
                    var peer = new Peer(this, connection) { Connected = true };
                    peers.Add(connection, peer);
                    accepted.Enqueue(peer);
                }
            }
            foreach (var pair in peers.ToArray())
            {
                var peer = pair.Value;
                if (!peer.Closed)
                {
                    for (int i = 0; i < MaxQueued && !peer.Closed; i++)
                    {
                        var type = driver.PopEventForConnection(pair.Key, out var reader);
                        if (type == NetworkEvent.Type.Empty) break;
                        if (type == NetworkEvent.Type.Connect) peer.Connected = true;
                        else if (type == NetworkEvent.Type.Disconnect) peer.Fail("Relay peer disconnected");
                        else if (type == NetworkEvent.Type.Data)
                        {
                            if (reader.Length <= 0 || reader.Length > MaxMessage)
                            { peer.Fail("Invalid Relay message length"); break; }
                            using (var bytes = new NativeArray<byte>(reader.Length, Allocator.Temp))
                            {
                                reader.ReadBytes(bytes);
                                peer.Receive(Encoding.UTF8.GetString(bytes.ToArray()));
                            }
                        }
                    }
                    if (!peer.Closed && driver.GetConnectionState(pair.Key) == NetworkConnection.State.Disconnected)
                        peer.Fail("Relay connection lost");
                }
                if (peer.Closed) peers.Remove(pair.Key);
                else if (peer.Connected) Flush(peer);
            }
        }

        void Flush(Peer peer)
        {
            // Keep a bounded queue when the reliable window is temporarily full.
            while (!peer.Closed && peer.Outgoing.Count > 0)
            {
                int result = driver.BeginSend(pipeline, peer.Connection, out var writer);
                if (result == (int)Unity.Networking.Transport.Error.StatusCode.NetworkSendQueueFull) return;
                if (result != 0) { peer.Fail("Relay BeginSend failed: " + result); return; }
                using (var bytes = new NativeArray<byte>(peer.Outgoing.Peek(), Allocator.Temp)) writer.WriteBytes(bytes);
                result = driver.EndSend(writer);
                if (result == (int)Unity.Networking.Transport.Error.StatusCode.NetworkSendQueueFull) return;
                if (result < 0) { peer.Fail("Relay EndSend failed: " + result); return; }
                peer.Outgoing.Dequeue();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var peer in peers.Values.ToArray()) peer.Dispose();
            peers.Clear(); accepted.Clear();
            if (driver.IsCreated)
            {
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            JoinCode = "";
        }

        sealed class Peer : INetworkTestPeer
        {
            readonly RelayTestTransport owner;
            readonly Queue<string> incoming = new Queue<string>();
            public readonly Queue<byte[]> Outgoing = new Queue<byte[]>();
            public readonly NetworkConnection Connection;
            public bool Connected;
            public string Error { get; private set; }
            public bool Closed { get; private set; }
            public Peer(RelayTestTransport owner, NetworkConnection connection)
            { this.owner = owner; Connection = connection; }
            public bool TryRead(out string message)
            {
                message = incoming.Count == 0 ? null : incoming.Dequeue();
                return message != null;
            }
            public void Receive(string message)
            {
                if (incoming.Count >= MaxQueued) { Fail("Relay receive queue exceeded limit"); return; }
                incoming.Enqueue(message);
            }
            public void Send(string message)
            {
                if (Closed) return;
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                if (bytes.Length == 0 || bytes.Length > MaxMessage || Outgoing.Count >= MaxQueued)
                { Fail("Relay send size / queue exceeded limit (slow connection)"); return; }
                Outgoing.Enqueue(bytes);
            }
            public void Fail(string reason) { Error = reason; Dispose(); }
            public void Dispose()
            {
                if (Closed) return;
                Closed = true;
                Outgoing.Clear();
                if (owner.driver.IsCreated) owner.driver.Disconnect(Connection);
            }
        }
    }
}
