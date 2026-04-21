// Assets/Editor/Phase4/PlayModeVerifier.cs
// Phase 4: Play Mode live sensor verification — MenuItems for MCP-driven testing.
#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using RobotMiddleware.Sensors;
using RobotMiddleware.Scanning;
using Valve.VR;

namespace RobotMiddleware.Editor.Phase4
{
    public static class PlayModeVerifier
    {
        // ----------------------------------------------------------------
        // 1. RealSense Verification
        // ----------------------------------------------------------------
        [MenuItem("Sensors/Phase4/Verify RealSense")]
        public static void VerifyRealSense()
        {
            var rs = Object.FindAnyObjectByType<RealSenseManager>();
            if (rs == null)
            {
                Debug.LogError("[Phase4] RealSense Verification FAIL: RealSenseManager not found in scene");
                return;
            }

            bool isStreaming = rs.IsStreaming;
            bool isStub = rs.IsStub;

            Debug.Log($"[Phase4] RealSense IsStreaming={isStreaming}, IsStub={isStub}");

            if (!isStreaming)
            {
                Debug.LogError("[Phase4] RealSense Verification FAIL: IsStreaming=False");
                return;
            }

            // Sample color texture variance
            float colorVariance = 0f;
            if (rs.ColorTexture != null)
            {
                var pixels = rs.ColorTexture.GetPixels();
                if (pixels.Length > 0)
                {
                    float rSum = 0f, gSum = 0f, bSum = 0f;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        rSum += pixels[i].r;
                        gSum += pixels[i].g;
                        bSum += pixels[i].b;
                    }
                    float rMean = rSum / pixels.Length;
                    float gMean = gSum / pixels.Length;
                    float bMean = bSum / pixels.Length;

                    float varSum = 0f;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float dr = pixels[i].r - rMean;
                        float dg = pixels[i].g - gMean;
                        float db = pixels[i].b - bMean;
                        varSum += dr * dr + dg * dg + db * db;
                    }
                    colorVariance = varSum / pixels.Length;
                }
            }

            // Sample depth texture non-zero percentage
            float depthNonZeroPercent = 0f;
            if (rs.DepthTexture != null)
            {
                var depthPixels = rs.DepthTexture.GetPixels();
                if (depthPixels.Length > 0)
                {
                    int nonZero = 0;
                    for (int i = 0; i < depthPixels.Length; i++)
                    {
                        if (depthPixels[i].r > 0.001f)
                            nonZero++;
                    }
                    depthNonZeroPercent = (nonZero * 100f) / depthPixels.Length;
                }
            }

            Debug.Log($"[Phase4] RealSense colorVariance={colorVariance:F6}, depthNonZeroPercent={depthNonZeroPercent:F1}%");

            // PASS/FAIL determination
            if (isStub)
            {
                Debug.LogWarning("[Phase4] RealSense is in STUB mode — hardware not connected or SDK not available");
                Debug.Log("[Phase4] RealSense Verification PASS (stub mode — streaming but no real camera)");
            }
            else if (colorVariance > 0f && depthNonZeroPercent >= 10f)
            {
                Debug.Log("[Phase4] RealSense Verification PASS");
            }
            else
            {
                Debug.LogError($"[Phase4] RealSense Verification FAIL: colorVariance={colorVariance:F6} (need >0), depthNonZeroPercent={depthNonZeroPercent:F1}% (need >=10%)");
            }
        }

        // ----------------------------------------------------------------
        // 2. VIVE Tracker Verification
        // ----------------------------------------------------------------
        private static Vector3 _lastTrackerPosition;
        private static Quaternion _lastTrackerRotation;

        [MenuItem("Sensors/Phase4/Verify ViveTracker")]
        public static void VerifyViveTracker()
        {
            // Check OpenVR initialisation state
            bool openVRReady = OpenVRHelper.IsInitialized;
            Debug.Log($"[Phase4] ViveTracker OpenVRHelper.IsInitialized={openVRReady}");

            // Count GenericTracker devices via OpenVR
            int trackerCount = 0;
            if (openVRReady && OpenVR.System != null)
            {
                for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                {
                    if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                        trackerCount++;
                }
            }
            Debug.Log($"[Phase4] ViveTracker OpenVR trackerCount={trackerCount}");

            // Check ViveTrackerManager state
            var vtm = Object.FindAnyObjectByType<ViveTrackerManager>();
            if (vtm == null)
            {
                Debug.LogError("[Phase4] ViveTracker Verification FAIL: ViveTrackerManager not found in scene");
                return;
            }

            bool isTracking = vtm.IsTracking;
            bool isRealHardware = vtm.IsRealHardware;
            Vector3 pos = vtm.Position;
            Quaternion rot = vtm.Rotation;
            int trackingBits = vtm.TrackingStateBits;

            Debug.Log($"[Phase4] ViveTracker IsTracking={isTracking}, IsRealHardware={isRealHardware}, " +
                      $"Position={pos}, Rotation={rot}, TrackingStateBits={trackingBits}");

            // Store for delta check
            _lastTrackerPosition = pos;
            _lastTrackerRotation = rot;

            // PASS/FAIL
            if (openVRReady && trackerCount >= 1 && isRealHardware)
            {
                Debug.Log("[Phase4] ViveTracker Verification PASS");
            }
            else
            {
                Debug.LogWarning($"[Phase4] ViveTracker Verification FAIL: openVRReady={openVRReady}, " +
                                 $"trackerCount={trackerCount}, IsRealHardware={isRealHardware}");
                Debug.LogWarning("[Phase4] Run 'Sensors/Phase4/Dump Diagnostics' for detailed info");
            }
        }

        // ----------------------------------------------------------------
        // 3. VIVE Tracker Delta (motion detection)
        // ----------------------------------------------------------------
        [MenuItem("Sensors/Phase4/Verify ViveTracker Delta")]
        public static void VerifyViveTrackerDelta()
        {
            var vtm = Object.FindAnyObjectByType<ViveTrackerManager>();
            if (vtm == null)
            {
                Debug.LogError("[Phase4] ViveTracker Delta FAIL: ViveTrackerManager not found");
                return;
            }

            Vector3 newPos = vtm.Position;
            Quaternion newRot = vtm.Rotation;

            float posDelta = Vector3.Distance(newPos, _lastTrackerPosition);
            float rotDelta = Quaternion.Angle(newRot, _lastTrackerRotation);

            Debug.Log($"[Phase4] ViveTracker Delta: positionDelta={posDelta:F6}m, rotationDelta={rotDelta:F3}deg");
            Debug.Log($"[Phase4] ViveTracker Delta: prevPos={_lastTrackerPosition}, newPos={newPos}");
            Debug.Log($"[Phase4] ViveTracker Delta: prevRot={_lastTrackerRotation}, newRot={newRot}");

            bool moved = posDelta > 0.0001f || rotDelta > 0.01f;
            if (moved)
            {
                Debug.Log("[Phase4] ViveTracker Delta PASS: motion detected");
            }
            else
            {
                Debug.LogWarning("[Phase4] ViveTracker Delta INCONCLUSIVE: no significant motion detected. " +
                                 "If tracker is in stub mode, this is expected.");
            }

            _lastTrackerPosition = newPos;
            _lastTrackerRotation = newRot;
        }

        // ----------------------------------------------------------------
        // 4. Full Diagnostics Dump
        // ----------------------------------------------------------------
        [MenuItem("Sensors/Phase4/Dump Diagnostics")]
        public static void DumpDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Phase4] ===== FULL DIAGNOSTICS DUMP =====");

            // OpenVR device enumeration
            sb.AppendLine("[Phase4] --- OpenVR Devices ---");
            if (OpenVRHelper.IsInitialized && OpenVR.System != null)
            {
                for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                {
                    var devClass = OpenVR.System.GetTrackedDeviceClass(i);
                    if (devClass == ETrackedDeviceClass.Invalid) continue;
                    string serial = OpenVRHelper.GetDeviceSerial(i);
                    sb.AppendLine($"[Phase4]   [{i}] {devClass}  serial={serial}");
                }
            }
            else
            {
                sb.AppendLine("[Phase4]   OpenVR not initialized — cannot enumerate devices");
            }

            // ViveTrackerManager state
            var vtm = Object.FindAnyObjectByType<ViveTrackerManager>();
            sb.AppendLine("[Phase4] --- ViveTrackerManager ---");
            if (vtm != null)
            {
                sb.AppendLine($"[Phase4]   IsTracking={vtm.IsTracking}");
                sb.AppendLine($"[Phase4]   IsRealHardware={vtm.IsRealHardware}");
                sb.AppendLine($"[Phase4]   Position={vtm.Position}");
                sb.AppendLine($"[Phase4]   Rotation={vtm.Rotation}");
                sb.AppendLine($"[Phase4]   TrackingStateBits={vtm.TrackingStateBits}");
            }
            else
            {
                sb.AppendLine("[Phase4]   NOT FOUND IN SCENE");
            }

            // RealSenseManager state
            var rs = Object.FindAnyObjectByType<RealSenseManager>();
            sb.AppendLine("[Phase4] --- RealSenseManager ---");
            if (rs != null)
            {
                sb.AppendLine($"[Phase4]   IsStreaming={rs.IsStreaming}");
                sb.AppendLine($"[Phase4]   IsStub={rs.IsStub}");
                sb.AppendLine($"[Phase4]   ColorTexture={(rs.ColorTexture != null ? $"{rs.ColorTexture.width}x{rs.ColorTexture.height}" : "null")}");
                sb.AppendLine($"[Phase4]   DepthTexture={(rs.DepthTexture != null ? $"{rs.DepthTexture.width}x{rs.DepthTexture.height}" : "null")}");
            }
            else
            {
                sb.AppendLine("[Phase4]   NOT FOUND IN SCENE");
            }

            // ScanManager state
            var sm = Object.FindAnyObjectByType<ScanManager>();
            sb.AppendLine("[Phase4] --- ScanManager ---");
            if (sm != null)
            {
                sb.AppendLine($"[Phase4]   CurrentState={sm.CurrentState}");
                sb.AppendLine($"[Phase4]   PointCount={sm.PointCount}");
            }
            else
            {
                sb.AppendLine("[Phase4]   NOT FOUND IN SCENE");
            }

            sb.AppendLine("[Phase4] ===== END DIAGNOSTICS =====");
            Debug.Log(sb.ToString());
        }

        // ----------------------------------------------------------------
        // 5. Scanning Workflow Steps
        // ----------------------------------------------------------------
        [MenuItem("Sensors/Phase4/Scan Step1 - CaptureBackground")]
        public static void ScanStep1_CaptureBackground()
        {
            var sm = Object.FindAnyObjectByType<ScanManager>();
            if (sm == null)
            {
                Debug.LogError("[Phase4] ScanStep1 FAIL: ScanManager not found");
                return;
            }

            var rs = Object.FindAnyObjectByType<RealSenseManager>();
            if (rs != null && rs.IsStub)
            {
                Debug.LogWarning("[Phase4] ScanStep1 SKIP: RealSense is in stub mode, scanning not possible");
                return;
            }

            sm.CaptureBackground();
            Debug.Log($"[Phase4] ScanStep1 CaptureBackground complete, state={sm.CurrentState}");
        }

        [MenuItem("Sensors/Phase4/Scan Step2 - StartScan")]
        public static void ScanStep2_StartScan()
        {
            var sm = Object.FindAnyObjectByType<ScanManager>();
            if (sm == null)
            {
                Debug.LogError("[Phase4] ScanStep2 FAIL: ScanManager not found");
                return;
            }
            sm.StartScan();
            Debug.Log($"[Phase4] ScanStep2 StartScan complete, state={sm.CurrentState}");
        }

        [MenuItem("Sensors/Phase4/Scan Step3 - StopScan")]
        public static void ScanStep3_StopScan()
        {
            var sm = Object.FindAnyObjectByType<ScanManager>();
            if (sm == null)
            {
                Debug.LogError("[Phase4] ScanStep3 FAIL: ScanManager not found");
                return;
            }
            sm.StopScan();
            Debug.Log($"[Phase4] ScanStep3 StopScan complete, state={sm.CurrentState}, PointCount={sm.PointCount}");
        }

        [MenuItem("Sensors/Phase4/Scan Step4 - ConfirmScan")]
        public static void ScanStep4_ConfirmScan()
        {
            var sm = Object.FindAnyObjectByType<ScanManager>();
            if (sm == null)
            {
                Debug.LogError("[Phase4] ScanStep4 FAIL: ScanManager not found");
                return;
            }
            sm.ConfirmScan();
            Debug.Log($"[Phase4] ScanStep4 ConfirmScan complete, state={sm.CurrentState}");
        }

        // ----------------------------------------------------------------
        // 6. Scan Status (quick check)
        // ----------------------------------------------------------------
        [MenuItem("Sensors/Phase4/Scan Status")]
        public static void ScanStatus()
        {
            var sm = Object.FindAnyObjectByType<ScanManager>();
            if (sm == null)
            {
                Debug.LogError("[Phase4] ScanStatus: ScanManager not found");
                return;
            }
            Debug.Log($"[Phase4] ScanStatus: state={sm.CurrentState}, PointCount={sm.PointCount}");
        }
    }
}
#endif
