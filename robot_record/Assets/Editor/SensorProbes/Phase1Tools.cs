// Phase1Tools.cs — RealSense Phase 1 editor probes
// Lives under Assembly-CSharp-Editor. Hard-gated by REALSENSE_SDK define.
// DO NOT rely on runtime references; editor-only.

using UnityEditor;
using UnityEngine;

#if REALSENSE_SDK
using Intel.RealSense;
using System;
#endif

namespace RobotMiddleware.EditorTools
{
    public static class Phase1Tools
    {
        private const string MENU_IMPORT = "Sensors/Phase1/Import Wrapper";
        private const string MENU_SMOKE  = "Sensors/Phase1/Run Smoke Test";

        [MenuItem(MENU_IMPORT)]
        public static void ImportWrapper()
        {
            const string pkg = @"C:\tmp\phase1_download\Intel.RealSense.v2.55.1.unitypackage";
            Debug.Log($"[Phase1] ImportWrapper: {pkg}");
            AssetDatabase.ImportPackage(pkg, false);
            Debug.Log("[Phase1] ImportWrapper: ImportPackage call returned (async import in progress).");
        }

        [MenuItem(MENU_SMOKE)]
        public static void RunSmokeTest()
        {
#if !REALSENSE_SDK
            Debug.LogError("[Phase1] RunSmokeTest: REALSENSE_SDK define is OFF. Aborting.");
            return;
#else
            Debug.Log("[Phase1] RunSmokeTest: BEGIN");
            Pipeline pipe = null;
            try
            {
                using (var ctx = new Context())
                {
                    var devices = ctx.QueryDevices();
                    Debug.Log($"[Phase1] device_count={devices.Count}");
                    if (devices.Count == 0)
                    {
                        Debug.LogError("[Phase1] No RealSense devices enumerated. FAIL.");
                        return;
                    }
                    for (int i = 0; i < devices.Count; i++)
                    {
                        var dev = devices[i];
                        string name   = dev.Info.GetInfo(CameraInfo.Name);
                        string serial = dev.Info.GetInfo(CameraInfo.SerialNumber);
                        string fw     = dev.Info.GetInfo(CameraInfo.FirmwareVersion);
                        Debug.Log($"[Phase1] device[{i}] name={name} serial={serial} fw={fw}");
                    }
                }

                pipe = new Pipeline();
                using (var cfg = new Intel.RealSense.Config())
                {
                    cfg.EnableStream(Stream.Depth, 640, 480, Format.Z16, 30);
                    cfg.EnableStream(Stream.Color, 640, 480, Format.Rgb8, 30);

                    using (PipelineProfile profile = pipe.Start(cfg))
                    {
                        Debug.Log("[Phase1] pipeline_started=true");

                        using (FrameSet frames = pipe.WaitForFrames(2000))
                        {
                            if (frames == null)
                            {
                                Debug.LogError("[Phase1] WaitForFrames returned null. FAIL.");
                                return;
                            }

                            using (var color = frames.ColorFrame)
                            {
                                if (color != null)
                                    Debug.Log($"[Phase1] color={color.Width}x{color.Height}");
                                else
                                    Debug.LogError("[Phase1] color frame NULL. FAIL.");
                            }

                            using (var depth = frames.DepthFrame)
                            {
                                if (depth != null)
                                    Debug.Log($"[Phase1] depth={depth.Width}x{depth.Height}");
                                else
                                    Debug.LogError("[Phase1] depth frame NULL. FAIL.");
                            }
                        }
                    }
                }

                Debug.Log("[Phase1] RunSmokeTest: END OK");
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"[Phase1] DllNotFoundException: {e.Message}");
            }
            catch (EntryPointNotFoundException e)
            {
                Debug.LogError($"[Phase1] EntryPointNotFoundException: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Phase1] Exception: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                try { pipe?.Stop(); } catch { /* idempotent */ }
                pipe?.Dispose();
            }
#endif
        }
    }
}
