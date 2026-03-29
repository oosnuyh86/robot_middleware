using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace RobotMiddleware.Network
{
    /// <summary>
    /// Lightweight WebSocket client using raw TcpClient.
    /// Avoids System.Net.WebSockets.ClientWebSocket which deadlocks in Unity.
    /// Supports text frames only (sufficient for JSON command relay).
    /// </summary>
    public class SimpleWebSocketClient
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Thread _thread;
        private volatile bool _connected;
        private volatile bool _stopping;
        private readonly string _url;

        private readonly ConcurrentQueue<string> _incomingMessages = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _outgoingMessages = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<Action> _eventQueue = new ConcurrentQueue<Action>();

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessage;
        public event Action<string> OnError;

        public bool IsConnected => _connected;
        public int PendingMessageCount => _incomingMessages.Count;

        public SimpleWebSocketClient(string url) { _url = url; }

        public void Connect()
        {
            if (_connected || _thread != null) return;
            _stopping = false;
            _thread = new Thread(Run) { IsBackground = true, Name = "WS-Client" };
            _thread.Start();
        }

        public void Send(string message)
        {
            if (!_connected) return;
            _outgoingMessages.Enqueue(message);
        }

        public void ProcessMessages()
        {
            while (_eventQueue.TryDequeue(out var evt))
                try { evt(); } catch { }
            while (_incomingMessages.TryDequeue(out var msg))
                try { OnMessage?.Invoke(msg); } catch { }
        }

        public void Disconnect()
        {
            _stopping = true;
            _connected = false;
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            if (_thread != null && _thread.IsAlive)
                _thread.Join(2000);
            _thread = null;
        }

        // ═══ Background Thread ═══

        private void Run()
        {
            try
            {
                // Parse URL: ws://host:port/path
                var uri = new Uri(_url);
                string host = uri.Host;
                int port = uri.Port > 0 ? uri.Port : (uri.Scheme == "wss" ? 443 : 80);
                string path = uri.PathAndQuery;

                // TCP connect
                _tcp = new TcpClient();
                _tcp.Connect(host, port);
                _stream = _tcp.GetStream();
                _tcp.ReceiveTimeout = 50; // 50ms timeout for Socket.Receive

                // WebSocket handshake
                string key = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                string handshake =
                    $"GET {path} HTTP/1.1\r\n" +
                    $"Host: {host}:{port}\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Key: {key}\r\n" +
                    "Sec-WebSocket-Version: 13\r\n" +
                    "\r\n";

                byte[] handshakeBytes = Encoding.UTF8.GetBytes(handshake);
                _stream.Write(handshakeBytes, 0, handshakeBytes.Length);
                _stream.Flush();

                // Read handshake response
                string response = ReadHttpResponse();
                if (!response.Contains("101"))
                {
                    _eventQueue.Enqueue(() => OnError?.Invoke($"Handshake failed: {response.Substring(0, Math.Min(response.Length, 100))}"));
                    return;
                }

                _connected = true;
                Console.WriteLine("[WS-BG] Connected. DataAvailable=" + _stream.DataAvailable);
                _eventQueue.Enqueue(() =>
                {
                    Debug.Log("[SimpleWebSocketClient] Connected");
                    OnConnected?.Invoke();
                });

                // Main loop: send + receive
                while (_connected && !_stopping)
                {
                    // Send queued messages
                    while (_outgoingMessages.TryDequeue(out string outMsg))
                    {
                        SendFrame(outMsg);
                    }

                    // Try to read a frame (uses Socket.Receive with 50ms timeout)
                    string incoming = TryReadFrame();
                    if (incoming != null)
                    {
                        Console.WriteLine("[WS-BG] FRAME: " + incoming.Substring(0, Math.Min(incoming.Length, 80)));
                        _incomingMessages.Enqueue(incoming);
                    }
                }
            }
            catch (Exception ex) when (!_stopping)
            {
                _eventQueue.Enqueue(() => OnError?.Invoke(ex.Message));
            }
            finally
            {
                _connected = false;
                _eventQueue.Enqueue(() => OnDisconnected?.Invoke());
                try { _stream?.Close(); } catch { }
                try { _tcp?.Close(); } catch { }
            }
        }

        private string ReadHttpResponse()
        {
            var sb = new StringBuilder();
            byte[] buf = new byte[1];
            int crlfCount = 0;

            // Read until \r\n\r\n
            while (crlfCount < 4)
            {
                int read = _stream.Read(buf, 0, 1);
                if (read == 0) break;
                sb.Append((char)buf[0]);
                if (buf[0] == '\r' || buf[0] == '\n') crlfCount++;
                else crlfCount = 0;
            }
            return sb.ToString();
        }

        private void SendFrame(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int len = payload.Length;

            // Frame header: FIN=1, opcode=1 (text), MASK=1 (client must mask)
            byte[] header;
            if (len <= 125)
            {
                header = new byte[] { 0x81, (byte)(0x80 | len) };
            }
            else if (len <= 65535)
            {
                header = new byte[] { 0x81, 0xFE,
                    (byte)(len >> 8), (byte)(len & 0xFF) };
                header[1] |= 0x80;
            }
            else
            {
                header = new byte[10];
                header[0] = 0x81;
                header[1] = (byte)(0x80 | 127);
                for (int i = 0; i < 8; i++)
                    header[9 - i] = (byte)((long)len >> (8 * i));
                return; // Skip extremely large messages
            }

            // Masking key (required for client→server)
            byte[] mask = new byte[4];
            new System.Random().NextBytes(mask);

            // Mask payload
            byte[] masked = new byte[payload.Length];
            for (int i = 0; i < payload.Length; i++)
                masked[i] = (byte)(payload[i] ^ mask[i % 4]);

            try
            {
                _stream.Write(header, 0, header.Length);
                _stream.Write(mask, 0, 4);
                _stream.Write(masked, 0, masked.Length);
                _stream.Flush();
            }
            catch (Exception)
            {
                _connected = false;
            }
        }

        private string TryReadFrame()
        {
            try
            {
                // Read frame header (2 bytes)
                byte[] header = new byte[2];
                int read = ReadExact(header, 2);
                if (read < 2) return null;

                byte opcode = (byte)(header[0] & 0x0F);
                bool fin = (header[0] & 0x80) != 0;
                bool masked = (header[1] & 0x80) != 0;
                long payloadLen = header[1] & 0x7F;

                if (opcode == 0x08) // Close frame
                {
                    _connected = false;
                    return null;
                }

                if (opcode == 0x09) // Ping
                {
                    // Send pong
                    byte[] pong = new byte[] { 0x8A, 0x80, 0, 0, 0, 0 }; // Empty masked pong
                    _stream.Write(pong, 0, pong.Length);
                    return null;
                }

                // Extended payload length
                if (payloadLen == 126)
                {
                    byte[] ext = new byte[2];
                    ReadExact(ext, 2);
                    payloadLen = (ext[0] << 8) | ext[1];
                }
                else if (payloadLen == 127)
                {
                    byte[] ext = new byte[8];
                    ReadExact(ext, 8);
                    payloadLen = 0;
                    for (int i = 0; i < 8; i++)
                        payloadLen = (payloadLen << 8) | ext[i];
                }

                // Mask key (if server masks, which is unusual)
                byte[] maskKey = null;
                if (masked)
                {
                    maskKey = new byte[4];
                    ReadExact(maskKey, 4);
                }

                // Read payload
                if (payloadLen > 1024 * 1024) return null; // Skip >1MB
                byte[] payload = new byte[payloadLen];
                ReadExact(payload, (int)payloadLen);

                // Unmask if needed
                if (masked && maskKey != null)
                {
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= maskKey[i % 4];
                }

                if (opcode == 0x01) // Text frame
                {
                    return Encoding.UTF8.GetString(payload);
                }
                if (opcode == 0x02) // Binary frame
                {
                    return Encoding.UTF8.GetString(payload); // Try to decode as UTF-8 anyway
                }

                return null; // Other — skip
            }
            catch (IOException) // Read timeout
            {
                return null;
            }
            catch (SocketException) // Socket timeout — no data available
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                _connected = false;
                return null;
            }
            catch (Exception ex)
            {
                // Log unexpected errors to help debug
                _eventQueue.Enqueue(() => Debug.LogWarning($"[SimpleWebSocketClient] Read error: {ex.GetType().Name}: {ex.Message}"));
                return null;
            }
        }

        private int ReadExact(byte[] buffer, int count)
        {
            int total = 0;
            var socket = _tcp.Client;
            while (total < count)
            {
                try
                {
                    int read = socket.Receive(buffer, total, count - total, SocketFlags.None);
                    if (read == 0) return total; // Connection closed
                    total += read;
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.TimedOut)
                {
                    // Timeout on first byte means no data — throw to caller
                    if (total == 0) throw;
                    // Timeout mid-frame — keep trying (data is partial)
                    continue;
                }
            }
            return total;
        }
    }
}
