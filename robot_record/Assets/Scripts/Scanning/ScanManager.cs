using System;
using System.Collections.Generic;
using UnityEngine;
using RobotMiddleware.Models;
using RobotMiddleware.Sensors;
using RobotMiddleware.Recording;
using RobotMiddleware.Upload;

namespace RobotMiddleware.Scanning
{
    public enum ScanState
    {
        Idle,
        BackgroundCapture,
        Ready,
        Scanning,
        Preview,
        Confirmed
    }

    public class ScanManager : MonoBehaviour
    {
        public delegate void OnScanStateChangedDelegate(ScanState newState);
        public delegate void OnScanCompleteDelegate(byte[] plyData, int pointCount);

        public event OnScanStateChangedDelegate OnScanStateChanged;
        public event OnScanCompleteDelegate OnScanComplete;

        [SerializeField] private float _depthThresholdMm = 20f;

        public ScanState CurrentState { get; private set; } = ScanState.Idle;
        public int PointCount { get; private set; }

        private RealSenseManager _realSense;
        private RecordingManager _recordingManager;
        private S3Uploader _s3Uploader;

        private ushort[] _backgroundDepth;
        private ushort[] _currentDepth;
        private bool[] _objectMask;
        private int _pixelCount;

        // Cached results from last scan
        private List<Vector3> _scannedPoints;
        private List<Color32> _scannedColors;
        private byte[] _plyData;

        private void Awake()
        {
            _realSense = FindAnyObjectByType<RealSenseManager>();
            _recordingManager = FindAnyObjectByType<RecordingManager>();
            _s3Uploader = FindAnyObjectByType<S3Uploader>();
        }

        private void Start()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged += OnRecordingStateChanged;
        }

        private void OnDestroy()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged -= OnRecordingStateChanged;
        }

        private void OnRecordingStateChanged(RecordingState state)
        {
            if (state == RecordingState.Scanning && CurrentState == ScanState.Idle)
            {
                Debug.Log("[ScanManager] RecordingManager entered SCANNING, activating scan workflow");
                SetState(ScanState.Idle);
            }
        }

        private void Update()
        {
            if (CurrentState != ScanState.Scanning)
                return;

            if (_realSense == null || !_realSense.IsStreaming)
                return;

            // Each frame during scanning: grab depth, compute background subtraction
            if (!_realSense.CopyDepthData(_currentDepth))
                return;

            int objectPixels = 0;
            float thresholdRaw = _depthThresholdMm / 1000f / Mathf.Max(_realSense.DepthScale, 1e-9f);
            ushort thresholdU = (ushort)Mathf.Clamp(thresholdRaw, 1f, 65535f);

            for (int i = 0; i < _pixelCount; i++)
            {
                ushort bg = _backgroundDepth[i];
                ushort cur = _currentDepth[i];

                // Discard no-data pixels
                if (cur == 0 || bg == 0)
                {
                    _objectMask[i] = false;
                    continue;
                }

                int diff = Math.Abs(cur - bg);
                _objectMask[i] = diff > thresholdU;

                if (_objectMask[i])
                    objectPixels++;
            }

            PointCount = objectPixels;
        }

        /// <summary>
        /// Captures the current depth frame as the background reference.
        /// Call this with only the background visible (no object on the table).
        /// </summary>
        public void CaptureBackground()
        {
            if (_realSense == null || !_realSense.IsStreaming)
            {
                Debug.LogWarning("[ScanManager] RealSense not streaming, cannot capture background");
                return;
            }

            int w = _realSense.StreamWidth;
            int h = _realSense.StreamHeight;
            _pixelCount = w * h;

            _backgroundDepth = new ushort[_pixelCount];
            _currentDepth = new ushort[_pixelCount];
            _objectMask = new bool[_pixelCount];

            if (!_realSense.CopyDepthData(_backgroundDepth))
            {
                Debug.LogWarning("[ScanManager] Failed to copy background depth data");
                return;
            }

            SetState(ScanState.BackgroundCapture);
            Debug.Log($"[ScanManager] Background captured ({w}x{h}, {_pixelCount} pixels)");

            // Immediately transition to Ready
            SetState(ScanState.Ready);
        }

        /// <summary>
        /// Begins continuous background subtraction each frame.
        /// </summary>
        public void StartScan()
        {
            if (CurrentState != ScanState.Ready)
            {
                Debug.LogWarning($"[ScanManager] Cannot start scan from state {CurrentState}");
                return;
            }

            PointCount = 0;
            SetState(ScanState.Scanning);
            Debug.Log("[ScanManager] Scanning started");
        }

        /// <summary>
        /// Stops scanning and generates the masked point cloud + PLY from the last frame.
        /// </summary>
        public void StopScan()
        {
            if (CurrentState != ScanState.Scanning)
            {
                Debug.LogWarning($"[ScanManager] Cannot stop scan from state {CurrentState}");
                return;
            }

            // Generate point cloud from the current mask
            var intrinsics = _realSense.GetDepthIntrinsics();
            float depthScale = _realSense.DepthScale;

            // Use stub intrinsics if real ones are zeroed (stub mode)
            if (intrinsics.width == 0)
            {
                intrinsics = new DepthIntrinsics
                {
                    fx = 386.0f, fy = 386.0f,
                    cx = 320.0f, cy = 240.0f,
                    width = _realSense.StreamWidth,
                    height = _realSense.StreamHeight
                };
                depthScale = 0.001f;
            }

            _scannedPoints = PointCloudExporter.DepthToPoints(_currentDepth, _objectMask, intrinsics, depthScale);
            _scannedColors = PointCloudExporter.GetMaskedColors(
                _realSense.ColorTexture, _objectMask, intrinsics.width, intrinsics.height);

            _plyData = PointCloudExporter.ExportPLY(_scannedPoints, _scannedColors);
            PointCount = _scannedPoints.Count;

            SetState(ScanState.Preview);
            Debug.Log($"[ScanManager] Scan stopped. {PointCount} points, PLY size: {_plyData.Length} bytes");

            OnScanComplete?.Invoke(_plyData, PointCount);
        }

        /// <summary>
        /// Confirms the scan, uploads PLY to S3, and transitions RecordingManager to Aligning.
        /// </summary>
        public void ConfirmScan()
        {
            if (CurrentState != ScanState.Preview)
            {
                Debug.LogWarning($"[ScanManager] Cannot confirm from state {CurrentState}");
                return;
            }

            if (_plyData == null || _plyData.Length == 0)
            {
                Debug.LogWarning("[ScanManager] No PLY data to upload");
                return;
            }

            SetState(ScanState.Confirmed);

            // Upload to S3
            if (_s3Uploader != null && _recordingManager != null
                && !string.IsNullOrEmpty(_recordingManager.RecordId))
            {
                _s3Uploader.UploadData(_plyData, _recordingManager.RecordId, "ply");
                Debug.Log($"[ScanManager] PLY upload started for record {_recordingManager.RecordId}");
            }
            else
            {
                Debug.LogWarning("[ScanManager] S3Uploader or RecordId not available, skipping upload");
            }

            // Transition RecordingManager to Aligning
            if (_recordingManager != null)
            {
                _recordingManager.AlignSensors();
                Debug.Log("[ScanManager] Triggered RecordingManager -> Aligning");
            }
        }

        /// <summary>
        /// Resets to Ready state for another scan attempt. Background is preserved.
        /// </summary>
        public void Rescan()
        {
            if (CurrentState != ScanState.Preview)
            {
                Debug.LogWarning($"[ScanManager] Cannot rescan from state {CurrentState}");
                return;
            }

            _scannedPoints = null;
            _scannedColors = null;
            _plyData = null;
            PointCount = 0;

            SetState(ScanState.Ready);
            Debug.Log("[ScanManager] Reset to Ready for rescan");
        }

        /// <summary>
        /// Full reset back to Idle.
        /// </summary>
        public void ResetScan()
        {
            _backgroundDepth = null;
            _currentDepth = null;
            _objectMask = null;
            _scannedPoints = null;
            _scannedColors = null;
            _plyData = null;
            PointCount = 0;

            SetState(ScanState.Idle);
        }

        private void SetState(ScanState newState)
        {
            CurrentState = newState;
            OnScanStateChanged?.Invoke(newState);
        }
    }
}
