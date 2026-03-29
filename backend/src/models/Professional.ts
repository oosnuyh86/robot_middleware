import mongoose, { Schema, type InferSchemaType } from "mongoose";

const professionalSchema = new Schema(
  {
    name: { type: String, required: true, trim: true },
    profile_summary: { type: String, default: "" },
    company_id: {
      type: Schema.Types.ObjectId,
      ref: "Company",
      required: true,
    },
  },
  { timestamps: true }
);

export type IProfessional = InferSchemaType<typeof professionalSchema>;
export const Professional = mongoose.model("Professional", professionalSchema);
