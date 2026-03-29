import { Professional } from "../models/Professional.js";

export async function findAll(companyId?: string) {
  const filter = companyId ? { company_id: companyId } : {};
  return Professional.find(filter).populate("company_id").sort({ createdAt: -1 });
}

export async function findById(id: string) {
  return Professional.findById(id).populate("company_id");
}

export async function create(data: {
  name: string;
  profile_summary?: string;
  company_id: string;
}) {
  return Professional.create(data);
}

export async function update(
  id: string,
  data: { name?: string; profile_summary?: string; company_id?: string }
) {
  return Professional.findByIdAndUpdate(id, data, {
    new: true,
    runValidators: true,
  });
}

export async function remove(id: string) {
  return Professional.findByIdAndDelete(id);
}
