# Implementation Roadmap — 송월 PoC (Approach B)

Source of truth for what's merged, what's in-flight, and what's next. Paired with the full plan at `C:/Users/GLADPARK_PC_05/.claude/plans/nested-jumping-finch.md`.

## Lane status

| Lane | CP | Status | Owner agent | Last update |
|---|---|---|---|---|
| A.1 | CP1 — scan sub-commands | **CODE LANDED, VERIFICATION PENDING** | integrator + reviewer | 2026-04-16 |
| A.2 | CP2 — GuidedFlowOverlay | QUEUED (blocked by A.1 MCP verify) | unity-game-developer | — |
| B | CP3 — object placement gate | QUEUED (independent) | unity-game-developer | — |
| C | CP5 — calibration persistence + IMU drift | QUEUED (independent; see RealSense IMU risk) | general-purpose | — |
| D | CP6 — trajectory MAE + video export | QUEUED (independent) | unity-game-developer | — |
| E | CP7 — Python sidecar + Unity manifest emit | QUEUED (independent) | general-purpose | — |
| F | backend tests bootstrap (E-3A) | QUEUED (after C+E) | general-purpose | — |
| G | Unity MCP E2E harness (E-3B) | QUEUED (after A-E) | unity-game-developer | — |

## Lane A.1 — CP1 — what actually landed

9 files changed, +301 / -2 lines.

### Code
- `robot_record/Assets/Scripts/DataChannel/CommandMessage.cs` — 4 new enum values
- `robot_record/Assets/Scripts/Recording/RecordingManager.cs` — `ScanManager` field + Awake fallback + 4 passthroughs with state gate
- `robot_record/Assets/Scripts/Controller/MiddlewareController.cs` — 4 switch cases + `InvalidOperationException` catch

### Tests (new — 12)
- `CommandMessageTests.cs` — 4 parameterized `[TestCase]` round-trip
- `MiddlewareControllerTests.cs` — 3 (wrong-state rejection, Scanning-state dispatch, parse all four actions)
- `RecordingManagerTests.cs` — 5 (4 wrong-state throws + 1 null-scanmanager-graceful)

### Frontend
- `frontend/src/lib/webrtc/dataChannel.ts` — 4 new union members
- `frontend/src/components/webrtc/CommandToolbar.tsx` — `onScanSubCommand` prop + conditional 4-button block
- `frontend/src/app/records/[id]/page.tsx` — `handleScanSubCommand` (does NOT PATCH state on CONFIRM_SCAN — ScanManager internally triggers AlignSensors advance)

### Reviews completed
- **Reviewer (pr-review-toolkit:code-reviewer):** PASS. Zero P0/P1 findings. 2 trivial P2 (defensive `onScanSubCommand` guard, test doesn't spy on `SendError` call — both non-blocking).

### Reviews pending (next session)
- **Unity compile check** — MCP disconnected this session.
- **Unity EditMode tests** — run `Window → General → Test Runner → EditMode → Run All`, expect 41 tests pass.
- **Unity MCP E2E** — author + run `scripts/mcp/e2e_scan_flow.sh` (see below).
- **Codex adversarial** — command below; blocked on stdin in this session.

## Next session — exact commands

### Step 1. Reconnect services

```powershell
# Terminal A — mongod on 27018
& "C:\Program Files\MongoDB\Server\7.0\bin\mongod.exe" --port 27018 --dbpath "D:\Github\robot_middleware\.mongo-data" --bind_ip 127.0.0.1

# Terminal B — backend
cd D:\Github\robot_middleware\backend; pnpm dev

# Terminal C — frontend
cd D:\Github\robot_middleware\frontend; pnpm dev

# Ensure Ollama is running in tray
# Ensure SteamVR is running + tracker is SLAM-locked (see SETUP.md step 5e)
```

### Step 2. Unity compile + tests

Open `robot_record/` in Unity 6 (6000.3.12f1). Let domain reload finish. If any red squiggles, fix BEFORE running tests:

```
Window → General → Test Runner → EditMode → Run All
```

Expect: **41 pass / 41 total.** If any fail, root-cause and report before touching lane A.2.

### Step 3. Unity MCP E2E for CP1

Reconnect UnityMCP (SETUP.md step 12). Then in a Claude session:

```python
# Via mcp__UnityMCP__* tools
# 1. Enter Play Mode
mcp__UnityMCP__manage_editor action=play

# 2. Assert initial state
mcp__UnityMCP__read_console filter_text="[RecordingManager]"
# Expect: "State transitioned to: Idle" or similar

# 3. Simulate START_SCAN from a test harness (the actual frontend will drive this in prod)
# For now, trigger via existing menu item that sets state then verify sub-commands work:
mcp__UnityMCP__execute_menu_item menu_path="Sensors/Phase4/Scan Step1 - CaptureBackground"
mcp__UnityMCP__read_console filter_text="CaptureBackground"
# Expect: "[ScanManager] Background captured"

# 4. Screenshot for visual sanity
mcp__UnityMCP__manage_camera action=screenshot capture_source=game_view include_image=true

# 5. Exit Play Mode
mcp__UnityMCP__manage_editor action=stop
```

A fuller harness script should be authored as `scripts/mcp/e2e_scan_flow.sh` during lane G. For CP1 verification, the above is sufficient.

### Step 4. Codex adversarial review

```powershell
cd D:\Github\robot_middleware
codex exec "Adversarial review of CP1 diff vs origin/main. Find silent failures, races, null-path bugs, type mismatches, state-machine edge cases. Focus on CONFIRM_SCAN not double-PATCHing state. Report P0/P1/P2 with file:line. No hedging." -s read-only -c 'model_reasoning_effort=\"high\"'
```

Expected: zero P0. If any P0 found, dispatch integrator with fix.

### Step 5. Approval gate → merge Lane A.1

If Steps 2, 3, 4 all green:

```powershell
# Scope is already staged from this session's session-handoff commit.
# Just create the A.1 merge commit:
cd D:\Github\robot_middleware
git log --oneline -5
# Identify the scope boundary + create release tag
git tag -a cp1-merged -m "Lane A.1 (CP1 scan sub-commands) verified + merged"
git push origin cp1-merged
```

Then dispatch Lane A.2 (CP2 GuidedFlowOverlay).

## Lane A.2 — CP2 — pre-kickoff notes

Blocked on A.1 verification. When starting:

- Subagent: `unity-game-developer`
- Scope: new `Assets/Scripts/UI/GuidedFlowOverlay.cs` component, scene wiring, step-state resolution table, verbosity auto-collapse logic (persisted in CalibrationStore — but CalibrationStore lands in Lane C, so coordinate: temporarily stub the persistence, wire real store when C merges).
- Design decisions DS1-DS9 are locked (see CEO plan). Do not re-debate layout or motion choices.
- Screenshot gate: play-mode-tester must capture 10 overlay states (Idle, Scanning×BgCapture, Scanning×Preview + CP3-fail, Aligning×reuse-prompt, Aligning×VLM-flight, Aligning×VLM-low-confidence, Recording×Active, Validating, Failed, Complete) via `mcp__UnityMCP__manage_camera`.

## Lane B — CP3 — pre-kickoff notes

Independent. `ScanManager.cs` gets `DetectObjectPlacement()` method, invoked between `StopScan()` and `ConfirmScan()`. Gate: `centroidDelta > 5cm && pointCount > 500`. On fail, overlay shows "⚠ Did you place the object?" — but that overlay integration is CP2's job. For CP3 alone, just the gate + unit tests + a log line.

## Lane C — CP5 — pre-kickoff notes

Biggest lane. New files: `CalibrationStore.cs`, `IMUDriftMonitor.cs`, backend `Workstation` model + routes, `Record.workstation_id` schema change.

**Risk to clear first:** RealSense wrapper's IMU read path. Before starting, verify from `RealSenseManager.cs` + Intel SDK docs whether accel/gyro streams are exposed through the Unity wrapper. If not, fall back to 24h timestamp expiry (degrades D2 but not blocking).

## Lane D — CP6 — pre-kickoff notes

`TrajectoryComparator.cs` new. Extends `ValidationVisualizer.cs` with MAE/max-dev computation + overlaid trace render. Video export via Unity Recorder or keyframe PNGs. Acceptance: `MAE < 2cm, max < 5cm`.

## Lane E — CP7 — pre-kickoff notes

Two halves.

**Unity side:** `DemonstrationManager.cs` modified to emit per-episode intermediate: `.demo` binary (existing), `tracker.csv`, `flow.csv`, `keyframes/*.png` + `keyframes/*.exr` at 5Hz, `manifest.json`. RGB+depth local-only per D1.

**Python side:** `scripts/export_lerobot.py` new. Reads manifest.json + writes HDF5 per LeRobot v2 schema via h5py. Backend `records.controller.ts` spawns this via `child_process` on `state→COMPLETED`, updates `Record.hdf5_export_status`.

**Open question for this lane:** LeRobot HDF5 schema version to pin. Recommend `lerobot-v2.0` stable; track upstream in TODOS.md.

## Lane F — backend tests — pre-kickoff

`backend/test/` bootstrap with Vitest + supertest + mongodb-memory-server. Covers CP5 `Workstation` CRUD, `/api/alignment/latest/:workstationId`, CP7 sidecar subprocess trigger (mocked child_process). Pre-existing endpoints (companies/professionals/jobs/records CRUD) are deferred to TODOS.md P2.

## Lane G — Unity MCP E2E harness — pre-kickoff

6 scripts to author:
1. `scripts/mcp/e2e_scan_flow.sh` — CAPTURE_BACKGROUND → gate pass → CONFIRM_SCAN
2. `scripts/mcp/e2e_scan_empty_table_gate.sh` — skip object → CP3 gate fires
3. `scripts/mcp/e2e_calibration_reuse.sh` — two records → second shows reuse prompt
4. `scripts/mcp/e2e_imu_drift.sh` — simulate motion → forced fresh cal
5. `scripts/mcp/e2e_full_painter_session.sh` — create→scan→align→record→validate→export, HDF5 written
6. `scripts/mcp/screenshot_overlay_states.sh` — 10 CP2 state screenshots

Each script wraps `mcp__UnityMCP__*` calls with assertions. Runs pre-demo for dry-run.

## Locked design + eng decisions (do not re-open)

| Ref | Decision |
|---|---|
| Approach | B (direct replay) for PoC; A (BC) next sprint |
| CP4 | SKIPPED (ArUco fallback) — pre-demo dry-run mandatory |
| D1 | HDF5 keyframes at 5Hz; RGB+depth local-only |
| D2 | Manual + IMU drift auto-invalidation |
| D3 | uGUI + TMPro |
| E-CP7 | Python sidecar, not Unity-native HDF5 [EUREKA] |
| E-1A | Passthrough via RecordingManager |
| E-1B | First-class Workstation mongoose model |
| E-1C | Backend subprocess on state=COMPLETED |
| E-3A | Vitest + supertest + mongodb-memory-server |
| E-3B | Unity MCP scripted E2E harness |
| DS1 | Top banner, 80% width, 800-1400px |
| DS2 | Auto-collapse verbosity after 3 records |
| DS4 | 150ms fade + 8px Y-offset, HUDTheme inherit |
| DS6 | Recording × Active minimizes to red dot + time |
| DS8 | Variable step count per-session |
| DS9 | Point-cloud thumbnail + badge on scan preview |

## TODOS deferred (already in `TODOS.md`)

- P1: JWT auth for backend + WebSocket
- P2: Legacy endpoint tests
- P2: S3 upload of RGB+depth keyframes
- P2: Formalize DESIGN.md via `/design-consultation`

## Risks

1. **VLM single-point-of-failure** on demo day — mitigation is the pre-demo Ollama dry-run runbook.
2. **RealSense IMU exposure** — verify before committing to D2 full implementation.
3. **Python sidecar subprocess handling** — backend must `unref()` + handle SIGCHLD to avoid zombies. Codex adversarial review will flag if missed.
4. **HDF5 schema drift** — pin to `lerobot-v2.0`; track upstream changes.
5. **Object placement 5cm threshold** — empirical, needs demo-room calibration. Instrumentation logs centroid deltas for tuning.

## Dashboard snapshot (end of planning)

```
+====================================================================+
|                    REVIEW READINESS DASHBOARD                       |
+====================================================================+
| Review          | Runs | Last Run            | Status       | Req |
|-----------------|------|---------------------|--------------|-----|
| CEO Review      |  1   | 2026-04-15 13:11    | CLEAR        | no  |
| Eng Review      |  1   | 2026-04-15 13:26    | CLEAR (PLAN) | YES |
| Design Review   |  1   | 2026-04-15 13:38    | CLEAR (FULL) | no  |
| Adversarial     |  0   | —                   | —            | no  |
| Outside Voice   |  0   | —                   | —            | no  |
+--------------------------------------------------------------------+
| VERDICT: FULLY CLEARED — ready to implement                         |
+====================================================================+
```
