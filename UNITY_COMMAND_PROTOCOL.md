# Unity DataChannel Command Protocol Specification
**Based on:** UNITY_REVIEW_REPORT.md findings
**Purpose:** Map middleware commands to JSON messages over WebRTC DataChannel

---

## Current State
The Unity project uses **UTF-8 encoded text messages** over a reliable DataChannel.
- Implementation: `Assets/WebRtcVideoChat/chatapp/ChatApp.cs` (lines 350-407)
- Format: Encoded as `Encoding.UTF8.GetBytes(messageString)`
- Delivery: Reliable (TCP-like), ordered

## Required Implementation
Need to wrap commands in JSON structure for structured command dispatching.

---

## Command Mapping: Middleware → DataChannel

### Command Format (Proposed JSON Schema)

```json
{
  "id": "cmd-123",
  "type": "command",
  "action": "START_SCAN" | "ALIGN_SENSORS" | "START_RECORD" | "STOP" | "START_TRAINING" | "APPROVE_VALIDATION" | "START_EXECUTION" | "MARK_FAILED",
  "timestamp": 1711766400000,
  "payload": {
    // Command-specific parameters
  },
  "clientId": "unity-client-1"
}
```

---

## Command Definitions

### 1. START_SCAN
**Purpose:** Initiate scanning of the composite part
**Sender:** Backend → Unity
**Payload:**
```json
{
  "action": "START_SCAN",
  "payload": {
    "scanId": "scan-12345",
    "cameraSettings": {
      "resolution": "1920x1080",
      "frameRate": 30,
      "captureMode": "full"
    },
    "targetDuration": 30000
  }
}
```
**Unity Response:**
- ACK receipt
- State transition: `RecordingManager.Idle → Scanning`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "scanning"}`

---

### 2. ALIGN_SENSORS
**Purpose:** Execute sensor alignment before recording
**Sender:** Backend → Unity (after scanning complete)
**Payload:**
```json
{
  "action": "ALIGN_SENSORS",
  "payload": {
    "alignmentId": "align-12345",
    "alignmentData": {
      "scanReference": "scan-12345",
      "targetPoints": [[x, y, z], ...],
      "tolerance": 0.5,
      "method": "optical"
    }
  }
}
```
**Unity Response:**
- State transition: `Scanning → Aligning`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "aligning"}`
- Activate alignment UI/haptic feedback

---

### 3. START_RECORD
**Purpose:** Begin recording the actual painting session
**Sender:** Backend → Unity (after alignment complete) or User
**Payload:**
```json
{
  "action": "START_RECORD",
  "payload": {
    "recordingId": "rec-12345",
    "recordingSettings": {
      "targetQuality": "4K",
      "frameBufferSize": 1000,
      "compressionLevel": "medium"
    },
    "robotState": {
      "positionX": 0.0,
      "positionY": 0.0,
      "positionZ": 0.0,
      "orientation": [0, 0, 0, 1]
    }
  }
}
```
**Unity Response:**
- State transition: `Aligning → Recording`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "recording"}`
- Begin frame buffering

---

### 4. STOP
**Purpose:** Stop active operation (scanning, recording, training, etc.)
**Sender:** User or Backend
**Payload:**
```json
{
  "action": "STOP",
  "payload": {
    "reason": "user_request" | "timeout" | "error",
    "targetState": "idle" | "reviewing",
    "metadata": {
      "framesCollected": 500,
      "duration": 120000
    }
  }
}
```
**Unity Response:**
- State transition depends on current state
- If `Recording`: `Recording → Uploading`
- If `Scanning`: `Scanning → Idle`
- REST call: `PATCH /api/records/{id}/state` with corresponding state

---

### 5. START_TRAINING
**Purpose:** Initiate ML model training on collected data
**Sender:** Backend or User (post-upload)
**Payload:**
```json
{
  "action": "START_TRAINING",
  "payload": {
    "trainingJobId": "train-12345",
    "datasetReferences": {
      "recordingId": "rec-12345",
      "uploadUrl": "s3://bucket/rec-12345/",
      "frameCount": 500
    },
    "modelConfig": {
      "modelType": "composite-painter-v2",
      "epochs": 100,
      "batchSize": 32
    }
  }
}
```
**Unity Response:**
- State transition: `Recording → Training` (or equivalent)
- REST call: `PATCH /api/records/{id}/state` with `{"state": "training"}`
- UI shows training progress indicator

---

### 6. APPROVE_VALIDATION
**Purpose:** Approve the trained model for deployment
**Sender:** User (after reviewing validation results)
**Payload:**
```json
{
  "action": "APPROVE_VALIDATION",
  "payload": {
    "validationJobId": "val-12345",
    "approverNotes": "Model looks good, ready for deployment",
    "confidence": 0.95
  }
}
```
**Unity Response:**
- State transition: `Validating → Approved`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "approved"}`
- Enable execution button

---

### 7. START_EXECUTION
**Purpose:** Deploy and run the trained model in production
**Sender:** User (after approval)
**Payload:**
```json
{
  "action": "START_EXECUTION",
  "payload": {
    "executionJobId": "exec-12345",
    "modelVersion": "2.0",
    "targetParts": ["part-001", "part-002"],
    "executionSettings": {
      "speed": 1.0,
      "precision": "high",
      "rollback": true
    }
  }
}
```
**Unity Response:**
- State transition: `Approved → Executing`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "executing"}`
- Monitor robot execution in real-time

---

### 8. MARK_FAILED
**Purpose:** Mark a record/job as failed (error recovery)
**Sender:** Backend or User
**Payload:**
```json
{
  "action": "MARK_FAILED",
  "payload": {
    "failureReason": "scan_failed" | "alignment_timeout" | "upload_error" | "training_diverged",
    "errorDetails": {
      "code": "E001",
      "message": "Scan timeout after 60 seconds",
      "timestamp": 1711766400000
    },
    "recoverySuggestion": "Restart from scanning phase"
  }
}
```
**Unity Response:**
- State transition: Any state → `Failed`
- REST call: `PATCH /api/records/{id}/state` with `{"state": "failed"}`
- Display error to user with recovery options

---

## Implementation Requirements in Unity

### File: Assets/Scripts/DataChannel/CommandMessage.cs
```csharp
public enum CommandAction
{
    START_SCAN,
    ALIGN_SENSORS,
    START_RECORD,
    STOP,
    START_TRAINING,
    APPROVE_VALIDATION,
    START_EXECUTION,
    MARK_FAILED
}

[System.Serializable]
public class CommandMessage
{
    public string id;
    public string type;
    public CommandAction action;
    public long timestamp;
    public Dictionary<string, object> payload;
    public string clientId;

    // Serialization methods
    public string ToJson() { }
    public static CommandMessage FromJson(string json) { }
}
```

### File: Assets/Scripts/DataChannel/CommandHandler.cs
```csharp
public class CommandHandler : MonoBehaviour
{
    // Event handlers for each command type
    public event Action<CommandMessage> OnStartScan;
    public event Action<CommandMessage> OnAlignSensors;
    public event Action<CommandMessage> OnStartRecord;
    public event Action<CommandMessage> OnStop;
    public event Action<CommandMessage> OnStartTraining;
    public event Action<CommandMessage> OnApproveValidation;
    public event Action<CommandMessage> OnStartExecution;
    public event Action<CommandMessage> OnMarkFailed;

    public void HandleMessage(string jsonMessage)
    {
        // 1. Parse JSON → CommandMessage
        // 2. Validate required fields
        // 3. Dispatch to appropriate event
        // 4. Send ACK back (optional)
    }

    private void SendAck(string commandId) { }
    private void SendNack(string commandId, string errorReason) { }
}
```

### File: Assets/Scripts/Recording/RecordingManager.cs
State transitions needed:
```
Idle
├→ Scanning (START_SCAN)
│  └→ Scanning (STOP) → Idle
│  └→ Aligning (ALIGN_SENSORS)
│     └→ Aligning (STOP) → Idle
│     └→ Recording (START_RECORD)
│        ├→ Recording (STOP) → Uploading
│        └→ Recording (MARK_FAILED) → Failed
├→ Uploading (after STOP from Recording)
│  └→ Training (START_TRAINING)
│     ├→ Training (START_TRAINING) → Validating (after backend)
│     └→ Training (MARK_FAILED) → Failed
├→ Validating
│  ├→ Approved (APPROVE_VALIDATION)
│  │  └→ Executing (START_EXECUTION)
│  │     └→ Complete (after success)
│  └→ Failed (MARK_FAILED)
└→ Failed
   └→ Idle (user reset)
```

---

## DataChannel Message Flow Example

### Scenario: Complete Recording Session

```
1. Backend sends:
   → {"id": "cmd-1", "action": "START_SCAN", ...}

2. Unity CommandHandler receives, parses, dispatches OnStartScan
   → RecordingManager.StartScanning()
   → PATCH /api/records/rec-123/state {"state": "scanning"}
   → Unity sends ACK: {"id": "cmd-1", "status": "acknowledged"}

3. Backend sends:
   → {"id": "cmd-2", "action": "ALIGN_SENSORS", ...}

4. Unity processes alignment
   → RecordingManager.AlignSensors()
   → PATCH /api/records/rec-123/state {"state": "aligning"}

5. Backend sends:
   → {"id": "cmd-3", "action": "START_RECORD", ...}

6. Unity starts recording
   → RecordingManager.StartRecording()
   → PATCH /api/records/rec-123/state {"state": "recording"}
   → Begin frame buffering

7. [Recording session continues...]

8. Backend sends:
   → {"id": "cmd-4", "action": "STOP", "payload": {"reason": "user_request"}}

9. Unity stops and uploads
   → RecordingManager.StopRecording()
   → PATCH /api/records/rec-123/state {"state": "uploading"}
   → POST /api/uploads/presigned-url → S3 upload
   → PATCH /api/records/rec-123/state {"state": "training"}
```

---

## Technical Constraints

1. **No Built-in Request/Response Correlation**
   - DataChannel provides no message ID matching
   - ACKs are optional; use reliable delivery for guarantees
   - Backend should assume all messages arrive (ordered, reliable)

2. **Serialization Overhead**
   - JSON text is ~2-3x larger than binary
   - For 500 messages per session: ~100KB overhead (acceptable)
   - If performance critical, switch to MessagePack/ProtoBuf later

3. **Error Handling**
   - Malformed JSON should trigger `MARK_FAILED`
   - Unknown action should be logged, not crash
   - CommandHandler should be resilient to invalid payloads

4. **Concurrency**
   - Only one active state at a time (state machine enforces this)
   - No parallel scanning + recording
   - Queue mechanism for rapid consecutive commands

---

## Testing Checklist

- [ ] Parse all 8 command types from JSON
- [ ] Validate required payload fields per command
- [ ] Dispatch to correct event handler
- [ ] State transitions match diagram above
- [ ] REST API calls (`PATCH /state`) on each transition
- [ ] Error cases: malformed JSON, unknown action, invalid state transition
- [ ] Test with mock backend server
- [ ] Test with real backend once available

---

**Note:** This protocol specification should be finalized with the backend team to ensure alignment on:
- Payload structure for each command
- Error codes and failure reasons
- Timeout values and retry strategies
- ACK/NACK handling requirements
