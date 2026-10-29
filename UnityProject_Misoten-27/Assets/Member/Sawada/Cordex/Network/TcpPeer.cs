using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sawada.Cordex
{
    // Length-prefixed UTF-8 frames. All Unity APIs and JSON parsing stay on the main thread.
    internal sealed class TcpPeer : IDisposable
    {
        const int MaxFrame = 8192;
        readonly TcpClient client;
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim ready = new SemaphoreSlim(0);
        int receivedCount, sendCount, closed;
        public string Error { get; private set; }
        public bool Closed => Volatile.Read(ref closed) != 0;

        public TcpPeer(TcpClient socket)
        {
            client = socket;
            client.NoDelay = true;
            _ = ReadLoop();
            _ = WriteLoop();
        }

        public bool TryRead(out string message)
        {
            if (!incoming.TryDequeue(out message)) return false;
            Interlocked.Decrement(ref receivedCount);
            return true;
        }

        public void Send(string message)
        {
            if (Closed) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            if (data.Length > MaxFrame || Interlocked.Increment(ref sendCount) > 128)
            {
                Fail("Send queue exceeded limit (slow connection)");
                return;
            }
            outgoing.Enqueue(data);
            ready.Release();
        }

        async Task ReadExact(NetworkStream stream, byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = await stream.ReadAsync(bytes, offset, bytes.Length - offset).ConfigureAwait(false);
                if (count == 0) throw new IOException("Remote endpoint closed the connection");
                offset += count;
            }
        }

        async Task ReadLoop()
        {
            try
            {
                var stream = client.GetStream();
                var header = new byte[4];
                while (!Closed)
                {
                    await ReadExact(stream, header).ConfigureAwait(false);
                    int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
                    if (length <= 0 || length > MaxFrame) throw new IOException("Invalid frame length");
                    var data = new byte[length];
                    await ReadExact(stream, data).ConfigureAwait(false);
                    if (Interlocked.Increment(ref receivedCount) > 128)
                        throw new IOException("Receive queue exceeded limit");
                    incoming.Enqueue(Encoding.UTF8.GetString(data));
                }
            }
            catch (Exception e) { Fail(e.Message); }
        }

        async Task WriteLoop()
        {
            try
            {
                var stream = client.GetStream();
                while (!Closed)
                {
                    await ready.WaitAsync().ConfigureAwait(false);
                    if (Closed) break;
                    if (!outgoing.TryDequeue(out var data)) continue;
                    Interlocked.Decrement(ref sendCount);
                    byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
                    await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                    await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }
            }
            catch (Exception e) { Fail(e.Message); }
        }

        void Fail(string reason)
        {
            Error = reason;
            Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            client.Close();
            ready.Release(); // Wake the writer when disconnected while idle.
        }
    }
}
