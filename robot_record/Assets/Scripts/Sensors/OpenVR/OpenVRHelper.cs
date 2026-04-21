// Assets/Scripts/Sensors/OpenVR/OpenVRHelper.cs
using UnityEngine;
using Valve.VR;

namespace RobotMiddleware.Sensors
{
    /// <summary>
    /// Static helper for direct Valve OpenVR API access.
    /// Handles init/shutdown, SteamVR→Unity coordinate conversion, and tracker discovery.
    /// </summary>
    public static class OpenVRHelper
    {
        private static bool _initialized;
        private static int _refCount;

        public static bool Init()
        {
            if (_initialized)
            {
                _refCount++;
                return true;
            }
            if (!OpenVR.IsRuntimeInstalled())
            {
                Debug.LogWarning("[OpenVRHelper] SteamVR runtime not installed");
                return false;
            }
            var error = EVRInitError.None;
            // VRApplication_Other gives full device access without requiring an HMD.
            // VRApplication_Background is too restrictive — devices show as disconnected.
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);
            if (error != EVRInitError.None)
            {
                Debug.LogError($"[OpenVRHelper] OpenVR.Init failed: {error}");
                return false;
            }
            _initialized = true;
            _refCount = 1;
            Debug.Log("[OpenVRHelper] OpenVR initialized (Background mode)");
            return true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            _refCount--;
            if (_refCount > 0) return;
            OpenVR.Shutdown();
            _initialized = false;
            _refCount = 0;
            Debug.Log("[OpenVRHelper] OpenVR shut down (last ref released)");
        }

        public static bool IsInitialized => _initialized;

        // SteamVR: right-handed (Y-up, Z-backward)
        // Unity:   left-handed  (Y-up, Z-forward)
        // Conversion: negate Z in position, flip rotation handedness
        public static Vector3 GetPosition(HmdMatrix34_t m)
        {
            return new Vector3(m.m3, m.m7, -m.m11);
        }

        public static Quaternion GetRotation(HmdMatrix34_t m)
        {
            // Build a 4x4 matrix from the 3x4 SteamVR matrix
            // Apply Z-negate to convert right-handed → left-handed
            var matrix = new Matrix4x4();
            matrix.m00 =  m.m0; matrix.m01 =  m.m1; matrix.m02 = -m.m2;  matrix.m03 = m.m3;
            matrix.m10 =  m.m4; matrix.m11 =  m.m5; matrix.m12 = -m.m6;  matrix.m13 = m.m7;
            matrix.m20 = -m.m8; matrix.m21 = -m.m9; matrix.m22 =  m.m10; matrix.m23 = -m.m11;
            matrix.m30 =  0;    matrix.m31 =  0;    matrix.m32 =  0;     matrix.m33 = 1;
            return matrix.rotation;
        }

        /// <summary>
        /// Find the first connected GenericTracker device. Optionally filter by serial substring.
        /// Returns <c>OpenVR.k_unTrackedDeviceIndexInvalid</c> if none found.
        /// </summary>
        public static uint FindTracker(string serialFilter = "")
        {
            if (!_initialized || OpenVR.System == null) return OpenVR.k_unTrackedDeviceIndexInvalid;

            // Count total GenericTrackers for multi-tracker warning
            int trackerCount = 0;
            for (uint j = 0; j < OpenVR.k_unMaxTrackedDeviceCount; j++)
                if (OpenVR.System.GetTrackedDeviceClass(j) == ETrackedDeviceClass.GenericTracker)
                    trackerCount++;

            if (trackerCount > 1 && string.IsNullOrEmpty(serialFilter))
                Debug.LogWarning($"[OpenVRHelper] {trackerCount} GenericTrackers detected but no serial filter set. " +
                                 "Binding to the first one found. Set _trackerSerial on ViveTrackerManager " +
                                 "to pin to a specific device.");

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker)
                    continue;

                if (string.IsNullOrEmpty(serialFilter))
                {
                    Debug.Log($"[OpenVRHelper] Found tracker at index {i}: {GetDeviceSerial(i)}");
                    return i;
                }

                string serial = GetDeviceSerial(i);
                if (serial.Contains(serialFilter))
                {
                    Debug.Log($"[OpenVRHelper] Found tracker at index {i} matching '{serialFilter}': {serial}");
                    return i;
                }
            }
            return OpenVR.k_unTrackedDeviceIndexInvalid;
        }

        public static string GetDeviceSerial(uint deviceIndex)
        {
            if (!_initialized || OpenVR.System == null) return "";
            var error = ETrackedPropertyError.TrackedProp_Success;
            var sb = new System.Text.StringBuilder(64);
            OpenVR.System.GetStringTrackedDeviceProperty(
                deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, 64, ref error);
            return sb.ToString();
        }
    }
}
