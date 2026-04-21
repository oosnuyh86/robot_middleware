// Assets/Editor/Phase2/Phase2Probe.cs
#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Valve.VR;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.Sensors.EditorTools
{
    /// <summary>
    /// Phase 2 editor-side probes for OpenVR / Vive Tracker diagnostics.
    /// All tracker interaction goes through the Valve OpenVR native API.
    /// </summary>
    public static class Phase2Probe
    {
        [MenuItem("Sensors/Phase2/Check OpenVR Installation")]
        public static void CheckOpenVRInstallation()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Phase2Probe] === OpenVR Installation Check ===");

            // 1. Runtime installed?
            bool runtimeInstalled = OpenVR.IsRuntimeInstalled();
            sb.AppendLine($"  OpenVR.IsRuntimeInstalled() = {runtimeInstalled}");

            // 2. Native DLL present?
            string dllPath = Path.Combine(Application.dataPath, "Plugins", "x86_64", "openvr_api.dll");
            bool dllExists = File.Exists(dllPath);
            sb.AppendLine($"  openvr_api.dll exists = {dllExists}  ({dllPath})");

            // 3. SteamVR processes running?
            bool steamVRRunning = false;
            bool vrServerRunning = false;
            try
            {
                var procs = Process.GetProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (name.Contains("steamvr") || name == "vrmonitor")
                            steamVRRunning = true;
                        if (name == "vrserver")
                            vrServerRunning = true;
                    }
                    catch { /* access denied for some system processes */ }
                }
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"  Process scan error: {ex.Message}");
            }
            sb.AppendLine($"  SteamVR process running = {steamVRRunning}");
            sb.AppendLine($"  vrserver process running = {vrServerRunning}");

            // Summary
            bool allGood = runtimeInstalled && dllExists;
            sb.AppendLine(allGood
                ? "[Phase2Probe] OpenVR installation OK"
                : "[Phase2Probe] OpenVR installation INCOMPLETE — see above");

            if (allGood)
                UnityEngine.Debug.Log(sb.ToString());
            else
                UnityEngine.Debug.LogWarning(sb.ToString());
        }

        [MenuItem("Sensors/Phase2/Enumerate OpenVR Trackers")]
        public static void EnumerateOpenVRTrackers()
        {
            bool weInitialized = false;
            try
            {
                if (!OpenVRHelper.IsInitialized)
                {
                    if (!OpenVRHelper.Init())
                    {
                        UnityEngine.Debug.LogError("[Phase2Probe] OpenVRHelper.Init() failed — is SteamVR running?");
                        return;
                    }
                    weInitialized = true;
                }

                var sb = new StringBuilder();
                sb.AppendLine("[Phase2Probe] === OpenVR Device Enumeration ===");

                int trackerCount = 0;
                for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                {
                    var devClass = OpenVR.System.GetTrackedDeviceClass(i);
                    if (devClass == ETrackedDeviceClass.Invalid)
                        continue;

                    string serial = OpenVRHelper.GetDeviceSerial(i);
                    string marker = devClass == ETrackedDeviceClass.GenericTracker ? " <-- TRACKER" : "";
                    sb.AppendLine($"  [{i}] {devClass}  serial={serial}{marker}");

                    if (devClass == ETrackedDeviceClass.GenericTracker)
                        trackerCount++;
                }

                sb.AppendLine($"[Phase2Probe] Total GenericTracker devices: {trackerCount}");
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                // Editor scripts must not hold the OpenVR connection
                if (weInitialized)
                    OpenVRHelper.Shutdown();
            }
        }

        [MenuItem("Sensors/Phase2/Dump OpenVR Status")]
        public static void DumpOpenVRStatus()
        {
            bool runtimeInstalled = OpenVR.IsRuntimeInstalled();

            bool steamVRRunning = false;
            try
            {
                var procs = Process.GetProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (name.Contains("steamvr") || name == "vrmonitor" || name == "vrserver")
                            steamVRRunning = true;
                    }
                    catch { }
                }
            }
            catch { }

            // Quick tracker count via init/enumerate/shutdown
            int trackerCount = 0;
            bool weInitialized = false;
            try
            {
                if (!OpenVRHelper.IsInitialized)
                {
                    if (OpenVRHelper.Init())
                        weInitialized = true;
                }
                if (OpenVRHelper.IsInitialized && OpenVR.System != null)
                {
                    for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                    {
                        if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                            trackerCount++;
                    }
                }
            }
            finally
            {
                if (weInitialized)
                    OpenVRHelper.Shutdown();
            }

            var vtm = Object.FindAnyObjectByType<ViveTrackerManager>();
            string vtmState = vtm != null
                ? $"IsTracking={vtm.IsTracking} IsRealHardware={vtm.IsRealHardware}"
                : "NOT IN SCENE";

            UnityEngine.Debug.Log(
                $"[Phase2Probe] STATUS " +
                $"runtimeInstalled={runtimeInstalled} " +
                $"steamVRRunning={steamVRRunning} " +
                $"trackerCount={trackerCount} " +
                $"ViveTrackerManager=[{vtmState}]");
        }
    }
}
#endif
