/**
 * WebSocket client for the backend relay server.
 *
 * Connects to the relay at ws://localhost:4000/ws?role=dashboard and
 * sends/receives JSON CommandMessages directly.
 */

export type SignalingState = "disconnected" | "connecting" | "connected" | "error";

export interface SignalingEvents {
  onStateChange: (state: SignalingState) => void;
  onError: (error: string) => void;
  /** Called when a JSON message arrives from the relay. */
  onMessage?: (data: string) => void;
}

export class SignalingClient {
  private ws: WebSocket | null = null;
  private _state: SignalingState = "disconnected";
  private events: SignalingEvents;

  constructor(events: SignalingEvents) {
    this.events = events;
  }

  get state(): SignalingState {
    return this._state;
  }

  private setState(state: SignalingState) {
    this._state = state;
    this.events.onStateChange(state);
  }

  connect(url: string): void {
    if (this.ws) {
      this.ws.onopen = null;
      this.ws.onclose = null;
      this.ws.onerror = null;
      this.ws.onmessage = null;
      if (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING) {
        this.ws.close();
      }
      this.ws = null;
    }

    this.setState("connecting");

    try {
      this.ws = new WebSocket(url);

      this.ws.onopen = () => {
        this.setState("connected");
      };

      this.ws.onclose = () => {
        this.ws = null;
        this.setState("disconnected");
      };

      this.ws.onerror = () => {
        this.events.onError("WebSocket connection failed");
      };

      this.ws.onmessage = (event: MessageEvent) => {
        if (typeof event.data === "string") {
          this.events.onMessage?.(event.data);
        }
      };
    } catch {
      this.setState("error");
      this.events.onError("Failed to create WebSocket connection");
    }
  }

  disconnect(): void {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
    this.setState("disconnected");
  }

  /** Send a JSON string through the relay WebSocket. */
  send(data: string): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(data);
    }
  }
}
