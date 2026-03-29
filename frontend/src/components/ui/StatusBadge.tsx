"use client";

import type { RecordState } from "@/types/models";

const stateColors: Record<RecordState, string> = {
  PENDING: "badge-gray",
  SCANNING: "badge-blue",
  ALIGNING: "badge-blue",
  RECORDING: "badge-yellow",
  TRAINING: "badge-purple",
  VALIDATING: "badge-purple",
  EXECUTING: "badge-orange",
  COMPLETED: "badge-green",
  FAILED: "badge-red",
};

interface StatusBadgeProps {
  state: RecordState;
}

export function StatusBadge({ state }: StatusBadgeProps) {
  return (
    <span className={`badge ${stateColors[state]}`}>
      {state}
    </span>
  );
}
