import type { Request, Response, NextFunction } from "express";
import * as professionalService from "../services/professional.service.js";

function paramId(req: Request): string {
  const id = req.params.id;
  return Array.isArray(id) ? id[0] : id;
}

export async function list(req: Request, res: Response, next: NextFunction) {
  try {
    const companyId = req.query.company_id as string | undefined;
    const professionals = await professionalService.findAll(companyId);
    res.json(professionals);
  } catch (err) {
    next(err);
  }
}

export async function getById(req: Request, res: Response, next: NextFunction) {
  try {
    const professional = await professionalService.findById(paramId(req));
    if (!professional) {
      res.status(404).json({ error: "Professional not found" });
      return;
    }
    res.json(professional);
  } catch (err) {
    next(err);
  }
}

export async function create(req: Request, res: Response, next: NextFunction) {
  try {
    const professional = await professionalService.create(req.body);
    res.status(201).json(professional);
  } catch (err) {
    next(err);
  }
}

export async function update(req: Request, res: Response, next: NextFunction) {
  try {
    const professional = await professionalService.update(paramId(req), req.body);
    if (!professional) {
      res.status(404).json({ error: "Professional not found" });
      return;
    }
    res.json(professional);
  } catch (err) {
    next(err);
  }
}

export async function remove(req: Request, res: Response, next: NextFunction) {
  try {
    const professional = await professionalService.remove(paramId(req));
    if (!professional) {
      res.status(404).json({ error: "Professional not found" });
      return;
    }
    res.status(204).send();
  } catch (err) {
    next(err);
  }
}
