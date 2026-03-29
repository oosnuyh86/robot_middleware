using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using RobotMiddleware.Config;

namespace RobotMiddleware.API
{
    public class MiddlewareClient : MonoBehaviour
    {
        [System.Serializable]
        private class StateUpdateRequest
        {
            public string state;
            public string error_reason;
        }

        [System.Serializable]
        private class PresignedUrlRequest
        {
            public string recordId;
            public string fileType;
        }

        [System.Serializable]
        private class PresignedUrlResponse
        {
            public string uploadUrl;
            public string publicUrl;
        }

        public delegate void OnErrorDelegate(string error);
        public delegate void OnSuccessDelegate(string response);

        public event OnErrorDelegate OnError;
        public event OnSuccessDelegate OnSuccess;

        private BackendConfig _config;

        private void Awake()
        {
            _config = BackendConfig.Instance;
        }

        public void PatchRecordState(string recordId, string backendState, string errorReason = null)
        {
            if (string.IsNullOrEmpty(recordId))
            {
                OnError?.Invoke("Invalid recordId");
                return;
            }

            StartCoroutine(PatchRecordStateCoroutine(recordId, backendState, errorReason));
        }

        private IEnumerator PatchRecordStateCoroutine(string recordId, string backendState, string errorReason)
        {
            string url = $"{_config.BaseApiUrl}/records/{recordId}/state";

            StateUpdateRequest request = new StateUpdateRequest
            {
                state = backendState,
                error_reason = errorReason
            };

            string jsonBody = JsonUtility.ToJson(request);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest www = new UnityWebRequest(url, "PATCH"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    OnError?.Invoke($"PATCH failed: {www.error}");
                }
                else
                {
                    OnSuccess?.Invoke(www.downloadHandler.text);
                    Debug.Log($"[MiddlewareClient] State updated to {backendState}");
                }
            }
        }

        public void GetPresignedUrl(string recordId, string fileType)
        {
            if (string.IsNullOrEmpty(recordId) || string.IsNullOrEmpty(fileType))
            {
                OnError?.Invoke("Invalid recordId or fileType");
                return;
            }

            StartCoroutine(GetPresignedUrlCoroutine(recordId, fileType));
        }

        private IEnumerator GetPresignedUrlCoroutine(string recordId, string fileType)
        {
            string url = $"{_config.BaseApiUrl}/uploads/presigned-url";

            PresignedUrlRequest request = new PresignedUrlRequest
            {
                recordId = recordId,
                fileType = fileType
            };

            string jsonBody = JsonUtility.ToJson(request);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    OnError?.Invoke($"Presigned URL request failed: {www.error}");
                }
                else
                {
                    OnSuccess?.Invoke(www.downloadHandler.text);
                    Debug.Log($"[MiddlewareClient] Presigned URL obtained for {recordId}");
                }
            }
        }
    }
}
