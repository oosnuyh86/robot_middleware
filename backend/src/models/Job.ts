import mongoose, { Schema, type InferSchemaType } from "mongoose";

const jobSchema = new Schema(
  {
    company_id: {
      type: Schema.Types.ObjectId,
      ref: "Company",
      required: true,
    },
    description: { type: String, required: true, trim: true },
    record_ids: [{ type: Schema.Types.ObjectId, ref: "Record" }],
  },
  { timestamps: true }
);

export type IJob = InferSchemaType<typeof jobSchema>;
export const Job = mongoose.model("Job", jobSchema);
