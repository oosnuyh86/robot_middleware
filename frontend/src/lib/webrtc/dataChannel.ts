/**
 * Data channel abstractions for sending commands to the robot controller (Unity).
 *
 * Commands sent to Unity must match the `CommandMessage` C# class:
 * ```json
 * {
 *   "id": "<uuid>",
 *   "type": "COMMAND",
 *   "action": "START_SCAN" | "ALIGN_SENSORS" | "START_RECORD" | ... ,
 *   "timestamp": "2026-03-29T12:00:00.000Z",
 *   "payload": "<optional string>",
 *   "clientId": "<browser-generated id>"
 * }
 * ```
 *
 * Unity responds with ACK or ERROR messages using the same schema but with
 * `type` set to "ACK" or "ERROR".
 */

export type DataChannelState = "closed" | "connecting" | "open" | "closing";

/**
 * Command actions matching Unity's CommandAction enum.
 * These correspond 1:1 with the C# enum in CommandMessage.cs.
 */
export type CommandAction =
  | "START_SCAN"
  | "ALIGN_SENSORS"
  | "START_RECORD"
  | "STOP"
  | "START_TRAINING"
  | "START_VALIDATING"
  | "APPROVE_VALIDATION"
  | "START_EXECUTION"
  | "MARK_FAILED";

/**
 * Wire format matching Unity's CommandMessage class.
 */
export interface CommandMessage {
  id: string;
  type: "COMMAND" | "ACK" | "ERROR";
  action: string;
  timestamp: string;
  payload: string;
  clientId: string;
}

export interface RobotCommand {
  action: CommandAction;
  payload?: string;
}

export interface DataChannelEvents {
  onStateChange: (state: DataChannelState) => void;
  onMessage: (message: CommandMessage) => void;
  onError: (error: string) => void;
}

let clientId: string | null = null;

function getClientId(): string {
  if (!clientId) {
    clientId = crypto.randomUUID();
  }
  return clientId;
}

/** Build a CommandMessage from a RobotCommand, ready for JSON serialization. */
export function buildCommandMessage(command: RobotCommand): CommandMessage {
  return {
    id: crypto.randomUUID(),
    type: "COMMAND",
    action: command.action,
    timestamp: new Date().toISOString(),
    payload: command.payload ?? "",
    clientId: getClientId(),
  };
}

/** Try to parse an incoming JSON string as a CommandMessage. Returns null on failure. */
export function parseCommandMessage(data: string): CommandMessage | null {
  try {
    const msg = JSON.parse(data);
    if (msg && typeof msg.type === "string" && typeof msg.action === "string") {
      return msg as CommandMessage;
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * WebSocket-backed data channel that uses the relay server connection
 * as a transport for command messages.
 */
export class RelayChannel {
  private sendFn: ((data: string) => void) | null = null;
  private events: DataChannelEvents;
  private _state: DataChannelState = "closed";

  constructor(events: DataChannelEvents) {
    this.events = events;
  }

  get state(): DataChannelState {
    return this._state;
  }

  /**
   * Bind to a send function (typically signalingClient.send).
   * Call this when the relay WebSocket connects.
   */
  open(sendFn: (data: string) => void): void {
    this.sendFn = sendFn;
    this._state = "open";
    this.events.onStateChange("open");
  }

  /** Handle an incoming JSON message from the relay WebSocket. */
  handleIncoming(data: string): void {
    const msg = parseCommandMessage(data);
    if (msg) {
      this.events.onMessage(msg);
    }
  }

  close(): void {
    this.sendFn = null;
    this._state = "closed";
    this.events.onStateChange("closed");
  }

  sendCommand(command: RobotCommand): void {
    if (!this.sendFn || this._state !== "open") {
      this.events.onError("Data channel is not open");
      return;
    }
    const msg = buildCommandMessage(command);
    this.sendFn(JSON.stringify(msg));
  }
}
