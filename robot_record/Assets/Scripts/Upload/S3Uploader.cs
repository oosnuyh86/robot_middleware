using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using RobotMiddleware.API;

namespace RobotMiddleware.Upload
{
    public class S3Uploader : MonoBehaviour
    {
        public delegate void OnUploadCompleteDelegate(string publicUrl);
        public delegate void OnUploadErrorDelegate(string error);

        public event OnUploadCompleteDelegate OnUploadComplete;
        public event OnUploadErrorDelegate OnUploadError;

        private MiddlewareClient _middlewareClient;
        private string _uploadUrl;
        private string _publicUrl;
        private byte[] _pendingData;

        private void Awake()
        {
            _middlewareClient = GetComponent<MiddlewareClient>();
            if (_middlewareClient == null)
            {
                _middlewareClient = gameObject.AddComponent<MiddlewareClient>();
            }

            _middlewareClient.OnSuccess += HandlePresignedUrlResponse;
            _middlewareClient.OnError += HandlePresignedUrlError;
        }

        private void OnDestroy()
        {
            if (_middlewareClient != null)
            {
                _middlewareClient.OnSuccess -= HandlePresignedUrlResponse;
                _middlewareClient.OnError -= HandlePresignedUrlError;
            }
        }

        public void UploadData(byte[] data, string recordId, string fileType)
        {
            if (data == null || data.Length == 0)
            {
                OnUploadError?.Invoke("Data is empty");
                return;
            }

            if (string.IsNullOrEmpty(recordId) || string.IsNullOrEmpty(fileType))
            {
                OnUploadError?.Invoke("recordId or fileType is empty");
                return;
            }

            // Store data for use after async presigned URL response
            _pendingData = data;

            // Step 1: Get presigned URL from backend
            _middlewareClient.GetPresignedUrl(recordId, fileType);
        }

        private void HandlePresignedUrlResponse(string response)
        {
            // Only handle responses that contain presigned URL data
            if (!response.Contains("uploadUrl"))
                return;

            try
            {
                // Parse response to extract upload URL
                // Expected format: {"uploadUrl":"...","publicUrl":"..."}

                int startIdx = response.IndexOf("\"uploadUrl\":\"") + "\"uploadUrl\":\"".Length;
                int endIdx = response.IndexOf("\"", startIdx);
                _uploadUrl = response.Substring(startIdx, endIdx - startIdx);

                if (response.Contains("publicUrl"))
                {
                    startIdx = response.IndexOf("\"publicUrl\":\"") + "\"publicUrl\":\"".Length;
                    endIdx = response.IndexOf("\"", startIdx);
                    _publicUrl = response.Substring(startIdx, endIdx - startIdx);
                }

                Debug.Log($"[S3Uploader] Upload URL received");

                // Step 2: Upload the pending data to the presigned URL
                if (_pendingData != null && _pendingData.Length > 0)
                {
                    UploadToPresignedUrl(_uploadUrl, _pendingData);
                    _pendingData = null;
                }
                else
                {
                    OnUploadError?.Invoke("No pending data to upload");
                }
            }
            catch (Exception ex)
            {
                _pendingData = null;
                OnUploadError?.Invoke($"Failed to parse presigned URL response: {ex.Message}");
            }
        }

        private void HandlePresignedUrlError(string error)
        {
            OnUploadError?.Invoke($"Failed to get presigned URL: {error}");
        }

        public void UploadToPresignedUrl(string presignedUrl, byte[] data)
        {
            if (string.IsNullOrEmpty(presignedUrl))
            {
                OnUploadError?.Invoke("Invalid presignedUrl");
                return;
            }

            if (data == null || data.Length == 0)
            {
                OnUploadError?.Invoke("Data is empty");
                return;
            }

            StartCoroutine(UploadCoroutine(presignedUrl, data));
        }

        private IEnumerator UploadCoroutine(string presignedUrl, byte[] data)
        {
            using (UnityWebRequest www = UnityWebRequest.Put(presignedUrl, data))
            {
                www.SetRequestHeader("Content-Type", "application/octet-stream");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    OnUploadError?.Invoke($"Upload failed: {www.error}");
                    Debug.LogError($"[S3Uploader] Upload error: {www.error}");
                }
                else
                {
                    OnUploadComplete?.Invoke(_publicUrl);
                    Debug.Log($"[S3Uploader] Upload completed successfully. Public URL: {_publicUrl}");
                }
            }
        }
    }
}
