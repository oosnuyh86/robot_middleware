import mongoose, { Schema, type InferSchemaType } from "mongoose";

const companySchema = new Schema(
  {
    name: { type: String, required: true, trim: true },
  },
  { timestamps: true }
);

export type ICompany = InferSchemaType<typeof companySchema>;
export const Company = mongoose.model("Company", companySchema);
