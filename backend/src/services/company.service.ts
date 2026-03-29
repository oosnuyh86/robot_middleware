import { Company } from "../models/Company.js";

export async function findAll() {
  return Company.find().sort({ createdAt: -1 });
}

export async function findById(id: string) {
  return Company.findById(id);
}

export async function create(data: { name: string }) {
  return Company.create(data);
}

export async function update(id: string, data: { name?: string }) {
  return Company.findByIdAndUpdate(id, data, { new: true, runValidators: true });
}

export async function remove(id: string) {
  return Company.findByIdAndDelete(id);
}
