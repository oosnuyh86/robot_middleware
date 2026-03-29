"use client";

import { useState, useRef, useCallback, useEffect } from "react";
import { Button } from "@/components/ui/Button";
import {
  SignalingClient,
  type SignalingState,
} from "@/lib/webrtc/signalingClient";
import {
  RelayChannel,
  type CommandMessage,
  type DataChannelState,
} from "@/lib/webrtc/dataChannel";

const RELAY_URL =
  process.env.NEXT_PUBLIC_SIGNALING_URL || "ws://localhost:4000/ws";

export interface ConnectionPanelProps {
  /** Called when the data channel becomes available or unavailable. */
  onDataChannelReady?: (channel: RelayChannel | null) => void;
  /** Called when a message is received from Unity. */
  onMessage?: (message: CommandMessage) => void;
}

export function ConnectionPanel({ onDataChannelReady, onMessage }: ConnectionPanelProps) {
  const [state, setState] = useState<SignalingState>("disconnected");
  const [channelState, setChannelState] = useState<DataChannelState>("closed");
  const [error, setError] = useState<string | null>(null);
  const signalingRef = useRef<SignalingClient | null>(null);
  const channelRef = useRef<RelayChannel | null>(null);

  // Clean up on unmount
  useEffect(() => {
    return () => {
      channelRef.current?.close();
      signalingRef.current?.disconnect();
    };
  }, []);

  const connect = useCallback(() => {
    setError(null);

    const channel = new RelayChannel({
      onStateChange: (s) => {
        setChannelState(s);
        if (s === "open") {
          onDataChannelReady?.(channel);
        } else if (s === "closed") {
          onDataChannelReady?.(null);
        }
      },
      onMessage: (msg) => onMessage?.(msg),
      onError: (err) => setError(err),
    });
    channelRef.current = channel;

    const signaling = new SignalingClient({
      onStateChange: (s) => {
        setState(s);
        if (s === "connected") {
          channel.open((data) => signaling.send(data));
        } else if (s === "disconnected" || s === "error") {
          channel.close();
          onDataChannelReady?.(null);
        }
      },
      onError: (err) => setError(err),
      onMessage: (data) => channel.handleIncoming(data),
    });
    signalingRef.current = signaling;

    signaling.connect(RELAY_URL + "?role=dashboard");
  }, [onDataChannelReady, onMessage]);

  const disconnect = useCallback(() => {
    channelRef.current?.close();
    channelRef.current = null;
    signalingRef.current?.disconnect();
    signalingRef.current = null;
    onDataChannelReady?.(null);
    setError(null);
  }, [onDataChannelReady]);

  const stateLabel: Record<SignalingState, string> = {
    disconnected: "Disconnected",
    connecting: "Connecting...",
    connected: "Connected",
    error: "Error",
  };

  const stateClass: Record<SignalingState, string> = {
    disconnected: "status-gray",
    connecting: "status-yellow",
    connected: "status-green",
    error: "status-red",
  };

  const showConnect = state === "disconnected" || state === "error";

  return (
    <div className="connection-panel">
      <h3>Relay Connection</h3>
      <div className="connection-status">
        <span className={`status-dot ${stateClass[state]}`} />
        <span>{stateLabel[state]}</span>
        {state === "connected" && (
          <span className="channel-state">
            {" "}/ Data: {channelState}
          </span>
        )}
      </div>
      {error && <p className="error-text">{error}</p>}
      <div className="connection-actions">
        {showConnect ? (
          <Button onClick={connect}>Connect</Button>
        ) : state === "connecting" ? (
          <Button variant="secondary" loading>
            Connecting
          </Button>
        ) : (
          <Button variant="danger" onClick={disconnect}>
            Disconnect
          </Button>
        )}
      </div>
    </div>
  );
}
