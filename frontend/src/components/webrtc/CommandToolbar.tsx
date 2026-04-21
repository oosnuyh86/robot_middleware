"use client";

import { Button } from "@/components/ui/Button";
import type { RecordState } from "@/types/models";

/**
 * State transitions that can be triggered from the dashboard.
 * Maps current state to the next valid forward state.
 */
const NEXT_STATE: Partial<Record<RecordState, RecordState>> = {
  PENDING: "SCANNING",
  SCANNING: "ALIGNING",
  ALIGNING: "RECORDING",
  RECORDING: "TRAINING",
  TRAINING: "VALIDATING",
  VALIDATING: "EXECUTING",
  EXECUTING: "COMPLETED",
};

interface CommandToolbarProps {
  currentState: RecordState;
  onAdvanceState: (nextState: RecordState) => void;
  onFail: (reason: string) => void;
  onScanSubCommand?: (
    action: "CAPTURE_BACKGROUND" | "START_OBJECT_SCAN" | "CONFIRM_SCAN" | "RESCAN",
  ) => void;
  disabled?: boolean;
}

export function CommandToolbar({
  currentState,
  onAdvanceState,
  onFail,
  onScanSubCommand,
  disabled = false,
}: CommandToolbarProps) {
  const nextState = NEXT_STATE[currentState];
  const isTerminal = currentState === "COMPLETED" || currentState === "FAILED";

  const handleFail = () => {
    const reason = window.prompt("Enter failure reason:");
    if (reason) {
      onFail(reason);
    }
  };

  return (
    <div className="command-toolbar">
      <h3>Record Controls</h3>
      <p className="toolbar-state">
        Current: <strong>{currentState}</strong>
        {nextState && (
          <>
            {" "}
            &rarr; Next: <strong>{nextState}</strong>
          </>
        )}
      </p>
      <div className="toolbar-actions">
        {!isTerminal && nextState && (
          <Button
            onClick={() => onAdvanceState(nextState)}
            disabled={disabled}
          >
            Advance to {nextState}
          </Button>
        )}
        {!isTerminal && (
          <Button
            variant="danger"
            onClick={handleFail}
            disabled={disabled}
          >
            Mark Failed
          </Button>
        )}
        {isTerminal && (
          <p className="toolbar-terminal">
            Record is in terminal state: {currentState}
          </p>
        )}
      </div>
      {currentState === "SCANNING" && onScanSubCommand && (
        <div className="toolbar-actions">
          <Button
            onClick={() => onScanSubCommand("CAPTURE_BACKGROUND")}
            disabled={disabled}
          >
            Capture Background
          </Button>
          <Button
            onClick={() => onScanSubCommand("START_OBJECT_SCAN")}
            disabled={disabled}
          >
            Start Object Scan
          </Button>
          <Button
            onClick={() => onScanSubCommand("CONFIRM_SCAN")}
            disabled={disabled}
          >
            Confirm Scan
          </Button>
          <Button
            onClick={() => onScanSubCommand("RESCAN")}
            disabled={disabled}
          >
            Rescan
          </Button>
        </div>
      )}
    </div>
  );
}
