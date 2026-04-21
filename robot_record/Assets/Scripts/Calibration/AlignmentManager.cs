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
        // ----- Serializable DTOs for backend API -----

        [Serializable]
        private class LocateToolRequest
        {
            public string rgbImage;
        }

        [Serializable]
        private class LocateToolResponse
        {
            public float x;
            public float y;
            public float confidence;
        }

        [Serializable]
        private class Vec3Array
        {
            public float[] src;
            public float[] dst;
        }

        [Serializable]
        private class ComputeTransformRequest
        {
            public Vec3Array[] pointPairs;
            public string recordId;
        }

        [Serializable]
        private class ComputeTransformResponse
        {
            public float[] transformMatrix;
            public float error;
        }

        // ----- Events -----

        public delegate void CalibrationCompleteDelegate(Matrix4x4 transform, float error);
        public delegate void CalibrationFailedDelegate(string error);
        public delegate void CalibrationProgressDelegate(int capturedPoints, int totalPoints, string status);

        public event CalibrationCompleteDelegate OnCalibrationComplete;
        public event CalibrationFailedDelegate OnCalibrationFailed;
        public event CalibrationProgressDelegate OnCalibrationProgress;

        // ----- Inspector fields -----

        [Header("Sensors")]
        [SerializeField] private RealSenseManager _realSenseManager;
        [SerializeField] private ViveTrackerManager _viveTrackerManager;

        [Header("Calibration Settings")]
        [SerializeField] private int _requiredPoints = 4;
        [SerializeField] private float _requestTimeoutSeconds = 65f;

        [Header("RealSense Depth Intrinsics")]
        [SerializeField] private float _fx = 384.0f;
        [SerializeField] private float _fy = 384.0f;
        [SerializeField] private float _cx = 320.0f;
        [SerializeField] private float _cy = 240.0f;
        [SerializeField] private float _depthScale = 0.001f; // R16 raw value → meters

        // ----- Public state -----

        public bool IsCalibrating { get; private set; }
        public int CapturedPointCount => _capturedPairs.Count;
        public int RequiredPoints => _requiredPoints;
        public float LastError { get; private set; }
        public Matrix4x4 LastTransform { get; private set; } = Matrix4x4.identity;

        // ----- Private state -----

        private BackendConfig _config;
        private readonly List<Vec3Array> _capturedPairs = new List<Vec3Array>();
        private string _activeRecordId;
        private bool _captureBusy; // prevent overlapping capture coroutines

        private void Awake()
        {
            _config = BackendConfig.Instance;
        }

        // =====================================================================
        // Public API
        // =====================================================================

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

            _capturedPairs.Clear();
            _activeRecordId = recordId;
            _captureBusy = false;
            IsCalibrating = true;

            if (!_realSenseManager.IsStreaming)
            {
                _realSenseManager.StartStreaming();
            }

            Debug.Log($"[AlignmentManager] Calibration started, need {_requiredPoints} points");
            OnCalibrationProgress?.Invoke(0, _requiredPoints, "Place tool on landmark and press Capture");
        }

        /// <summary>
        /// Capture one calibration point. This:
        /// 1. Reads Vive pose + RealSense RGB frame
        /// 2. Sends RGB to backend /locate-tool → pixel (u,v)
        /// 3. Looks up depth at (u,v) from DepthTexture
        /// 4. Computes 3D camera-space point from depth + intrinsics
        /// 5. Stores the {cameraPoint, vivePoint} pair
        /// When enough points are collected, automatically computes the transform.
        /// </summary>
        public void CapturePoint()
        {
            if (!IsCalibrating)
            {
                Debug.LogWarning("[AlignmentManager] Not calibrating");
                return;
            }

            if (_captureBusy)
            {
                Debug.LogWarning("[AlignmentManager] Previous capture still in progress");
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

            if (_realSenseManager.IsStub)
            {
                OnCalibrationFailed?.Invoke("RealSense is in stub mode — real depth data required for calibration");
                return;
            }

            StartCoroutine(CapturePointCoroutine());
        }

        public void CancelCalibration()
        {
            if (!IsCalibrating) return;

            IsCalibrating = false;
            _capturedPairs.Clear();
            _captureBusy = false;
            Debug.Log("[AlignmentManager] Calibration cancelled");
        }

        // =====================================================================
        // Step 1: Capture a single point (VLM locate → depth lookup → store)
        // =====================================================================

        private IEnumerator CapturePointCoroutine()
        {
            _captureBusy = true;

            // Snapshot Vive pose at capture time
            Vector3 vivePos = _viveTrackerManager.Position;

            // Snapshot RealSense frames
            string rgbBase64 = Convert.ToBase64String(_realSenseManager.ColorTexture.EncodeToPNG());
            Texture2D depthSnapshot = _realSenseManager.DepthTexture;

            OnCalibrationProgress?.Invoke(_capturedPairs.Count, _requiredPoints, "Locating tool in image...");

            // --- Call backend /locate-tool ---
            var locateReq = new LocateToolRequest { rgbImage = rgbBase64 };
            string locateJson = JsonUtility.ToJson(locateReq);
            byte[] locateBody = System.Text.Encoding.UTF8.GetBytes(locateJson);
            string locateUrl = $"{_config.BaseApiUrl}/alignment/locate-tool";

            using (UnityWebRequest www = new UnityWebRequest(locateUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(locateBody);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)_requestTimeoutSeconds;

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string error = $"locate-tool request failed: {www.error}";
                    Debug.LogError($"[AlignmentManager] {error}");
                    OnCalibrationFailed?.Invoke(error);
                    _captureBusy = false;
                    yield break;
                }

                LocateToolResponse locateResp;
                try
                {
                    locateResp = JsonUtility.FromJson<LocateToolResponse>(www.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    OnCalibrationFailed?.Invoke($"Failed to parse locate-tool response: {ex.Message}");
                    _captureBusy = false;
                    yield break;
                }

                Debug.Log($"[AlignmentManager] Tool located at pixel ({locateResp.x}, {locateResp.y}) confidence={locateResp.confidence:F2}");

                if (locateResp.confidence < 0.3f)
                {
                    Debug.LogWarning("[AlignmentManager] Low confidence detection, point may be inaccurate");
                }

                // --- Look up depth at detected pixel ---
                int u = Mathf.RoundToInt(locateResp.x);
                int v = Mathf.RoundToInt(locateResp.y);

                // Clamp to texture bounds
                u = Mathf.Clamp(u, 0, depthSnapshot.width - 1);
                v = Mathf.Clamp(v, 0, depthSnapshot.height - 1);

                float depthMeters = ReadDepthAtPixel(depthSnapshot, u, v);

                if (depthMeters <= 0.01f || depthMeters > 10f)
                {
                    OnCalibrationFailed?.Invoke($"Invalid depth at pixel ({u},{v}): {depthMeters}m");
                    _captureBusy = false;
                    yield break;
                }

                // --- Compute 3D point in camera space using pinhole model ---
                float X = (u - _cx) * depthMeters / _fx;
                float Y = (v - _cy) * depthMeters / _fy;
                float Z = depthMeters;

                Debug.Log($"[AlignmentManager] Camera-space point: ({X:F4}, {Y:F4}, {Z:F4}), depth={depthMeters:F4}m");

                // Store pair: src = camera space, dst = Vive space
                _capturedPairs.Add(new Vec3Array
                {
                    src = new float[] { X, Y, Z },
                    dst = new float[] { vivePos.x, vivePos.y, vivePos.z }
                });

                int count = _capturedPairs.Count;
                Debug.Log($"[AlignmentManager] Captured point {count}/{_requiredPoints}");
                OnCalibrationProgress?.Invoke(count, _requiredPoints, "Point captured. Place tool on next landmark.");

                _captureBusy = false;

                if (count >= _requiredPoints)
                {
                    StartCoroutine(ComputeTransformCoroutine());
                }
            }
        }

        // =====================================================================
        // Step 2: Compute transform from collected point pairs
        // =====================================================================

        private IEnumerator ComputeTransformCoroutine()
        {
            OnCalibrationProgress?.Invoke(_capturedPairs.Count, _requiredPoints, "Computing transform...");
            Debug.Log("[AlignmentManager] Submitting point pairs for SVD transform computation...");

            var request = new ComputeTransformRequest
            {
                pointPairs = _capturedPairs.ToArray(),
                recordId = _activeRecordId
            };

            string jsonBody = JsonUtility.ToJson(request);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            string url = $"{_config.BaseApiUrl}/alignment/compute-transform";

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
                    string error = $"compute-transform request failed: {www.error}";
                    Debug.LogError($"[AlignmentManager] {error}");
                    OnCalibrationFailed?.Invoke(error);
                    yield break;
                }

                string responseText = www.downloadHandler.text;
                Debug.Log($"[AlignmentManager] Transform response: {responseText}");

                ComputeTransformResponse response;
                try
                {
                    response = JsonUtility.FromJson<ComputeTransformResponse>(responseText);
                }
                catch (Exception ex)
                {
                    OnCalibrationFailed?.Invoke($"Failed to parse compute-transform response: {ex.Message}");
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
                LastError = response.error;

                Debug.Log($"[AlignmentManager] Alignment complete! Mean error: {response.error:F6}m");
                OnCalibrationComplete?.Invoke(matrix, response.error);
            }
        }

        // =====================================================================
        // Depth texture reading
        // =====================================================================

        /// <summary>
        /// Read depth value in meters from an R16 depth texture at the given pixel.
        /// R16 stores a 16-bit unsigned integer; multiply by _depthScale to get meters.
        /// </summary>
        private float ReadDepthAtPixel(Texture2D depthTex, int u, int v)
        {
            // Unity Texture2D (0,0) is bottom-left; RealSense (0,0) is top-left.
            // Flip v to match RealSense convention.
            int flippedV = depthTex.height - 1 - v;

            Color pixel = depthTex.GetPixel(u, flippedV);

            // For R16 format, the red channel holds the normalized value (0-1 maps to 0-65535).
            // Convert back to raw 16-bit and then to meters.
            float raw16 = pixel.r * 65535f;
            return raw16 * _depthScale;
        }
    }
}
