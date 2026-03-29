export interface Company {
  _id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface Professional {
  _id: string;
  name: string;
  profile_summary: string;
  company_id: string;
  createdAt: string;
  updatedAt: string;
}

export interface Job {
  _id: string;
  company_id: string;
  description: string;
  record_ids: string[];
  createdAt: string;
  updatedAt: string;
}

export const RECORD_STATES = [
  "PENDING",
  "SCANNING",
  "ALIGNING",
  "RECORDING",
  "TRAINING",
  "VALIDATING",
  "EXECUTING",
  "COMPLETED",
  "FAILED",
] as const;

export type RecordState = (typeof RECORD_STATES)[number];

export interface RecordSubject {
  type: string;
  "3d_model_url": string;
}

export interface RecordEntry {
  _id: string;
  state: RecordState;
  professional_id: string;
  subject: RecordSubject;
  sensors_used: string[];
  alignment_metadata: unknown;
  error_reason: string | null;
  createdAt: string;
  updatedAt: string;
}

export const SENSOR_CATALOG = [
  "Intel RealSense D435i",
  "HTC Vive Tracker",
  "Alicat Flow Meter",
] as const;

export type SensorName = (typeof SENSOR_CATALOG)[number];
