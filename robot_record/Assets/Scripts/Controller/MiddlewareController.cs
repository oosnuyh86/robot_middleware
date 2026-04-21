using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using RobotMiddleware.DataChannel;
using RobotMiddleware.Network;
using RobotMiddleware.Recording;
using RobotMiddleware.Config;
using RobotMiddleware.Models;

namespace RobotMiddleware.Controller
{
    public class MiddlewareController : MonoBehaviour
    {
        [SerializeField] private RecordingManager _recordingManager;
        public string relayUrl;
        public string roomName = "middleware";

        private CommandHandler _commandHandler;
        private SimpleWebSocketClient _wsClient;
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        public event Action<string> OnMessageReceived;
        public event Action<string> OnMessageSent;
        public event Action<string> OnStatusChanged;
        public bool IsConnected => _wsClient != null && _wsClient.IsConnected;

        private void Awake()
        {
            _commandHandler = new CommandHandler();
            _commandHandler.OnCommandReceived += HandleCommand;
            _commandHandler.OnParseError += OnParseError;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(relayUrl))
            {
                relayUrl = BackendConfig.Instance.RelayUrl;
            }

            string url = relayUrl.Contains("?")
                ? $"{relayUrl}&role=unity"
                : $"{relayUrl}?role=unity";

            OnStatusChanged?.Invoke("Connecting to relay...");
            Debug.Log($"[MiddlewareController] Connecting to relay: {url}");

            _wsClient = new SimpleWebSocketClient(url);
            _wsClient.OnConnected += () =>
            {
                Debug.Log("[MiddlewareController] Connected to relay");
                OnStatusChanged?.Invoke("Connected to relay");
            };
            _wsClient.OnDisconnected += () =>
            {
                Debug.Log("[MiddlewareController] Disconnected from relay");
                OnStatusChanged?.Invoke("Disconnected from relay");
            };
            _wsClient.OnMessage += (msg) =>
            {
                _messageQueue.Enqueue(msg);
            };
            _wsClient.OnError += (err) =>
            {
                Debug.LogError($"[MiddlewareController] WebSocket error: {err}");
                OnStatusChanged?.Invoke($"WebSocket error: {err}");
            };

            _wsClient.Connect();
        }

        private void Update()
        {
            // Pump the WebSocket client to dispatch messages on main thread
            _wsClient?.ProcessMessages();

            while (_messageQueue.TryDequeue(out string msg))
            {
                Debug.Log($"[MiddlewareController] Received: {msg}");
                OnMessageReceived?.Invoke(msg);

                if (!msg.Contains("\"type\":\"COMMAND\""))
                {
                    Debug.Log($"[MiddlewareController] Skipping non-command: {msg.Substring(0, Math.Min(msg.Length, 80))}");
                    continue;
                }

                _commandHandler.HandleMessage(msg);
            }
        }

        private void HandleCommand(CommandMessage cmd)
        {
            if (cmd == null) return;

            // Extract recordId from payload if present
            if (!string.IsNullOrEmpty(cmd.payload) && _recordingManager != null)
            {
                TryExtractRecordId(cmd.payload);
            }

            try
            {
                CommandAction action = (CommandAction)Enum.Parse(typeof(CommandAction), cmd.action);

                switch (action)
                {
                    case CommandAction.START_SCAN:
                        _recordingManager.StartScanning();
                        break;
                    case CommandAction.ALIGN_SENSORS:
                        _recordingManager.AlignSensors();
                        break;
                    case CommandAction.START_RECORD:
                        _recordingManager.StartRecording();
                        break;
                    case CommandAction.STOP:
                        _recordingManager.StopRecording();
                        break;
                    case CommandAction.START_TRAINING:
                        _recordingManager.StartTraining();
                        break;
                    case CommandAction.APPROVE_VALIDATION:
                        _recordingManager.ApproveValidation();
                        break;
                    case CommandAction.START_EXECUTION:
                        _recordingManager.StartExecution();
                        break;
                    case CommandAction.MARK_FAILED:
                        _recordingManager.MarkFailed(cmd.payload);
                        break;
                    case CommandAction.START_VALIDATING:
                        _recordingManager.StartValidating();
                        break;
                    case CommandAction.CAPTURE_BACKGROUND:
                        _recordingManager.CaptureBackground();
                        break;
                    case CommandAction.START_OBJECT_SCAN:
                        _recordingManager.StartObjectScan();
                        break;
                    case CommandAction.CONFIRM_SCAN:
                        _recordingManager.ConfirmScan();
                        break;
                    case CommandAction.RESCAN:
                        _recordingManager.Rescan();
                        break;
                    default:
                        Debug.LogWarning($"[MiddlewareController] Unhandled action: {action}");
                        break;
                }

                // Send ACK back
                SendAck(cmd.id, cmd.action);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[MiddlewareController] Invalid action: {cmd.action} - {ex.Message}");
                SendError(cmd.id, $"Invalid action: {cmd.action}");
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogWarning($"[MiddlewareController] State-gate rejection for {cmd.action}: {ex.Message}");
                SendError(cmd.id, ex.Message);
            }
        }

        private void TryExtractRecordId(string payload)
        {
            // Payload may contain recordId directly or as part of a JSON object
            // Try simple format first: payload is the recordId itself
            if (!payload.Contains("{"))
            {
                _recordingManager.SetRecordId(payload);
                return;
            }

            // Try to extract recordId from JSON payload
            try
            {
                if (payload.Contains("recordId"))
                {
                    int startIdx = payload.IndexOf("\"recordId\":\"") + "\"recordId\":\"".Length;
                    int endIdx = payload.IndexOf("\"", startIdx);
                    if (startIdx > 0 && endIdx > startIdx)
                    {
                        string recordId = payload.Substring(startIdx, endIdx - startIdx);
                        _recordingManager.SetRecordId(recordId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MiddlewareController] Failed to extract recordId from payload: {ex.Message}");
            }
        }

        private void SendAck(string commandId, string action)
        {
            var ack = new CommandMessage
            {
                id = commandId,
                type = "ACK",
                action = action,
                timestamp = DateTime.UtcNow.ToString("O")
            };
            string json = ack.ToJson();
            SendResponse(json);
            _commandHandler.SendAck(commandId);
        }

        private void SendError(string commandId, string errorMessage)
        {
            var error = new CommandMessage
            {
                id = commandId,
                type = "ERROR",
                action = "ERROR",
                payload = errorMessage,
                timestamp = DateTime.UtcNow.ToString("O")
            };
            SendResponse(error.ToJson());
        }

        public void SendResponse(string json)
        {
            if (_wsClient == null || !_wsClient.IsConnected)
            {
                Debug.LogWarning("[MiddlewareController] Not connected. Cannot send response.");
                return;
            }

            _wsClient.Send(json);
            Debug.Log($"[MiddlewareController] Sent: {json}");
            OnMessageSent?.Invoke(json);
        }

        private void OnParseError(string error)
        {
            Debug.LogError($"[MiddlewareController] Parse error: {error}");
            SendError("parse_error", error);
        }

        private void OnDestroy()
        {
            if (_commandHandler != null)
            {
                _commandHandler.OnCommandReceived -= HandleCommand;
                _commandHandler.OnParseError -= OnParseError;
            }

            _wsClient?.Disconnect();
        }
    }
}
