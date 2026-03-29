# Unity WebRTC Integration Readiness Review
**Date:** 2026-03-29
**Reviewer:** Unity Integration Agent (READ-ONLY)
**Project:** robot_record (D:\Github\robot_middleware\robot_record)

---

## Executive Summary

The Unity project (`robot_record`) is a WebRTC-based video chat application built on the **Byn.Awrtc** library. While it has a functional WebRTC foundation with video/audio capabilities and data channel support, **significant integration work is needed** to connect it to the backend middleware API.

**Status:** ⚠️ **NOT READY** for backend integration
- ✅ WebRTC signaling configured
- ✅ DataChannel message protocol established
- ❌ No REST API client code
- ❌ No backend-specific integration scripts
- ❌ No configuration management for backend URLs

---

## 1. Signaling URL Configuration

### Current State
**Location:** `Assets/WebRtcVideoChat/examples/ExampleGlobals.cs` (lines 53-127)

The signaling server is hardcoded with the public test server:
```
Domain: s.y-not.app
SignalingCallApp: wss://s.y-not.app/callapp
SignalingChatApp: wss://s.y-not.app/chatapp
```

### How It's Used
- **CallApp.cs:25** - `public string uSignalingUrl = ExampleGlobals.SignalingCallApp;`
- **ChatApp.cs:33** - `public string uSignalingUrl = ExampleGlobals.SignalingChatApp;`

Both are public serialized fields in the MonoBehaviour, making them editable in the Inspector at runtime.

### What Needs to Change
❌ **Current**: Points to external test server `s.y-not.app`
✅ **Required**: Point to backend's `/signaling` endpoint

**Integration needed:**
1. Create a new configuration class (e.g., `Assets/Scripts/Config/BackendConfig.cs`) that:
   - Loads backend URL from a ScriptableObject or player preferences
   - Provides separate configs for development/staging/production
   - Supports both WebSocket (`wss://`) for signaling and REST base URL

2. Modify `ExampleGlobals.cs` or create a parallel `MiddlewareGlobals.cs` with:
   ```
   public static string MiddlewareSignalingUrl = "wss://backend-server/signaling"
   ```

3. Update `CallApp.cs` and related apps to use the new backend URL

### Current ICE Server Configuration
- **STUN:** `stun:t.y-not.app:443`
- **TURN:** `turn:t.y-not.app:443` (credentials: user_nov/pass_nov)
- **Backup:** `stun:stun.l.google.com:19302` (Google public STUN)

These are also in `ExampleGlobals.cs` and will work for NAT traversal but should be validated with your backend deployment.

---

## 2. DataChannel Protocol Analysis

### Message Format: String/Text-Based

**Location:** `Assets/WebRtcVideoChat/chatapp/ChatApp.cs` (lines 350-407)

The current implementation uses **UTF-8 encoded text messages**:

```csharp
// Sending (line 388)
byte[] msgData = Encoding.UTF8.GetBytes(msg);
mNetwork.SendData(id, msgData, 0, msgData.Length, reliable);

// Receiving (line 354)
string msg = Encoding.UTF8.GetString(buffer.Buffer, 0, buffer.ContentLength);
```

### Reliability Modes
- **Reliable (TCP-like):** `SendData(..., true)` - guaranteed delivery, ordered
- **Unreliable (UDP-like):** `SendData(..., false)` - no guarantees

Current implementation defaults to **reliable** messaging (line 380).

### Message Protocol Example
```
// Chat app format: "ConnectionId:Message"
"0:Hello from server"
"1:User 1 joined the room"

// Simple string messages sent as UTF-8 bytes
```

### What Needs to Change
The chat app's simple string protocol is **insufficient** for command dispatching. Need:

**JSON-based protocol** for structured commands:
```json
{
  "type": "command",
  "action": "startScanning" | "alignPart" | "startRecording" | "stopRecording",
  "timestamp": 1234567890,
  "payload": {
    "partId": "xyz",
    "settings": {}
  }
}
```

**Required Implementation:**
1. Create `Assets/Scripts/DataChannel/CommandMessage.cs` with:
   - Message type enum (Command, Status, Error, etc.)
   - Action enum (StartScanning, AlignPart, StartRecording, StopRecording)
   - Serialization to/from JSON
   - Versioning for future protocol changes

2. Create `Assets/Scripts/DataChannel/CommandHandler.cs` to:
   - Parse incoming JSON messages
   - Dispatch to appropriate action handlers
   - Send ACK/NACK responses
   - Handle parsing errors gracefully

### Current DataChannel Limitation
The Byn.Awrtc library's `IBasicNetwork.SendData()` API only supports:
- Raw byte arrays
- No built-in serialization (must manually encode/decode JSON)
- No request/response correlation (no message IDs) - reliability is only at transmission level

---

## 3. REST API Capabilities

### Available HTTP Infrastructure

**Package:** `com.unity.modules.unitywebrequest:1.0.0` ✅

Located in `Packages/manifest.json` (line 38). This is Unity's built-in HTTP module.

### What's NOT Present

❌ **No existing HTTP utility classes** - searched entire Assets folder for HTTP client implementations
❌ **No existing REST API integration** - no calls to external APIs found
❌ **No JSON serialization framework** - would need to use Unity's built-in `JsonUtility` or third-party package

### What Needs to Be Built

Three new integration scripts required:

#### 1. **MiddlewareClient.cs** (HTTP REST Client)
```csharp
public class MiddlewareClient
{
    // Required endpoints:
    public async Task<ApiResponse> PatchRecordState(string recordId, RecordState newState)
    // Called when: scan completes, alignment completes, recording starts/stops

    public async Task<PresignedUrlResponse> GetPresignedUploadUrl(string fileName, long fileSize)
    // Called when: ready to upload recorded data to S3
}
```

**Specifications:**
- Use `UnityWebRequest` for HTTP calls
- Support HTTPS with certificate validation
- Implement retry logic (exponential backoff)
- Handle timeouts (recommend 30s for long operations)
- Log requests/responses for debugging
- Endpoint base URL configurable (from BackendConfig)

**Required HTTP Methods:**
- `PATCH /api/records/{id}/state` - Update recording state (body: `{"state": "scanning|aligning|recording|complete"}`)
- `POST /api/uploads/presigned-url` - Get S3 presigned URL (body: `{"fileName": "...", "fileSize": ...}`)

#### 2. **CommandHandler.cs** (DataChannel Message Dispatcher)
```csharp
public class CommandHandler : MonoBehaviour
{
    public event Action<ScanningCommand> OnScanningCommand;
    public event Action<AlignmentCommand> OnAlignmentCommand;
    public event Action<RecordingCommand> OnRecordingCommand;

    public void HandleMessage(string jsonMessage)
    // Parse JSON, validate, dispatch to handlers, send ACK
}
```

**Specifications:**
- Subscribe to `IBasicNetwork` message events (unreliable or reliable)
- Parse incoming JSON command messages
- Validate message structure and required fields
- Dispatch to command-specific handlers
- Send confirmation messages back (optional, for debugging)
- Log all received commands to console/file for troubleshooting

#### 3. **RecordingManager.cs** (State Machine Orchestrator)
```csharp
public class RecordingManager : MonoBehaviour
{
    private enum RecordingState { Idle, Scanning, Aligning, Recording, Uploading, Complete }

    public async Task StartScanning(ScanParameters params)
    public async Task AlignPart(AlignmentData data)
    public async Task StartRecording(RecordingSettings settings)
    public async Task StopRecording()
    // Orchestrates state transitions, calls MiddlewareClient, handles errors
}
```

**Specifications:**
- Implements state machine with transitions: Idle → Scanning → Aligning → Recording → Uploading → Complete
- Validates state transitions (e.g., can't start recording from Idle)
- Calls `MiddlewareClient.PatchRecordState()` on each transition
- Handles command failures (rollback, retry, or error notification)
- Manages timers/timeouts for long-running operations
- Serializes state for save/resume capability

---

## 4. Package Dependencies Analysis

### Manifest.json (Packages/manifest.json)

**Installed Packages:**
- ✅ `com.unity.ai.navigation` (2.0.11) - not needed for middleware
- ✅ `com.unity.modules.unitywebrequest` (1.0.0) - **REQUIRED for REST API**
- ✅ `com.unity.inputsystem` (1.19.0) - for UI input
- ✅ `com.unity.render-pipelines.universal` (17.3.0) - graphics pipeline
- ✅ `com.coplaydev.unity-mcp` (git) - MCP integration (for AI tooling)
- ✅ All core modules enabled

**NOT Present (and not needed):**
- ❌ ML-Agents (not needed for this use case)
- ❌ Robotics packages (robot control handled by backend)
- ❌ JSON serialization library (use Unity's built-in `JsonUtility`)

### Recommendation
**No additional package dependencies required.** The project has everything needed:
- WebRTC via Byn.Awrtc (already integrated)
- HTTP via UnityWebRequest (built-in)
- JSON via JsonUtility (built-in)

---

## 5. Missing Integration Scripts Checklist

| Script | Location | Status | Priority | Notes |
|--------|----------|--------|----------|-------|
| `MiddlewareClient.cs` | Assets/Scripts/API/ | ❌ Missing | **High** | HTTP client for backend REST API |
| `CommandHandler.cs` | Assets/Scripts/DataChannel/ | ❌ Missing | **High** | Parses and dispatches WebRTC commands |
| `RecordingManager.cs` | Assets/Scripts/Recording/ | ❌ Missing | **High** | State machine orchestrator |
| `S3Uploader.cs` | Assets/Scripts/Upload/ | ❌ Missing | **Medium** | Pre-signed URL upload handling |
| `BackendConfig.cs` | Assets/Scripts/Config/ | ❌ Missing | **High** | Configuration management (URLs, auth) |
| `CommandMessage.cs` | Assets/Scripts/DataChannel/ | ❌ Missing | **High** | JSON serialization for commands |
| `RecordingState.cs` | Assets/Scripts/Recording/ | ❌ Missing | **Medium** | Enum and state definitions |

**Total:** 7 new scripts required (approximately 200-300 lines of code each)

---

## 6. Network Architecture

### Current WebRTC Setup
```
┌─────────────────────────────────────────────────────────┐
│                    Unity Client                         │
│  ┌─────────────────────────────────────────────────┐  │
│  │ CallApp / ChatApp (Byn.Awrtc)                   │  │
│  │ - WebSocket → wss://s.y-not.app/callapp         │  │
│  │ - DataChannel (TCP-like) for messages           │  │
│  │ - STUN/TURN via t.y-not.app                     │  │
│  └─────────────────────────────────────────────────┘  │
│                                                         │
│  ❌ No REST API client                                  │
│  ❌ No backend configuration                            │
└─────────────────────────────────────────────────────────┘
         ⬇
        Signaling Server (External)
```

### Required Backend Integration
```
┌──────────────────────────────────────────────────────────────┐
│                    Unity Client (Enhanced)                    │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ WebRTC Layer (Byn.Awrtc)                              │ │
│  │ - WebSocket → wss://backend/signaling                 │ │
│  │ - DataChannel (TCP) for robot commands                │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ NEW: REST API Client (UnityWebRequest)                │ │
│  │ - PATCH /api/records/{id}/state                       │ │
│  │ - POST /api/uploads/presigned-url                     │ │
│  │ - GET /api/config (for settings, optional)            │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ NEW: Command/State Management                         │ │
│  │ - CommandHandler (parses DataChannel JSON)            │ │
│  │ - RecordingManager (state machine)                    │ │
│  │ - S3Uploader (presigned URL upload)                   │ │
│  └────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
         ⬇ WebSocket + HTTPS
    ┌────────────────────────────────┐
    │  Backend Middleware             │
    │  - /signaling (WebSocket)       │
    │  - /api/records (REST)          │
    │  - /api/uploads (REST)          │
    └────────────────────────────────┘
         ⬇
    ┌────────────────────────────────┐
    │  Robot Control + S3 Storage     │
    └────────────────────────────────┘
```

---

## 7. Integration Workflow Example

### Hypothetical Recording Session Flow

```
1. User clicks "Start Scanning" in UI
   ↓
2. Unity → CommandHandler.OnScanningCommand()
   ↓
3. RecordingManager.StartScanning()
   ├─ Update local state: Idle → Scanning
   ├─ MiddlewareClient.PatchRecordState("rec123", "scanning")
   │  └─ PATCH /api/records/rec123/state {"state": "scanning"}
   ├─ Start camera feed capture
   └─ WebRTC → Send video frames via established connection
   ↓
4. [Backend processes scanning, eventually sends alignment command via DataChannel]
   ↓
5. CommandHandler receives: {"type": "command", "action": "alignPart", ...}
   ↓
6. RecordingManager.AlignPart()
   ├─ Update state: Scanning → Aligning
   ├─ PATCH /api/records/rec123/state {"state": "aligning"}
   └─ Align robot based on alignment parameters
   ↓
7. [User or backend triggers start recording]
   ↓
8. RecordingManager.StartRecording()
   ├─ Update state: Aligning → Recording
   ├─ PATCH /api/records/rec123/state {"state": "recording"}
   └─ Begin frame buffer accumulation
   ↓
9. [Recording continues...]
   ↓
10. RecordingManager.StopRecording()
    ├─ Update state: Recording → Uploading
    ├─ PATCH /api/records/rec123/state {"state": "uploading"}
    ├─ Buffer all frames: POST /api/uploads/presigned-url
    │  └─ Response: {"url": "https://s3.../bucket/rec123?sig=...", "headers": {...}}
    ├─ S3Uploader.UploadToPresignedUrl(presignedUrl, frameData)
    │  └─ PUT request to S3-hosted presigned URL
    └─ PATCH /api/records/rec123/state {"state": "complete"}
    ↓
11. UI shows "Recording complete"
```

---

## 8. Known Gaps & Risks

### Critical Issues
1. **No authentication/authorization** - WebRTC and HTTP calls lack any auth token/credential system
   - Need to add: JWT or API key support to both WebSocket signaling and REST calls
   - Impact: **SECURITY ISSUE** - anyone can control the robot

2. **No error handling in message parsing** - ChatApp just prints received messages
   - Need: Try-catch, validation, error logging in CommandHandler
   - Impact: Malformed messages could crash or cause undefined behavior

3. **No connection recovery** - If WebRTC disconnects, there's auto-rejoin (4 seconds) but no REST API retry
   - Need: Implement exponential backoff for REST calls
   - Impact: Upload failures could lose data

### Design Considerations
4. **State synchronization** - Unity maintains local state while backend also has record state
   - Risk: Race conditions if commands arrive out-of-order
   - Mitigation: Use version numbers or transaction IDs in state updates

5. **No message ID correlation** - DataChannel doesn't support request/response pattern
   - Issue: Can't match ACKs to specific commands
   - Workaround: Use reliable channel, assume all messages arrive

6. **JSON serialization overhead** - JsonUtility is simpler but slower than MessagePack
   - For high-frequency commands: Consider binary protocol instead
   - Current: Text/JSON is fine for low-frequency control commands

### Environment Configuration
7. **Hardcoded test server URLs** - ExampleGlobals points to public test servers
   - Need: Environment-specific configuration (Dev/Staging/Prod)
   - Implement: ScriptableObject or PlayerPrefs-based config

---

## 9. Integration Readiness Checklist

### Before Connecting to Backend

- [ ] Create `BackendConfig` class with configurable base URL
- [ ] Create `MiddlewareClient` with PATCH and POST implementations
- [ ] Create `CommandMessage` JSON serialization classes
- [ ] Create `CommandHandler` to parse and dispatch messages
- [ ] Create `RecordingManager` state machine
- [ ] Implement authentication tokens (JWT or API key) in both WebSocket and HTTP
- [ ] Add error handling and logging throughout
- [ ] Create unit tests for command parsing and state transitions
- [ ] Implement S3 presigned URL upload flow
- [ ] Test with mock backend server first
- [ ] Validate certificate validation on HTTPS
- [ ] Document backend API contract (expected request/response formats)

### Testing Strategy
1. **Unit tests:** Command parsing, state transitions
2. **Integration tests:** Mock backend server (Node.js/Python)
3. **End-to-end tests:** Real backend (staging environment)
4. **Load tests:** Verify upload performance with large frame buffers

---

## 10. Recommendations

### Immediate Next Steps (High Priority)

1. **Establish Backend API Contract**
   - Document exact REST endpoint specs (request body, response format, error codes)
   - Define DataChannel message protocol (JSON schema)
   - Specify authentication mechanism (JWT, API key, etc.)

2. **Create Integration Layer Scripts**
   - Start with `BackendConfig.cs` (configuration management)
   - Then `MiddlewareClient.cs` (REST client)
   - Then `CommandHandler.cs` (message parsing)
   - Implement with placeholders first, integrate after API spec is confirmed

3. **Implement Authentication**
   - Add JWT token management to MiddlewareClient
   - Add auth token to WebSocket connection request headers
   - Create token refresh mechanism

4. **Mock Server for Testing**
   - Create a simple Node.js/Python mock backend
   - Respond to PATCH and POST requests
   - Simulate DataChannel commands
   - Allows Unity development to proceed in parallel with backend

### Medium-Term Improvements

5. **Implement S3 Upload**
   - Implement `S3Uploader.cs` with presigned URL handling
   - Add multipart upload for large files (if needed)
   - Implement retry logic

6. **State Persistence**
   - Save `RecordingManager` state to file
   - Allow resume after app crash/restart

7. **Logging & Debugging**
   - Add comprehensive logging to all integration points
   - Create debug UI to inspect connection state
   - Log all WebRTC events

---

## Summary Table

| Component | Status | Impact | Notes |
|-----------|--------|--------|-------|
| WebRTC Signaling | ✅ Ready | Functional | Currently hardcoded to test server |
| DataChannel API | ✅ Ready | Functional | Works but needs JSON wrapper |
| REST HTTP Client | ❌ Missing | **Critical** | No implementation exists |
| Authentication | ❌ Missing | **Critical** | No auth system implemented |
| Command Parsing | ❌ Missing | **Critical** | Only basic string messages supported |
| State Management | ❌ Missing | **Critical** | No recording state machine |
| Configuration | ⚠️ Partial | **High** | Hardcoded URLs, need environment config |
| Error Handling | ⚠️ Poor | **High** | Minimal error handling in place |
| Package Dependencies | ✅ Good | Low | All required packages present |

**Overall Status:** ⚠️ **50% Ready** - WebRTC foundation exists, but backend integration layer completely missing.

---

## File Reference Summary

### Key Source Files
- `Assets/WebRtcVideoChat/callapp/CallApp.cs` - Main video call controller
- `Assets/WebRtcVideoChat/chatapp/ChatApp.cs` - Chat/messaging with DataChannel
- `Assets/WebRtcVideoChat/examples/ExampleGlobals.cs` - Configuration constants
- `Packages/manifest.json` - Unity package dependencies

### Missing Files (Need to Create)
- `Assets/Scripts/API/MiddlewareClient.cs` - HTTP REST client
- `Assets/Scripts/DataChannel/CommandHandler.cs` - Message dispatcher
- `Assets/Scripts/DataChannel/CommandMessage.cs` - JSON models
- `Assets/Scripts/Recording/RecordingManager.cs` - State machine
- `Assets/Scripts/Upload/S3Uploader.cs` - S3 upload handler
- `Assets/Scripts/Config/BackendConfig.cs` - Configuration manager

---

**Report Generated:** 2026-03-29
**Reviewer:** Unity Integration Agent (READ-ONLY Review)
