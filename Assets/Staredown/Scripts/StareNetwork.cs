using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Staredown
{
    [Serializable] public class WireMessage
    {
        public string kind, name, error; public int id, blink, prop; public bool valid, ready, demo, dancing;
        public float left, right, yaw, pitch; public Vector3 origin, velocity; public ArenaState state;
        public int avatar,skin,dance; public string avatarUserId;
        public string pose;
        public Vector3 roomPosition; public float roomYaw; public bool hasRoomPose;
        public StarePresenceFrame presence;
        public bool camera; public string cameraFrame; public int cameraSequence;
        public bool capturedBlinks; public StareCapturedBlink blinkEvent;
    }
    // Small LAN transport: length-prefixed UTF-8, bounded packets/queues, host-owned tournament.
    // Phone face previews use separate bounded camera-frame messages.
    public interface IStareTransport : IDisposable
    {
        string Error { get; }
        void Tick();
        bool Poll(out StareNetwork.Incoming incoming);
        void Send(int target, WireMessage message);
        void Broadcast(WireMessage message);
        void ClosePeer(int id);
    }
    public sealed class StareNetwork : IStareTransport
    {
        public const int Port = 47777; const int MaxPacket = 65536;
        sealed class Peer
        {
            public TcpClient socket; public int id; public volatile bool alive = true;
            public readonly object sendLock = new object();
            public readonly ConcurrentQueue<string> outbound = new ConcurrentQueue<string>();
            public int queued;
        }
        public struct Incoming { public int sender; public string json; }
        public readonly ConcurrentQueue<Incoming> Inbox = new ConcurrentQueue<Incoming>();
        readonly ConcurrentDictionary<int, Peer> peers = new ConcurrentDictionary<int, Peer>();
        TcpListener listener; volatile bool running; int nextId, inboundCount;
        public bool IsHost { get; private set; } public string Error { get; private set; }
        public void Tick() { }
        public void Host()
        {
            Dispose(); IsHost = true; running = true; nextId = 0; Error = null;
            listener = new TcpListener(IPAddress.Any, Port); listener.Start(Tournament.Capacity);
            new Thread(Accept) { IsBackground = true, Name = "Staredown accept" }.Start();
        }
        public void Join(string address)
        {
            Dispose(); IsHost = false; running = true; Error = null;
            new Thread(() =>
            {
                try
                {
                    var socket = new TcpClient(); var task = socket.ConnectAsync(address, Port);
                    if (!task.Wait(5000)) { socket.Close(); throw new IOException("Connection timed out"); }
                    if (!running) { socket.Close(); return; }
                    AddPeer(socket, 0); Enqueue(-1, "{\"kind\":\"connected\"}");
                }
                catch (Exception e) { Error = e.Message; Enqueue(-1, "{\"kind\":\"failed\"}"); }
            }) { IsBackground = true, Name = "Staredown connect" }.Start();
        }
        void Accept()
        {
            while (running)
            {
                try
                {
                    var s = listener.AcceptTcpClient();
                    if (peers.Count >= Tournament.Capacity-1) { s.Close(); continue; }
                    int id = Interlocked.Increment(ref nextId); AddPeer(s, id);
                    Enqueue(id, "{\"kind\":\"connected\"}");
                }
                catch { if (!running) return; }
            }
        }
        void AddPeer(TcpClient socket, int id)
        {
            socket.NoDelay = true; socket.SendTimeout = 2500; socket.ReceiveTimeout = 15000;
            var p = new Peer { socket = socket, id = id }; peers[id] = p;
            new Thread(() => Read(p)) { IsBackground = true, Name = "Staredown receive" }.Start();
            new Thread(() => Write(p)) { IsBackground = true, Name = "Staredown send" }.Start();
        }
        void Read(Peer p)
        {
            try
            {
                var stream = p.socket.GetStream(); var header = new byte[4];
                while (running && p.alive)
                {
                    ReadExactly(stream, header, 4); int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
                    int limit = IsHost ? 8192 : MaxPacket;
                    if (length <= 0 || length > limit) throw new IOException("Packet limit");
                    var bytes = new byte[length]; ReadExactly(stream, bytes, length);
                    if (!Enqueue(p.id, Encoding.UTF8.GetString(bytes))) throw new IOException("Input queue limit");
                }
            }
            catch { }
            finally { ClosePeer(p.id); Enqueue(p.id, "{\"kind\":\"disconnected\"}"); }
        }
        static void ReadExactly(Stream s, byte[] bytes, int count)
        { int offset = 0; while (offset < count) { int n = s.Read(bytes, offset, count - offset); if (n == 0) throw new EndOfStreamException(); offset += n; } }
        bool Enqueue(int sender, string json)
        {
            if (Interlocked.Increment(ref inboundCount) > 512) { Interlocked.Decrement(ref inboundCount); return false; }
            Inbox.Enqueue(new Incoming { sender = sender, json = json }); return true;
        }
        public bool Poll(out Incoming incoming)
        { if (!Inbox.TryDequeue(out incoming)) return false; Interlocked.Decrement(ref inboundCount); return true; }
        void Write(Peer p)
        {
            try
            {
                var stream = p.socket.GetStream();
                while (running && p.alive)
                {
                    if (!p.outbound.TryDequeue(out string json)) { Thread.Sleep(5); continue; }
                    Interlocked.Decrement(ref p.queued); var data = Encoding.UTF8.GetBytes(json);
                    var header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
                    stream.Write(header, 0, 4); stream.Write(data, 0, data.Length);
                }
            }
            catch { ClosePeer(p.id); }
        }
        public void Send(int target, WireMessage message)
        {
            if (!peers.TryGetValue(target, out var p)) return;
            string json = JsonUtility.ToJson(message);
            if (Interlocked.Increment(ref p.queued) > 32) { Interlocked.Decrement(ref p.queued); ClosePeer(target); return; }
            p.outbound.Enqueue(json);
        }
        public void Broadcast(WireMessage message) { foreach (int id in peers.Keys) Send(id, message); }
        public void ClosePeer(int id)
        { if (peers.TryRemove(id, out var p)) { p.alive = false; try { p.socket.Close(); } catch { } } }
        public void Dispose()
        {
            running = false; try { listener?.Stop(); } catch { } listener = null;
            foreach (int id in peers.Keys) ClosePeer(id);
            while (Inbox.TryDequeue(out _)) { } inboundCount = 0;
        }
    }
}
