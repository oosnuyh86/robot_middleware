# Session Handoff — 2026-04-16

Purpose: let another session pick up cleanly without re-reading 1 MB of conversation history.

## Where we are

**Phases 1-4 complete.** Sensor integration end-to-end: Intel RealSense D435i streaming live + Intel VIVE Ultimate Tracker via direct Valve OpenVR API (the OpenXR route was proved dead on PC SteamVR and abandoned). Live pose verified in Unity Play Mode, sub-millimeter precision, RealSense↔Vive coordinate conversion validated against `vrcmd --pollposes` ground truth (X/Y match, Z negated as expected).

**Planning phase complete** for the 송월 테크놀로지 PoC (composite aircraft panel painting demo). Three reviews ran back-to-back:

- `/plan-ceo-review` — SELECTIVE EXPANSION mode, 6 of 7 cherry-picks accepted.
- `/plan-eng-review` — architecture + tests + performance, 1 [EUREKA] finding, zero critical gaps.
- `/plan-design-review` — UI review for CP2 GuidedFlowOverlay, rating improved 4/10 → 9/10.

All review logs persisted. Dashboard verdict: **FULLY CLEARED**.

**Implementation started:**

| Lane | Status | What landed |
|---|---|---|
| A.1 (CP1 — scan sub-commands) | ✅ Code written, PASS reviewer, codex inconclusive this session, MCP verification deferred | 9 files changed, 12 new tests, 29 existing preserved |
| A.2 (CP2 — GuidedFlowOverlay) | 🔲 blocked by A.1 MCP verify | — |
| B (CP3 — object-placement gate) | 🔲 queued independent | — |
| C (CP5 — cal persistence + IMU drift) | 🔲 queued independent | — |
| D (CP6 — trajectory MAE + video) | 🔲 queued independent | — |
| E (CP7 — Python sidecar + manifest) | 🔲 queued independent | — |
| F (backend tests bootstrap — E-3A) | 🔲 after C + E land | — |
| G (Unity MCP E2E harness — E-3B) | 🔲 after A-E land | — |

## Implementation path chosen

**Approach B (design-doc-approved):** capture expert trace → map Vive → UR10e via `TrackerToRobotMapper` + `UR10eIKSolver` → replay on digital twin. **No ML training in PoC.** Approach A (behavioral cloning) committed as follow-on sprint after B proves the pipeline.

Why B first: zero ML-convergence risk, uses 100% existing code in the tree, deterministic demo-day artifact that the painter can see. A follows in the next sprint.

## Critical persisted artifacts

| File | Purpose |
|---|---|
| `C:/Users/GLADPARK_PC_05/.claude/plans/nested-jumping-finch.md` | Full approved implementation plan, agent-team workflow, verification checklist |
| `C:/Users/GLADPARK_PC_05/.gstack/projects/oosnuyh86-robot_middleware/ceo-plans/2026-04-15-song-wol-poc-approach-b.md` | CEO plan with all CP decisions + DS1-DS9 design decisions + E-1A/B/C engineering decisions |
| `C:/Users/GLADPARK_PC_05/.gstack/projects/oosnuyh86-robot_middleware/gladpark-main-eng-review-test-plan-20260415-221941.md` | Test plan artifact from eng review |
| `TODOS.md` | Deferred items: auth, legacy tests, S3 keyframe upload, formalize DESIGN.md |
| `docs/SETUP.md` | Install + hardware setup guide |
| `docs/IMPLEMENTATION_ROADMAP.md` | Lane-by-lane status + next actions |
| `docs/SENSOR_ARCHITECTURE.md` | Direct-OpenVR integration explained; why not OpenXR |

## Key decisions locked (do not re-debate)

1. **Approach B before A.** No behavioral cloning in PoC. Direct replay only.
2. **VLM + SVD calibration only.** CP4 (ArUco fallback) skipped. Mitigated by pre-demo Ollama dry-run (SETUP.md step 9).
3. **Python sidecar for LeRobot HDF5.** Unity emits intermediate (.demo + CSV + keyframes + manifest.json). Python script `scripts/export_lerobot.py` writes HDF5 via h5py. [EUREKA logged.]
4. **Dual-write dataset.** ML-Agents `.demo` AND LeRobot HDF5 per episode. .demo keeps Approach A path alive; HDF5 is the portable 2026 standard.
5. **Top-banner overlay** (DS1), **uGUI+TMPro** (D3), **HUDTheme inheritance** (DS4), **150ms fade + 8px Y** motion (DS4).
6. **IMU-drift auto-detection** (D2) for calibration invalidation. +~4h dev over simple 24h expiry. RealSense IMU API must be verified during CP5.
7. **First-class Workstation mongoose model** (E-1B) — not env-var shortcut.
8. **Backend subprocess on COMPLETED** (E-1C) triggers Python sidecar.
9. **Vitest + supertest + mongodb-memory-server** (E-3A) for backend tests.
10. **Unity MCP scripted E2E** (E-3B) for all Unity user flows.
11. **Agent team gates** — every lane: integrator → reviewer → Unity MCP tester → codex adversarial → user approval.

## What Lane A.1 (CP1) actually changed

9 files, +301 lines, -2 lines. All additive. 29 existing tests preserved, 12 new added (target now 41 when Unity compiles).

Unity (changes):
- `robot_record/Assets/Scripts/DataChannel/CommandMessage.cs` — 4 enum values appended (CAPTURE_BACKGROUND=9, START_OBJECT_SCAN=10, CONFIRM_SCAN=11, RESCAN=12).
- `robot_record/Assets/Scripts/Recording/RecordingManager.cs` — `[SerializeField] ScanManager _scanManager`, Awake fallback `FindAnyObjectByType<ScanManager>()`, 4 passthroughs with state gate (throw `InvalidOperationException` when `CurrentState != Scanning`).
- `robot_record/Assets/Scripts/Controller/MiddlewareController.cs` — 4 new switch cases + `catch (InvalidOperationException)` sends ERROR via WebSocket without ACK.

Frontend (changes):
- `frontend/src/lib/webrtc/dataChannel.ts` — 4 union members added.
- `frontend/src/components/webrtc/CommandToolbar.tsx` — `onScanSubCommand` prop + conditional 4-button block (renders only when `currentState === "SCANNING"`).
- `frontend/src/app/records/[id]/page.tsx` — `handleScanSubCommand` callback sends via WebSocket; **CRITICAL:** on `CONFIRM_SCAN` only GETs record, does NOT PATCH state (because `ScanManager.ConfirmScan` already internally calls `RecordingManager.AlignSensors` which advances state — double-PATCH would race).

Tests (new — 12 total):
- `CommandMessageTests.cs` — 4 parameterized TestCase round-trip.
- `MiddlewareControllerTests.cs` — 3 tests: wrong-state rejection, Scanning-state dispatch, parse all 4 actions.
- `RecordingManagerTests.cs` — 5 tests: 4 wrong-state throws + 1 null-scanmanager-graceful.

## What this session did NOT do

- **Unity MCP E2E verification of CP1** — MCP server disconnected mid-session. Must run in next session per SETUP.md step 12.
- **Unity compile check** — Unity Editor MCP needed; deferred.
- **Codex adversarial final report on CP1** — codex CLI subprocess blocked on stdin this session. Run manually next session, command in IMPLEMENTATION_ROADMAP.md.
- **Any A.2 / B / C / D / E / F / G work.**

## Next session checklist

1. Read `C:/Users/GLADPARK_PC_05/.claude/plans/nested-jumping-finch.md` top to bottom.
2. Read `docs/IMPLEMENTATION_ROADMAP.md` for current lane status.
3. Reconnect Unity MCP (see SETUP.md step 12).
4. Run Unity MCP E2E harness for Lane A.1 (`Sensors/Phase4/...` verifiers + a new `scripts/mcp/e2e_scan_flow.sh` to be authored).
5. Run `codex review` adversarial on CP1 diff manually (command in IMPLEMENTATION_ROADMAP.md).
6. If both pass: mark Lane A.1 merged; dispatch integrator for Lane A.2 (CP2 GuidedFlowOverlay).
7. If any P0/P1 found: loop back to integrator for fix.

## Feedback / preferences to honor

Persisted in `~/.claude/projects/D--Github-robot-middleware/memory/`:

- `feedback_think_before_building.md` — walk through full end-to-end workflow scenarios before scaffolding.
- `feedback_one_question_at_a_time.md` — never batch AskUserQuestion in reviews.
- `feedback_unity_mcp_for_e2e.md` — use Unity MCP for every Unity scene + user flow.
- `project_first_job.md` — 송월 composite aircraft painting is the first real use case.
- `project_calibration_logic.md` — VLM finds tool in image, SVD computes transform from point correspondences.
- `project_ollama_local.md` — Qwen3-VL running locally via Ollama.
- `project_signaling_server.md` — WS relay E2E proven working.

## Three hazards to watch

1. **VLM/Ollama single point of failure** — CP4 ArUco fallback skipped. Pre-demo dry-run runbook mandatory (SETUP.md step 9).
2. **RealSense IMU read path** — unknown whether the current wrapper exposes IMU accel. Verify before starting CP5 implementation; fallback to 24h expiry only if unavailable.
3. **Tracker lost between sessions** — VIVE Ultimate Tracker uses inside-out SLAM, will show `Running_OutOfRange` until it re-establishes map. Wake tracker + move it around the workspace for ~10s before starting a session. `vrcmd --pollposes` should show non-zero values before Unity Play Mode.
