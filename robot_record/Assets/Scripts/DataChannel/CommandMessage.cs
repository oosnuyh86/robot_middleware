using System;
using UnityEngine;

namespace RobotMiddleware.DataChannel
{
    public enum CommandAction
    {
        START_SCAN = 0,
        ALIGN_SENSORS = 1,
        START_RECORD = 2,
        STOP = 3,
        START_TRAINING = 4,
        APPROVE_VALIDATION = 5,
        START_EXECUTION = 6,
        MARK_FAILED = 7,
        START_VALIDATING = 8,
        CAPTURE_BACKGROUND = 9,
        START_OBJECT_SCAN = 10,
        CONFIRM_SCAN = 11,
        RESCAN = 12
    }

    [System.Serializable]
    public class CommandMessage
    {
        public string id;
        public string type;
        public string action;
        public string timestamp;
        public string payload;
        public string clientId;

        public CommandMessage()
        {
            id = System.Guid.NewGuid().ToString();
            type = "COMMAND";
            timestamp = System.DateTime.UtcNow.ToString("O");
            clientId = SystemInfo.deviceUniqueIdentifier;
        }

        public CommandMessage(CommandAction commandAction, string payloadData = "")
        {
            id = System.Guid.NewGuid().ToString();
            type = "COMMAND";
            action = commandAction.ToString();
            timestamp = System.DateTime.UtcNow.ToString("O");
            payload = payloadData;
            clientId = SystemInfo.deviceUniqueIdentifier;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static CommandMessage FromJson(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                    return null;

                CommandMessage msg = JsonUtility.FromJson<CommandMessage>(json);
                return msg;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandMessage] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }
    }
}
