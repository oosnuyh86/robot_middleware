import type { Request, Response, NextFunction } from "express";
import * as companyService from "../services/company.service.js";

function paramId(req: Request): string {
  const id = req.params.id;
  return Array.isArray(id) ? id[0] : id;
}

export async function list(_req: Request, res: Response, next: NextFunction) {
  try {
    const companies = await companyService.findAll();
    res.json(companies);
  } catch (err) {
    next(err);
  }
}

export async function getById(req: Request, res: Response, next: NextFunction) {
  try {
    const company = await companyService.findById(paramId(req));
    if (!company) {
      res.status(404).json({ error: "Company not found" });
      return;
    }
    res.json(company);
  } catch (err) {
    next(err);
  }
}

export async function create(req: Request, res: Response, next: NextFunction) {
  try {
    const company = await companyService.create(req.body);
    res.status(201).json(company);
  } catch (err) {
    next(err);
  }
}

export async function update(req: Request, res: Response, next: NextFunction) {
  try {
    const company = await companyService.update(paramId(req), req.body);
    if (!company) {
      res.status(404).json({ error: "Company not found" });
      return;
    }
    res.json(company);
  } catch (err) {
    next(err);
  }
}

export async function remove(req: Request, res: Response, next: NextFunction) {
  try {
    const company = await companyService.remove(paramId(req));
    if (!company) {
      res.status(404).json({ error: "Company not found" });
      return;
    }
    res.status(204).send();
  } catch (err) {
    next(err);
  }
}
