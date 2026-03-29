using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using RobotMiddleware.Config;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.Calibration
{
    public class AlignmentManager : MonoBehaviour
    {
        [Serializable]
        private class VivePose
        {
            public Vector3Serializable position;
            public QuaternionSerializable rotation;
        }

        [Serializable]
        private class Vector3Serializable
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private class QuaternionSerializable
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        private class RealSensePoint
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private class CalibrationPointData
        {
            public VivePose vivePose;
            public RealSensePoint realSensePoint;
        }

        [Serializable]
        private class AlignmentRequestBody
        {
            public string rgbImage;
            public string depthImage;
            public CalibrationPointData[] calibrationPoints;
            public string recordId;
        }

        [Serializable]
        private class AlignmentResponse
        {
            public float[] transformMatrix;
            public float confidence;
            public string description;
        }

        public delegate void CalibrationCompleteDelegate(Matrix4x4 transform, float confidence);
        public delegate void CalibrationFailedDelegate(string error);
        public delegate void CalibrationProgressDelegate(int capturedPoints, int totalPoints);

        public event CalibrationCompleteDelegate OnCalibrationComplete;
        public event CalibrationFailedDelegate OnCalibrationFailed;
        public event CalibrationProgressDelegate OnCalibrationProgress;

        [SerializeField] private RealSenseManager _realSenseManager;
        [SerializeField] private ViveTrackerManager _viveTrackerManager;
        [SerializeField] private int _requiredPoints = 4;
        [SerializeField] private float _requestTimeoutSeconds = 65f;

        public bool IsCalibrating { get; private set; }
        public int CapturedPointCount => _capturedPoints.Count;
        public int RequiredPoints => _requiredPoints;
        public float LastConfidence { get; private set; }
        public Matrix4x4 LastTransform { get; private set; } = Matrix4x4.identity;

        private BackendConfig _config;
        private readonly List<CalibrationPointData> _capturedPoints = new List<CalibrationPointData>();
        private string _lastRgbBase64;
        private string _lastDepthBase64;
        private string _activeRecordId;

        private void Awake()
        {
            _config = BackendConfig.Instance;
        }

        public void StartCalibration(string recordId = null)
        {
            if (IsCalibrating)
            {
                Debug.LogWarning("[AlignmentManager] Calibration already in progress");
                return;
            }

            if (_realSenseManager == null || _viveTrackerManager == null)
            {
                OnCalibrationFailed?.Invoke("RealSenseManager or ViveTrackerManager not assigned");
                return;
            }

            _capturedPoints.Clear();
            _lastRgbBase64 = null;
            _lastDepthBase64 = null;
            _activeRecordId = recordId;
            IsCalibrating = true;

            if (!_realSenseManager.IsStreaming)
            {
                _realSenseManager.StartStreaming();
            }

            Debug.Log($"[AlignmentManager] Calibration started, need {_requiredPoints} points");
            OnCalibrationProgress?.Invoke(0, _requiredPoints);
        }

        public void CapturePoint()
        {
            if (!IsCalibrating)
            {
                Debug.LogWarning("[AlignmentManager] Not calibrating");
                return;
            }

            if (!_viveTrackerManager.IsTracking)
            {
                OnCalibrationFailed?.Invoke("Vive tracker is not tracking");
                return;
            }

            if (_realSenseManager.ColorTexture == null || _realSenseManager.DepthTexture == null)
            {
                OnCalibrationFailed?.Invoke("RealSense frames not available");
                return;
            }

            // Capture Vive pose
            Vector3 pos = _viveTrackerManager.Position;
            Quaternion rot = _viveTrackerManager.Rotation;

            var point = new CalibrationPointData
            {
                vivePose = new VivePose
                {
                    position = new Vector3Serializable { x = pos.x, y = pos.y, z = pos.z },
                    rotation = new QuaternionSerializable { x = rot.x, y = rot.y, z = rot.z, w = rot.w }
                },
                realSensePoint = new RealSensePoint { x = pos.x, y = pos.y, z = pos.z }
            };

            _capturedPoints.Add(point);

            // Capture latest RealSense frames as base64 PNG
            _lastRgbBase64 = Convert.ToBase64String(_realSenseManager.ColorTexture.EncodeToPNG());
            _lastDepthBase64 = Convert.ToBase64String(_realSenseManager.DepthTexture.EncodeToPNG());

            int count = _capturedPoints.Count;
            Debug.Log($"[AlignmentManager] Captured point {count}/{_requiredPoints}");
            OnCalibrationProgress?.Invoke(count, _requiredPoints);

            if (count >= _requiredPoints)
            {
                StartCoroutine(SubmitCalibrationCoroutine());
            }
        }

        public void CancelCalibration()
        {
            if (!IsCalibrating) return;

            IsCalibrating = false;
            _capturedPoints.Clear();
            Debug.Log("[AlignmentManager] Calibration cancelled");
        }

        private IEnumerator SubmitCalibrationCoroutine()
        {
            Debug.Log("[AlignmentManager] Submitting calibration data to backend...");

            var requestBody = new AlignmentRequestBody
            {
                rgbImage = _lastRgbBase64,
                depthImage = _lastDepthBase64,
                calibrationPoints = _capturedPoints.ToArray(),
                recordId = _activeRecordId
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            string url = $"{_config.BaseApiUrl}/alignment/compute";

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)_requestTimeoutSeconds;

                yield return www.SendWebRequest();

                IsCalibrating = false;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string error = $"Alignment request failed: {www.error}";
                    Debug.LogError($"[AlignmentManager] {error}");
                    OnCalibrationFailed?.Invoke(error);
                    yield break;
                }

                string responseText = www.downloadHandler.text;
                Debug.Log($"[AlignmentManager] Response received: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

                AlignmentResponse response;
                try
                {
                    response = JsonUtility.FromJson<AlignmentResponse>(responseText);
                }
                catch (Exception ex)
                {
                    OnCalibrationFailed?.Invoke($"Failed to parse response: {ex.Message}");
                    yield break;
                }

                if (response.transformMatrix == null || response.transformMatrix.Length != 16)
                {
                    OnCalibrationFailed?.Invoke("Invalid transform matrix in response");
                    yield break;
                }

                // Convert row-major float[16] to Unity Matrix4x4 (column-major)
                Matrix4x4 matrix = new Matrix4x4();
                float[] m = response.transformMatrix;
                matrix.SetColumn(0, new Vector4(m[0], m[4], m[8], m[12]));
                matrix.SetColumn(1, new Vector4(m[1], m[5], m[9], m[13]));
                matrix.SetColumn(2, new Vector4(m[2], m[6], m[10], m[14]));
                matrix.SetColumn(3, new Vector4(m[3], m[7], m[11], m[15]));

                LastTransform = matrix;
                LastConfidence = response.confidence;

                Debug.Log($"[AlignmentManager] Alignment complete! Confidence: {response.confidence:P1}");
                OnCalibrationComplete?.Invoke(matrix, response.confidence);
            }
        }
    }
}
