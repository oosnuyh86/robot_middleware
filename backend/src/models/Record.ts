import mongoose, { Schema, type InferSchemaType } from "mongoose";

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

/** Valid forward transitions (excludes FAILED, which is handled separately) */
const STATE_ORDER: RecordState[] = [
  "PENDING",
  "SCANNING",
  "ALIGNING",
  "RECORDING",
  "TRAINING",
  "VALIDATING",
  "EXECUTING",
  "COMPLETED",
];

export function isValidTransition(from: RecordState, to: RecordState): boolean {
  // Any state can transition to FAILED
  if (to === "FAILED") return from !== "FAILED" && from !== "COMPLETED";

  // Cannot transition out of FAILED or COMPLETED
  if (from === "FAILED" || from === "COMPLETED") return false;

  // Must be exactly one step forward
  const fromIndex = STATE_ORDER.indexOf(from);
  const toIndex = STATE_ORDER.indexOf(to);
  return toIndex === fromIndex + 1;
}

const subjectSchema = new Schema(
  {
    type: { $type: String, required: true, trim: true },
    "3d_model_url": { $type: String, default: "" },
  },
  { _id: false, typeKey: "$type" }
);

const recordSchema = new Schema(
  {
    state: {
      type: String,
      enum: RECORD_STATES,
      default: "PENDING",
      required: true,
    },
    professional_id: {
      type: Schema.Types.ObjectId,
      ref: "Professional",
      required: true,
    },
    subject: {
      type: subjectSchema,
      required: true,
    },
    sensors_used: {
      type: [String],
      required: true,
    },
    alignment_metadata: {
      type: Schema.Types.Mixed,
      default: null,
    },
    error_reason: {
      type: String,
      default: null,
    },
  },
  { timestamps: true }
);

export type IRecord = InferSchemaType<typeof recordSchema>;
export const Record = mongoose.model("Record", recordSchema);
