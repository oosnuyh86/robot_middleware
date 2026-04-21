# Setup — Robot Middleware

Complete install + hardware setup for the 송월 PoC. Run through every step on a fresh Windows 11 machine. Target: `CommandToolbar` on dashboard can drive a full session end-to-end with VIVE tracker + RealSense camera live.

## 1. System requirements

- **OS:** Windows 11 (tested on 10.0.26200)
- **GPU:** discrete GPU recommended for Unity. Tested on NVIDIA.
- **Network:** LAN access for dashboard ↔ Unity WebSocket relay on port 4000. Can be same machine.
- **Disk:** ~50 GB free (Unity project + MongoDB data + recorded demos + Steam SDK).
- **RAM:** 32 GB recommended (Unity + Ollama + MongoDB + Next.js dev + Python training later).

## 2. Core software

Install in this order. Open a fresh PowerShell for each.

### 2a. Node.js 20 LTS + pnpm

```powershell
winget install OpenJS.NodeJS.LTS
npm install -g pnpm
node --version   # >= 20.x
pnpm --version   # >= 9
```

### 2b. MongoDB Community 7.0

```powershell
winget install MongoDB.Server
```

Installer registers `MongoDB` Windows service. We use a **custom data dir on port 27018** (keeps this project's data separate from any other MongoDB workload). See step 6.

### 2c. Python 3.11

Needed for ML-Agents, Ollama client interactions, and the CP7 LeRobot HDF5 sidecar (lane E).

```powershell
winget install Python.Python.3.11
python --version   # 3.11.x
```

### 2d. Unity 6 (6000.3.12f1)

Install via Unity Hub. Add modules:
- Microsoft Visual Studio Community 2022 OR JetBrains Rider (for C# editor integration).
- **Windows Build Support (IL2CPP)** for any standalone build.

Unity Hub: <https://unity.com/download>

### 2e. Git for Windows

```powershell
winget install Git.Git
```

Clone the repo:

```powershell
cd D:\Github
git clone https://github.com/oosnuyh86/robot_middleware.git
cd robot_middleware
```

## 3. Ollama + Qwen3-VL (for VLM calibration)

The calibration pipeline calls Qwen3-VL via local Ollama to locate a handheld tool in RGB frames. **No cloud fallback** — if Ollama isn't up on demo day, calibration fails (this is a known risk; see CP4 in the plan).

```powershell
# Install Ollama
winget install Ollama.Ollama

# Pull the vision model (~5 GB)
ollama pull qwen3-vl:8b

# Verify it's up
curl http://localhost:11434/api/tags
# should list qwen3-vl:8b
```

Keep Ollama running as a background service in the system tray.

## 4. Intel RealSense SDK

The RealSense D435i is used for depth + RGB capture during scanning + calibration.

1. Install **Intel RealSense SDK 2.55.1** from the Intel release page. The Unity wrapper `Intel.RealSense.dll` is already committed to `robot_record/Assets/RealSenseSDK2.0/`, but the native `realsense2.dll` must be installed at the OS level.
2. Plug in the D435i.
3. Open **Intel RealSense Viewer** from the Start menu and confirm depth + RGB streams appear. Firmware should be 5.17.0.10 or higher; upgrade via the Viewer if older.

Troubleshooting: if Unity logs `RealSenseManager IsStub=True` when you expect real hardware, the native DLL is missing from PATH or Unity's working directory.

## 5. VIVE Ultimate Tracker + SteamVR

### 5a. Install Steam + SteamVR

```powershell
winget install Valve.Steam
```

Log in, then install SteamVR from the Steam store (free). Install path typically `D:\Steam\steamapps\common\SteamVR`.

### 5b. Copy openvr_api.dll into the project

This is already committed as `robot_record/Assets/Plugins/x86_64/openvr_api.dll`. If you ever need to refresh it:

```powershell
copy "D:\Steam\steamapps\common\SteamVR\bin\win64\openvr_api.dll" `
     "D:\Github\robot_middleware\robot_record\Assets\Plugins\x86_64\openvr_api.dll"
```

### 5c. Install VIVE Hub 2.5.4 BETA

Download from <https://www.vive.com/us/setup/hub/>. Required for VIVE Ultimate Tracker pairing and inside-out SLAM room setup.

### 5d. Pair the VIVE Ultimate Tracker

1. Open **VIVE Hub**.
2. Put the tracker into pairing mode (hold power button ~3 seconds, LED blinks blue).
3. Hub detects it and walks you through pairing.
4. Tracker serial shows up in SteamVR device list. Our tested tracker: `53-A33501900` (VIVE_Ultimate_Tracker_1).

### 5e. Run Room Setup (inside-out SLAM mapping)

Ultimate Tracker has no base stations. It uses its own cameras + SLAM to track the environment.

1. Open **VIVE Hub → Room Setup**. Follow the wizard.
2. Move the tracker slowly around the workspace, pointing its cameras at textured surfaces (wall features, posters, edges — not blank walls).
3. Wait until Hub reports "Tracking: OK".
4. Confirm with `vrcmd`:

```powershell
& "D:\Steam\steamapps\common\SteamVR\bin\win64\vrcmd.exe" --pollposes | Select-Object -First 10
```

You should see non-zero device-1 rows like `1, -0.604674, -0.350498, -0.211888, ...`. If it shows all zeros, SLAM is not locked — move the tracker around more, or the room needs more visual features (add a poster on blank walls).

### 5f. SteamVR background mode

OpenVRHelper.cs initializes SteamVR as `EVRApplicationType.VRApplication_Other` (not `_Background` — `_Background` gives restricted device access). SteamVR does not need an HMD connected. On first Unity Play Mode, SteamVR may auto-launch — that's expected.

### 5g. Verify in Unity Editor (no Play Mode)

In Unity, `Sensors → Phase2 → Dump OpenVR Status` menu item logs:

```
[Phase2Probe] STATUS runtimeInstalled=True steamVRRunning=True trackerCount=1 ViveTrackerManager=[IsTracking=False IsRealHardware=False]
```

Tracker count should be ≥ 1. IsTracking flips to True once in Play Mode.

## 6. MongoDB (port 27018)

We run a **user-level mongod on 27018** with a project-local data dir. Do NOT use the default `MongoDB` Windows service on 27017 — it may conflict with other installed Mongo workloads.

### First-time: create data dir

```powershell
mkdir D:\Github\robot_middleware\.mongo-data
```

This dir is in `.gitignore`.

### Start mongod (each session)

Run in its own terminal, keep it open:

```powershell
& "C:\Program Files\MongoDB\Server\7.0\bin\mongod.exe" `
    --port 27018 `
    --dbpath "D:\Github\robot_middleware\.mongo-data" `
    --bind_ip 127.0.0.1
```

Success: logs `Waiting for connections on port 27018`.

## 7. Backend

```powershell
cd D:\Github\robot_middleware\backend
pnpm install

# One-time .env setup
notepad .env
```

`.env` contents:

```
MONGO_URI=mongodb://127.0.0.1:27018/robot_middleware
PORT=4000
SIGNALING_PATH=/signaling
AWS_REGION=ap-northeast-2
AWS_ACCESS_KEY_ID=<your key>
AWS_SECRET_ACCESS_KEY=<your secret>
S3_BUCKET=etcserver
OLLAMA_URL=http://localhost:11434
```

Start dev server (separate terminal, keep open):

```powershell
pnpm dev
```

Verify:
- <http://localhost:4000/api> → JSON listing endpoints
- <http://localhost:4000/api/health> → `{"status":"ok"}`

WebSocket relay listens at `ws://localhost:4000/ws` (Unity connects with `?role=unity`, dashboard with `?role=dashboard`).

## 8. Frontend

```powershell
cd D:\Github\robot_middleware\frontend
pnpm install
pnpm dev
```

Opens at <http://localhost:3001> (we moved off 3000 to avoid the common-default collision).

Verify:
- Page title "Robot Middleware" renders.
- Companies/Professionals/Jobs/Records CRUD forms load.
- Dashboard's WebSocket panel shows `Connected` after Unity also connects.

## 9. Pre-demo VLM dry-run (CRITICAL — 24h before 송월)

Because CP4 (ArUco calibration fallback) was skipped, VLM calibration is the only path. If Ollama is unhealthy on demo day, the whole demo fails. Mandatory dry-run 24h before:

1. `curl http://localhost:11434/api/tags` → confirm `qwen3-vl:8b` is present.
2. Start backend (step 7). Confirm it says `MongoDB Connected`.
3. POST a known-good test image to `/api/alignment/locate-tool`. Expect JSON with confidence > 0.6.
4. In Unity, `Sensors → Phase4 → Verify RealSense` + `Sensors → Phase4 → Verify ViveTracker` — both must report `PASS`.
5. Run an end-to-end record session with a dummy object. Confirm `Record.state → COMPLETED`.

If any step fails → recalibrate Ollama OR restart SteamVR Room Setup before the painter arrives.

## 10. Unity project open

1. Unity Hub → Add Project → select `D:\Github\robot_middleware\robot_record\`.
2. First open takes ~5-10 min while Unity imports + regenerates .meta + shader cache.
3. Open scene `Assets/Scenes/MiddlewareScene.unity`.
4. Inspector sanity check on scene root:
   - `MiddlewareController` has `_recordingManager` wired.
   - `RecordingManager` has `_scanManager` wired (CP1 added this field — if you upgraded from a pre-CP1 version, the field will be null; drag-drop `ScanManager` GameObject into the slot).
   - `RealSenseManager` present, `ViveTrackerManager` present, `HUDController` present.

## 11. First run smoke test

In this order:

1. Mongod on 27018 ← open terminal A.
2. Ollama running in tray.
3. Backend `pnpm dev` on 4000 ← terminal B.
4. Frontend `pnpm dev` on 3001 ← terminal C.
5. Unity Play Mode on MiddlewareScene ← Unity editor.
6. Open <http://localhost:3001>, create Company → Professional → Job → Record.
7. Dashboard's CommandToolbar → click "Start Scan". Unity's HUDController shows `Recording: Scanning`.
8. CommandToolbar now shows 4 new scan sub-buttons (CP1):
   - Click "Capture Background" → Unity logs `[ScanManager] Background captured`.
   - Click "Start Object Scan" → Unity begins scanning, point count climbs.
   - Let it run 10-20 seconds, then "Confirm Scan" OR "Rescan".
   - On Confirm, `RecordingManager` auto-advances to `Aligning`.

## 12. Unity MCP — reconnecting

Unity MCP (Coplay Dev's MCP-For-Unity package) provides scripted Play Mode control + screenshot + console-read from Claude Code sessions. Already listed in `robot_record/Packages/manifest.json`:

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```

### 12a. Enable MCP inside Unity

In Unity Editor menu: `Window → UnityMCP → MCP Setup`. Follow wizard — starts a local MCP server.

### 12b. Check `.mcp.json` in repo root

Should contain an entry for `UnityMCP`. If missing or stale, re-run the MCP Setup wizard.

### 12c. Verify connection from Claude Code session

In a fresh Claude Code session at this repo root:

```
resource mcpforunity://instances
```

Should list the active Unity instance like `MiddlewareScene@<hash>`. If connected, tools `mcp__UnityMCP__execute_menu_item`, `mcp__UnityMCP__read_console`, `mcp__UnityMCP__manage_camera` are callable.

### 12d. Screenshot for design verification

```
mcp__UnityMCP__manage_camera action=screenshot capture_source=game_view include_image=true
```

Returns a base64 PNG inline in the session. Use this for CP2 GuidedFlowOverlay design sign-off.

## 13. Running the test suite

### Unity tests (EditMode)

In Unity: `Window → General → Test Runner → EditMode → Run All`. Target count after CP1 is **41 tests** (29 pre-existing + 12 CP1-new). All must pass.

### Backend tests (once lane F lands)

```powershell
cd backend
pnpm test
```

Uses Vitest + supertest + mongodb-memory-server. Currently zero tests; lane F (E-3A in plan) bootstraps this.

### Python sidecar tests (once lane E lands)

```powershell
cd scripts
pytest
```

## 14. Common breakages

| Symptom | Cause | Fix |
|---|---|---|
| Unity: `RealSenseManager IsStub=True` with camera plugged in | Native `realsense2.dll` not on PATH | Reinstall RealSense SDK, ensure `C:\Program Files (x86)\Intel RealSense SDK 2.0\bin\x64` is in PATH |
| Unity: `bPoseIsValid=False, eTrackingResult=Running_OutOfRange` | VIVE Ultimate Tracker SLAM not locked | Run VIVE Hub Room Setup again; ensure workspace has textured surfaces, not blank walls |
| Backend: `MongooseServerSelectionError 127.0.0.1:27018` | mongod not running on 27018 | Start mongod per step 6. Check it's on 27018, not 27017. |
| `svchost.exe` holds port 27017 | Windows port reservation or an old MongoDB service | Use 27018 as specified; do not try to free 27017 |
| `Cannot GET /api` in browser | Old backend without the `/api` index route | Pull latest; `/api` index route was added 2026-04-15 |
| Unity MCP says "session not ready" | Just started Play Mode; compile domain reload in flight | Wait 5-10s, retry |
| `setup` script warns CRLF/LF | git line-ending normalization | Benign warning on Windows; safe to ignore |

## 15. Services cheat sheet

| Service | Port | Start command | Required |
|---|---|---|---|
| MongoDB | 27018 | see step 6 | always |
| Backend | 4000 | `pnpm dev` in `backend/` | always |
| Frontend | 3001 | `pnpm dev` in `frontend/` | always |
| Ollama | 11434 | system tray / `ollama serve` | calibration |
| SteamVR | — | auto-launched by Unity | vive tracker |
| Unity Editor + MCP | — | Unity Hub + Play Mode | always |
