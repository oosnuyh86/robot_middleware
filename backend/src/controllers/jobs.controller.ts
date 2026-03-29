import type { Request, Response, NextFunction } from "express";
import * as jobService from "../services/job.service.js";

function paramId(req: Request): string {
  const id = req.params.id;
  return Array.isArray(id) ? id[0] : id;
}

export async function list(req: Request, res: Response, next: NextFunction) {
  try {
    const companyId = req.query.company_id as string | undefined;
    const jobs = await jobService.findAll(companyId);
    res.json(jobs);
  } catch (err) {
    next(err);
  }
}

export async function getById(req: Request, res: Response, next: NextFunction) {
  try {
    const job = await jobService.findById(paramId(req));
    if (!job) {
      res.status(404).json({ error: "Job not found" });
      return;
    }
    res.json(job);
  } catch (err) {
    next(err);
  }
}

export async function create(req: Request, res: Response, next: NextFunction) {
  try {
    const job = await jobService.create(req.body);
    res.status(201).json(job);
  } catch (err) {
    next(err);
  }
}

export async function update(req: Request, res: Response, next: NextFunction) {
  try {
    const job = await jobService.update(paramId(req), req.body);
    if (!job) {
      res.status(404).json({ error: "Job not found" });
      return;
    }
    res.json(job);
  } catch (err) {
    next(err);
  }
}

export async function addRecord(req: Request, res: Response, next: NextFunction) {
  try {
    const job = await jobService.addRecord(paramId(req), req.body.record_id);
    if (!job) {
      res.status(404).json({ error: "Job not found" });
      return;
    }
    res.json(job);
  } catch (err) {
    next(err);
  }
}

export async function remove(req: Request, res: Response, next: NextFunction) {
  try {
    const job = await jobService.remove(paramId(req));
    if (!job) {
      res.status(404).json({ error: "Job not found" });
      return;
    }
    res.status(204).send();
  } catch (err) {
    next(err);
  }
}
