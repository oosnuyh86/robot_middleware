import { Job } from "../models/Job.js";

export async function findAll(companyId?: string) {
  const filter = companyId ? { company_id: companyId } : {};
  return Job.find(filter).populate("company_id").sort({ createdAt: -1 });
}

export async function findById(id: string) {
  return Job.findById(id).populate("company_id").populate("record_ids");
}

export async function create(data: {
  company_id: string;
  description: string;
}) {
  return Job.create(data);
}

export async function update(
  id: string,
  data: { description?: string; company_id?: string }
) {
  return Job.findByIdAndUpdate(id, data, { new: true, runValidators: true });
}

export async function addRecord(jobId: string, recordId: string) {
  return Job.findByIdAndUpdate(
    jobId,
    { $push: { record_ids: recordId } },
    { new: true }
  );
}

export async function remove(id: string) {
  return Job.findByIdAndDelete(id);
}
