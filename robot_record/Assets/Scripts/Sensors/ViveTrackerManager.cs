// Assets/Scripts/Sensors/ViveTrackerManager.cs
using System;
using UnityEngine;
using Valve.VR;

namespace RobotMiddleware.Sensors
{
    /// <summary>
    /// Reads 6DoF pose from a Vive Tracker via direct Valve OpenVR API.
    ///
    /// Architecture: uses <see cref="OpenVRHelper"/> to initialise OpenVR in
    /// Background mode, discover GenericTracker devices, and poll
    /// <c>GetDeviceToAbsoluteTrackingPose</c> each frame. All coordinate
    /// conversion (SteamVR right-handed → Unity left-handed) is handled by
    /// <see cref="OpenVRHelper.GetPosition"/> / <see cref="OpenVRHelper.GetRotation"/>.
    ///
    /// Tracker discovery: on <see cref="OnEnable"/> and periodically (every
    /// <see cref="ScanInterval"/> seconds) the manager scans for a connected
    /// GenericTracker. An optional <see cref="_trackerSerial"/> filter narrows
    /// the match to a specific device.
    ///
    /// Fallback behaviour is controlled by <see cref="_requireHardware"/>:
    ///   - <c>false</c> (default, production): falls back to a stub orbit after
    ///     <see cref="FallbackDelaySeconds"/> of no real pose. Preserves hot-plug
    ///     resilience in live scenes.
    ///   - <c>true</c> (smoke-test): NO fallback. On failure, logs a loud error,
    ///     <c>IsTracking = false</c>, <c>IsRealHardware = false</c>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ViveTrackerManager : MonoBehaviour
    {
        public delegate void OnPoseUpdatedDelegate(Vector3 position, Quaternion rotation);
        public event OnPoseUpdatedDelegate OnPoseUpdated;

        // ----- Configuration -----

        [Header("Tracker Selection")]
        [Tooltip("Optional serial number substring to match a specific tracker. " +
                 "Leave empty to use the first GenericTracker found.")]
        [SerializeField] private string _trackerSerial = "";

        [Header("Hardware Gate")]
        [Tooltip("Smoke-test mode. If true, the manager will NOT fall back to stub when " +
                 "hardware is unavailable — it logs a loud error and keeps IsTracking=false / " +
                 "IsRealHardware=false.")]
        [SerializeField] private bool _requireHardware = false;

        [Header("Fallback")]
        [Tooltip("If no real pose arrives for this many seconds AND _requireHardware is false, " +
                 "fall back to stub orbit.")]
        [SerializeField] private float FallbackDelaySeconds = 3.0f;

        [Header("Stub Simulation")]
        [SerializeField] private float _orbitRadius = 0.3f;
        [SerializeField] private float _orbitSpeed  = 0.5f;

        // ----- Public read-only state -----

        public Vector3    Position          { get; private set; }
        public Quaternion Rotation          { get; private set; } = Quaternion.identity;
        public bool       IsTracking        { get; private set; }
        public bool       IsRealHardware    { get; private set; }
        public int        TrackingStateBits { get; private set; }

        // ----- Private state -----

        private uint _trackerIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
        private TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private float _lastScanTime;
        private float _lastTrackedTime;
        private bool  _hardwareFailureLogged;
        private bool  _everReadHardwarePose;
        private bool  _poseDebugLogged;

        private const float ScanInterval = 2f;

        private void OnEnable()
        {
            if (!OpenVRHelper.Init())
            {
                ReportBindingFailure("OpenVR failed to initialise — is SteamVR running?");
                return;
            }

            _trackerIndex          = OpenVR.k_unTrackedDeviceIndexInvalid;
            _lastScanTime          = 0f;
            _lastTrackedTime       = Time.unscaledTime;
            _hardwareFailureLogged = false;
            _everReadHardwarePose  = false;

            ScanForTracker();
            Debug.Log($"[ViveTrackerManager] Enabled — tracker index: {_trackerIndex}, serial filter: '{_trackerSerial}'");
        }

        private void OnDisable()
        {
            _trackerIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
            IsTracking     = false;
            IsRealHardware = false;
            // Note: we do NOT call OpenVRHelper.Shutdown() here because other
            // components may still need the OpenVR session.
            Debug.Log("[ViveTrackerManager] Tracking disabled");
        }

        private void OnApplicationQuit()
        {
            OpenVRHelper.Shutdown();
        }

        private void ReportBindingFailure(string reason)
        {
            if (_requireHardware)
            {
                IsRealHardware = false;
                IsTracking     = false;
                if (!_hardwareFailureLogged)
                {
                    Debug.LogError($"[ViveTrackerManager] HARDWARE-REQUIRED MODE: {reason}. " +
                                   "Stub fallback is disabled. Fix the hardware or clear " +
                                   "_requireHardware. IsTracking=false.");
                    _hardwareFailureLogged = true;
                }
            }
            else
            {
                Debug.LogWarning($"[ViveTrackerManager] Entering stub orbit mode ({reason}).");
            }
        }

        private void ScanForTracker()
        {
            if (!OpenVRHelper.IsInitialized) return;
            _trackerIndex = OpenVRHelper.FindTracker(_trackerSerial);
            _lastScanTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (!OpenVRHelper.IsInitialized)
            {
                FallbackOrStub("OpenVR not initialised");
                return;
            }

            // Periodic re-scan if we lost the tracker or never found one
            if (_trackerIndex == OpenVR.k_unTrackedDeviceIndexInvalid)
            {
                if (Time.unscaledTime - _lastScanTime > ScanInterval)
                    ScanForTracker();
            }

            // Try to read hardware pose
            if (_trackerIndex != OpenVR.k_unTrackedDeviceIndexInvalid && TryReadPose(out var pos, out var rot))
            {
                Position          = pos;
                Rotation          = rot;
                TrackingStateBits = 3; // position + rotation
                IsTracking        = true;
                IsRealHardware    = true;
                _lastTrackedTime  = Time.unscaledTime;
                _everReadHardwarePose = true;
                DispatchPose(pos, rot);
                return;
            }

            // No fresh real pose this frame
            FallbackOrStub("no tracker pose");
        }

        private void FallbackOrStub(string reason)
        {
            float idle = Time.unscaledTime - _lastTrackedTime;

            // Grace window: if we have NEVER read a hardware pose, wait before entering fallback.
            if (!_everReadHardwarePose && idle < FallbackDelaySeconds)
            {
                IsRealHardware = false;
                IsTracking     = false;
                return;
            }

            // Brief dropout — hold last pose
            if (_everReadHardwarePose && idle < FallbackDelaySeconds)
            {
                IsRealHardware = true;
                IsTracking     = false;
                return;
            }

            // Long dropout
            if (_requireHardware)
            {
                if (!_hardwareFailureLogged)
                {
                    Debug.LogError($"[ViveTrackerManager] HARDWARE-REQUIRED MODE: {reason} " +
                                   $"for {idle:F1}s. Stub disabled. IsTracking=false.");
                    _hardwareFailureLogged = true;
                }
                IsRealHardware = false;
                IsTracking     = false;
                return;
            }

            // Production: stub orbit
            IsRealHardware = false;
            IsTracking     = true;
            UpdateStub();
        }

        /// <summary>
        /// Poll OpenVR for the tracker pose. Returns true if the pose is valid and tracking.
        /// </summary>
        private bool TryReadPose(out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;

            if (OpenVR.System == null) return false;

            OpenVR.System.GetDeviceToAbsoluteTrackingPose(
                ETrackingUniverseOrigin.TrackingUniverseStanding,
                0f, _poses);

            var pose = _poses[_trackerIndex];

            if (!pose.bPoseIsValid || !pose.bDeviceIsConnected)
            {
                if (!_poseDebugLogged)
                {
                    Debug.LogWarning($"[ViveTrackerManager] Pose invalid at index {_trackerIndex}: " +
                                     $"bPoseIsValid={pose.bPoseIsValid}, bDeviceIsConnected={pose.bDeviceIsConnected}, " +
                                     $"eTrackingResult={pose.eTrackingResult}");
                    _poseDebugLogged = true;
                }
                if (!pose.bDeviceIsConnected)
                    _trackerIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
                return false;
            }

            if (pose.eTrackingResult != ETrackingResult.Running_OK &&
                pose.eTrackingResult != ETrackingResult.Running_OutOfRange)
            {
                return false;
            }

            pos = OpenVRHelper.GetPosition(pose.mDeviceToAbsoluteTracking);
            rot = OpenVRHelper.GetRotation(pose.mDeviceToAbsoluteTracking);

            // Numeric validity guard
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
                float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
                return false;

            if (float.IsNaN(rot.x) || float.IsNaN(rot.y) || float.IsNaN(rot.z) || float.IsNaN(rot.w) ||
                float.IsInfinity(rot.x) || float.IsInfinity(rot.y) || float.IsInfinity(rot.z) || float.IsInfinity(rot.w))
                return false;

            // All-zero quaternion is malformed
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                return false;

            return true;
        }

        private void DispatchPose(Vector3 pos, Quaternion rot)
        {
            // Subscriber containment — a throwing handler must not kill the loop.
            try
            {
                OnPoseUpdated?.Invoke(pos, rot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViveTrackerManager] OnPoseUpdated subscriber threw: {ex}");
            }
        }

        private void UpdateStub()
        {
            float t = Time.time * _orbitSpeed;
            Position = new Vector3(
                Mathf.Cos(t) * _orbitRadius,
                1.0f + Mathf.Sin(t * 0.7f) * 0.05f,
                Mathf.Sin(t) * _orbitRadius);

            Rotation = Quaternion.Euler(
                Mathf.Sin(t * 0.3f) * 5f,
                t * Mathf.Rad2Deg * 0.1f,
                Mathf.Cos(t * 0.5f) * 3f);

            DispatchPose(Position, Rotation);
        }
    }
}
