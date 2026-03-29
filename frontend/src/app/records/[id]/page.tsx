"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, del, patch } from "@/lib/api";
import type { RecordEntry, RecordState } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Modal } from "@/components/ui/Modal";
import { ConnectionPanel } from "@/components/webrtc/ConnectionPanel";
import { CommandToolbar } from "@/components/webrtc/CommandToolbar";
import type { RelayChannel } from "@/lib/webrtc/dataChannel";
import type { CommandAction, CommandMessage } from "@/lib/webrtc/dataChannel";

/**
 * Maps a target RecordState to the CommandAction that triggers it on Unity.
 * Unity's MiddlewareController dispatches these to RecordingManager methods.
 */
const STATE_TO_COMMAND: Partial<Record<RecordState, CommandAction>> = {
  SCANNING: "START_SCAN",
  ALIGNING: "ALIGN_SENSORS",
  RECORDING: "START_RECORD",
  TRAINING: "START_TRAINING",
  VALIDATING: "START_VALIDATING",
  EXECUTING: "START_EXECUTION",
};

export default function RecordDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [record, setRecord] = useState<RecordEntry | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDelete, setShowDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const dataChannelRef = useRef<RelayChannel | null>(null);

  useEffect(() => {
    if (!id) return;
    get<RecordEntry>(`/records/${id}`)
      .then(setRecord)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load record"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  const handleDataChannelReady = useCallback(
    (channel: RelayChannel | null) => {
      dataChannelRef.current = channel;
    },
    [],
  );

  const handleUnityMessage = useCallback((msg: CommandMessage) => {
    if (msg.type === "ACK") {
      // Unity acknowledged our command - could update UI feedback here
      console.log(`[Relay] Unity ACK for ${msg.action} (${msg.id})`);
    } else if (msg.type === "ERROR") {
      setError(`Unity error: ${msg.payload || msg.action}`);
    }
  }, []);

  async function handleDelete() {
    setDeleting(true);
    try {
      await del(`/records/${id}`);
      router.push("/records");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete record");
      setDeleting(false);
      setShowDelete(false);
    }
  }

  const handleAdvanceState = useCallback(
    async (nextState: RecordState) => {
      setError(null);

      // Send command to Unity via DataChannel if connected
      const commandAction = STATE_TO_COMMAND[nextState];
      if (dataChannelRef.current && commandAction) {
        dataChannelRef.current.sendCommand({
          action: commandAction,
          payload: id,
        });
      }

      // Also update backend state via REST API
      try {
        const updated = await patch<RecordEntry>(`/records/${id}/state`, {
          state: nextState,
        });
        setRecord(updated);
      } catch (err) {
        setError(
          err instanceof Error ? err.message : "Failed to transition state",
        );
      }
    },
    [id],
  );

  const handleFail = useCallback(
    async (reason: string) => {
      setError(null);

      // Send MARK_FAILED command to Unity via DataChannel if connected
      if (dataChannelRef.current) {
        dataChannelRef.current.sendCommand({
          action: "MARK_FAILED",
          payload: reason,
        });
      }

      // Also update backend state via REST API
      try {
        const updated = await patch<RecordEntry>(`/records/${id}/state`, {
          state: "FAILED" as RecordState,
          error_reason: reason,
        });
        setRecord(updated);
      } catch (err) {
        setError(
          err instanceof Error ? err.message : "Failed to mark record as failed",
        );
      }
    },
    [id],
  );

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!record) {
    return (
      <div className="page-container">
        <div className="error-message">{error || "Record not found"}</div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="detail-header">
        <div>
          <h1>{record.subject.type}</h1>
          <StatusBadge state={record.state} />
        </div>
        <div className="detail-header-actions">
          <Button onClick={() => router.push(`/records/${id}/edit`)}>
            Edit
          </Button>
          <Button variant="danger" onClick={() => setShowDelete(true)}>
            Delete
          </Button>
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="detail-container">
        <div className="detail-grid">
          <div className="detail-group">
            <label>Record ID</label>
            <p>{record._id}</p>
          </div>
          <div className="detail-group">
            <label>State</label>
            <p><StatusBadge state={record.state} /></p>
          </div>
          <div className="detail-group">
            <label>Professional</label>
            <p>{typeof record.professional_id === "object" && record.professional_id !== null
              ? (record.professional_id as { name?: string }).name ?? record._id
              : record.professional_id}</p>
          </div>
          <div className="detail-group">
            <label>Subject Type</label>
            <p>{record.subject.type}</p>
          </div>
          <div className="detail-group">
            <label>3D Model</label>
            <p>
              {record.subject["3d_model_url"] ? (
                <a
                  href={record.subject["3d_model_url"]}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  View Model
                </a>
              ) : (
                "No model uploaded"
              )}
            </p>
          </div>
          <div className="detail-group">
            <label>Sensors Used</label>
            <div className="chip-list">
              {record.sensors_used.map((s) => (
                <span key={s} className="chip">{s}</span>
              ))}
            </div>
          </div>
          <div className="detail-group">
            <label>Created</label>
            <p>{new Date(record.createdAt).toLocaleString()}</p>
          </div>
          <div className="detail-group">
            <label>Updated</label>
            <p>{new Date(record.updatedAt).toLocaleString()}</p>
          </div>
          {record.error_reason && (
            <div className="detail-group">
              <label>Error Reason</label>
              <p className="error-text">{record.error_reason}</p>
            </div>
          )}
          {record.alignment_metadata != null && (
            <div className="detail-group">
              <label>Alignment Metadata</label>
              <pre>
                {JSON.stringify(record.alignment_metadata, null, 2)}
              </pre>
            </div>
          )}
        </div>

        <h2 className="section-heading">Robot Control</h2>
        <div className="webrtc-container">
          <ConnectionPanel
            onDataChannelReady={handleDataChannelReady}
            onMessage={handleUnityMessage}
          />
          <CommandToolbar
            currentState={record.state}
            onAdvanceState={handleAdvanceState}
            onFail={handleFail}
          />
        </div>

        <div className="form-actions">
          <Button variant="ghost" onClick={() => router.push("/records")}>
            Back to Records
          </Button>
        </div>
      </div>

      <Modal
        open={showDelete}
        title="Delete Record"
        onClose={() => setShowDelete(false)}
        onConfirm={handleDelete}
        confirmLabel="Delete"
        confirmVariant="danger"
        loading={deleting}
      >
        <p>
          Are you sure you want to delete this record? This action cannot be
          undone.
        </p>
      </Modal>
    </div>
  );
}
