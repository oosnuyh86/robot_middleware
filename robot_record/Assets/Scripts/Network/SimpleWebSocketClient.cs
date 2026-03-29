using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RobotMiddleware.Network
{
    public class SimpleWebSocketClient
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly string _url;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessage;
        public event Action<string> OnError;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public SimpleWebSocketClient(string url)
        {
            _url = url;
        }

        public async void Connect()
        {
            try
            {
                _ws = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                Debug.Log($"[SimpleWebSocketClient] Connecting to {_url}...");
                await _ws.ConnectAsync(new Uri(_url), _cts.Token);
                Debug.Log("[SimpleWebSocketClient] Connected");
                OnConnected?.Invoke();
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleWebSocketClient] Connect failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        public async void Send(string message)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                Debug.LogWarning("[SimpleWebSocketClient] Cannot send, not connected");
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleWebSocketClient] Send failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        OnMessage?.Invoke(msg);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[SimpleWebSocketClient] Server closed connection");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown via CancellationToken
            }
            catch (WebSocketException ex)
            {
                Debug.LogError($"[SimpleWebSocketClient] WebSocket error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }

            OnDisconnected?.Invoke();
        }

        public async void Disconnect()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SimpleWebSocketClient] Close error: {ex.Message}");
                }
            }

            _ws?.Dispose();
            _cts?.Dispose();
        }
    }
}
