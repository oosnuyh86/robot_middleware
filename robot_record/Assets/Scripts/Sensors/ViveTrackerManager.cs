using System;
using UnityEngine;

// SDK Integration Notes:
// ----------------------
// To wire real Vive Tracker 6DoF data via OpenXR:
//
// Option A - Unity Input System (recommended, requires com.unity.inputsystem):
//   1. Add a TrackedPoseDriver component to the GameObject that should follow the tracker.
//   2. Set its Position Input and Rotation Input to the appropriate XR bindings,
//      e.g., <XRController>{RightHand}/devicePosition for a hand-role tracker,
//      or use a custom InputActionAsset with XR bindings for generic trackers.
//   3. Read transform.position / transform.rotation from that GameObject.
//
// Option B - Legacy XR Input API:
//   1. Use UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(...)
//      with InputDeviceCharacteristics.TrackerReference to find Vive Trackers.
//   2. Call device.TryGetFeatureValue(CommonUsages.devicePosition, out pos)
//      and device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot).
//
// OpenXR package: com.unity.xr.openxr (install via Package Manager)
// SteamVR plugin: com.valvesoftware.unity.openvr (optional, for SteamVR-specific features)

namespace RobotMiddleware.Sensors
{
    public class ViveTrackerManager : MonoBehaviour
    {
        public delegate void OnPoseUpdatedDelegate(Vector3 position, Quaternion rotation);
        public event OnPoseUpdatedDelegate OnPoseUpdated;

        [SerializeField] private int _trackerIndex;

        [Header("Stub Simulation Settings")]
        [SerializeField] private float _orbitRadius = 0.3f;
        [SerializeField] private float _orbitSpeed = 0.5f;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public bool IsTracking { get; private set; }

        private void OnEnable()
        {
            IsTracking = true;
            Debug.Log($"[ViveTrackerManager] Tracking enabled (stub, tracker index {_trackerIndex})");
        }

        private void OnDisable()
        {
            IsTracking = false;
            Debug.Log("[ViveTrackerManager] Tracking disabled");
        }

        private void Update()
        {
            if (!IsTracking)
                return;

            // Stub: simulate a slow orbit to test consumers of pose data
            float t = Time.time * _orbitSpeed;
            Position = new Vector3(
                Mathf.Cos(t) * _orbitRadius,
                1.0f + Mathf.Sin(t * 0.7f) * 0.05f,
                Mathf.Sin(t) * _orbitRadius
            );

            Rotation = Quaternion.Euler(
                Mathf.Sin(t * 0.3f) * 5f,
                t * Mathf.Rad2Deg * 0.1f,
                Mathf.Cos(t * 0.5f) * 3f
            );

            OnPoseUpdated?.Invoke(Position, Rotation);
        }
    }
}
