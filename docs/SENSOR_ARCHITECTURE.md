# Sensor Architecture

Why RealSense + VIVE, why direct OpenVR instead of OpenXR, how the coordinate systems connect.

## Hardware

| Sensor | Model | Role | Serial (tested) |
|---|---|---|---|
| Depth camera | Intel RealSense D435i | Scanning + VLM calibration input | 115222070639 |
| Tracker | VIVE Ultimate Tracker | Painter 6DoF wrist motion | FA53M3B00475 / 53-A33501900 |
| Flow meter | Alicat (future) | Spray trigger pressure | — |

## Software stack

- **RealSense wrapper:** `robot_record/Assets/RealSenseSDK2.0/` — managed Unity wrapper v2.55.1 over native `realsense2.dll` v2.57.7.
- **OpenVR binding:** `robot_record/Assets/Scripts/Sensors/OpenVR/openvr_api.cs` — Valve's generated C# binding (~8886 lines), paired with `Assets/Plugins/x86_64/openvr_api.dll`. No SteamVR Unity Plugin used.

## Why direct OpenVR, not OpenXR

We initially tried OpenXR with the `XR_HTC_vive_xr_tracker_interaction` extension via the HTC ViveSoftware OpenXR package. Four phases of testing proved it dead on PC SteamVR:

- SteamVR *advertises* the extension.
- Unity's OpenXR runtime *creates* 5 ViveTracker `InputDevice`s.
- All 5 report `pos=(0,0,0)`, `isTracked=False`. **Zero pose data flows** through the OpenXR pipeline on PC.
- Meanwhile, `vrcmd.exe --info` confirms SteamVR sees the tracker with live 6DoF pose at ~60Hz via the native OpenVR API.

The HTC OpenXR package targets Android only (`BuildTargetGroups = new[] { BuildTargetGroup.Android }`). On PC, the pose delivery path is broken.

**Decision:** drop OpenXR entirely. Use Valve's native OpenVR C# binding + `GetDeviceToAbsoluteTrackingPose`. Works immediately.

## OpenVRHelper and ViveTrackerManager

Two files own the tracker integration:

### `robot_record/Assets/Scripts/Sensors/OpenVR/OpenVRHelper.cs`

Static helper. Ref-counted Init/Shutdown so editor menu items + Play Mode + background monitors can share a single OpenVR session.

Key responsibilities:
- `Init()` — `OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other)`. `_Other` is deliberate (not `_Background`) — `_Background` restricts device access too much and reports `bDeviceIsConnected=False`.
- `Shutdown()` — only shuts down when refcount reaches zero.
- `GetPosition(HmdMatrix34_t m)` — extracts position and negates Z (SteamVR right-handed → Unity left-handed).
- `GetRotation(HmdMatrix34_t m)` — builds a 4x4 matrix from the 3x4 SteamVR matrix, negates the Z row and Z column of the 3x3 rotation submatrix, extracts the Unity-handed quaternion.
- `FindTracker(serialFilter)` — iterates tracked devices, matches `ETrackedDeviceClass.GenericTracker`, returns index. Optional serial substring filter for multi-tracker rigs. Warns when >1 tracker found without a filter set.
- `GetDeviceSerial(index)` — `Prop_SerialNumber_String` lookup.

### `robot_record/Assets/Scripts/Sensors/ViveTrackerManager.cs`

MonoBehaviour. Polls `OpenVR.System.GetDeviceToAbsoluteTrackingPose(TrackingUniverseStanding, 0f, poses)` each Update. On valid pose, extracts pos/rot via OpenVRHelper and fires `OnPoseUpdated` event.

Public API:
- `Vector3 Position`
- `Quaternion Rotation`
- `bool IsTracking`
- `bool IsRealHardware`
- `int TrackingStateBits` (3 = position + rotation, 0 = stub/missing)
- `event OnPoseUpdatedDelegate OnPoseUpdated`

Two fallback modes:
- `_requireHardware = false` (default, production): if no real pose for `FallbackDelaySeconds` seconds, falls back to a synthetic orbit. Preserves the downstream pipeline (HUDController, TrackerToRobotMapper, etc.) when hardware is unplugged mid-session.
- `_requireHardware = true` (smoke-test): no stub. Logs loud error, `IsTracking=false`, `IsRealHardware=false`.

Hot-plug resilience: if the tracker disconnects, the device index changes. Re-scans every 2 seconds.

## Coordinate conversion

SteamVR is right-handed (Y-up, Z-backward). Unity is left-handed (Y-up, Z-forward). Conversion is:

- **Position:** `Unity(x,y,z) = SteamVR(x, y, -z)`
- **Rotation:** build 4x4 from 3x4 matrix, negate m02/m12 and m20/m21 (Z row + Z column of the rotation submatrix), extract quaternion.

**Verified live against ground truth:**

```
vrcmd --pollposes device 1 (tracker) → (-0.604674, -0.350498, -0.211888)
ViveTrackerManager.Position        → (-0.60,    -0.35,    +0.21)
                                             ↑ X match   Y match   Z negated ✓
```

## RealSense integration

`robot_record/Assets/Scripts/Sensors/RealSenseManager.cs` owns the Intel wrapper. Exposes:
- `ColorTexture` — RGB as Texture2D
- `DepthTexture` — R16 depth as Texture2D
- `CopyDepthData(ushort[] buffer)` — for downstream processing without GPU readback
- `DepthScale` — raw-value → meters conversion
- `IsStreaming` / `IsStub` — state flags

Calibration (`AlignmentManager.cs`) reads a color frame + `GetPixel` on the depth frame at the VLM-detected tool pixel, applies pinhole camera model with intrinsics `(fx=384, fy=384, cx=320, cy=240)` to compute the 3D camera-space point.

## Calibration flow (RealSense ↔ Vive)

The two sensors must share one world. We do hand-eye-style calibration via 4 correspondence points:

1. Operator places Vive tracker on a known physical landmark.
2. Dashboard triggers `ALIGN_SENSORS`.
3. Unity sends RGB frame to backend `/api/alignment/locate-tool`.
4. Backend calls local Ollama with Qwen3-VL: "Where is the tracker tool in this image?" → returns pixel `(u,v)` + confidence.
5. Unity reads depth at `(u,v)` → computes 3D camera-space point via pinhole intrinsics.
6. Stores pair: `(cameraPoint_xyz, tracker_xyz)`.
7. After 4 points, backend `/api/alignment/compute-transform` runs SVD on the correspondences → returns rigid transform `cameraFrame → trackerFrame`.
8. Unity applies the transform at runtime to bring sensors into the shared coordinate frame.

**Known failure modes:**
- Ollama not running → step 4 fails. No ArUco fallback (CP4 skipped). Mitigated by pre-demo dry-run.
- Low VLM confidence (< 0.3) → warning logged, point may be inaccurate. CP5 will add UI recovery ("Retry / Skip point").
- Tripod nudged after calibration → stale transform applied to new recordings. CP5 adds IMU drift auto-detection (D2).

## Phase 4 verification evidence

At Play Mode with live tracker + RealSense:

```
[Phase4] ViveTracker OpenVRHelper.IsInitialized=True
[Phase4] ViveTracker OpenVR trackerCount=1
[Phase4] IsTracking=True, IsRealHardware=True,
         Position=(-0.60, -0.35, 0.21),
         Rotation=(-0.25392, -0.45950, -0.14735, 0.83826),
         TrackingStateBits=3
[Phase4] ViveTracker Verification PASS
[Phase4] ViveTracker Delta PASS: motion detected (0.000396 m)  ← sub-mm sensor jitter from stationary tracker
```

```
[Phase4] RealSense IsStreaming=True, IsStub=False
[Phase4] RealSense colorVariance=0.031, depthNonZeroPercent=73.4%
[Phase4] RealSense Verification PASS
```
