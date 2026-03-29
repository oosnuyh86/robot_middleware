using System;
using UnityEngine;

namespace RobotMiddleware.DataChannel
{
    public class CommandHandler
    {
        public delegate void OnCommandReceivedDelegate(CommandMessage command);
        public delegate void OnParseErrorDelegate(string error);

        public event OnCommandReceivedDelegate OnCommandReceived;
        public event OnParseErrorDelegate OnParseError;

        public void HandleMessage(string messageJson)
        {
            if (string.IsNullOrEmpty(messageJson))
            {
                OnParseError?.Invoke("Empty message");
                return;
            }

            CommandMessage command = CommandMessage.FromJson(messageJson);
            if (command == null)
            {
                OnParseError?.Invoke($"Failed to parse message: {messageJson}");
                return;
            }

            Debug.Log($"[CommandHandler] Received command: {command.action}");
            OnCommandReceived?.Invoke(command);
        }

        public void SendAck(string commandId)
        {
            Debug.Log($"[CommandHandler] ACK sent for command: {commandId}");
        }
    }
}
