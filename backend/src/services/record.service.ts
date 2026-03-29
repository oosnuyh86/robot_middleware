import { Record as RecordModel, isValidTransition, type RecordState } from "../models/Record.js";
import { validateSensors } from "../config/sensors.js";

export async function findAll(filters?: {
  state?: RecordState;
  professional_id?: string;
}) {
  const query: Record<string, unknown> = {};
  if (filters?.state) query.state = filters.state;
  if (filters?.professional_id) query.professional_id = filters.professional_id;
  return RecordModel.find(query).populate("professional_id").sort({ createdAt: -1 });
}

export async function findById(id: string) {
  return RecordModel.findById(id).populate("professional_id");
}

export async function create(data: {
  professional_id: string;
  subject: { type: string; "3d_model_url"?: string };
  sensors_used: string[];
}) {
  validateSensors(data.sensors_used);
  return RecordModel.create({ ...data, state: "PENDING" });
}

export async function update(
  id: string,
  data: {
    subject?: { type: string; "3d_model_url"?: string };
    sensors_used?: string[];
    alignment_metadata?: unknown;
  }
) {
  if (data.sensors_used) validateSensors(data.sensors_used);
  return RecordModel.findByIdAndUpdate(id, data, { new: true, runValidators: true });
}

export async function transitionState(
  id: string,
  newState: RecordState,
  errorReason?: string
) {
  const record = await RecordModel.findById(id);
  if (!record) throw new Error("Record not found");

  const currentState = record.state as RecordState;
  if (!isValidTransition(currentState, newState)) {
    throw new Error(
      `Invalid state transition: ${currentState} → ${newState}`
    );
  }

  record.state = newState;
  if (newState === "FAILED" && errorReason) {
    record.error_reason = errorReason;
  }
  return record.save();
}

export async function updateModelUrl(id: string, url: string) {
  return RecordModel.findByIdAndUpdate(
    id,
    { "subject.3d_model_url": url },
    { new: true }
  );
}

export async function remove(id: string) {
  return RecordModel.findByIdAndDelete(id);
}
