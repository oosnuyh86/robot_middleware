import type { Request, Response, NextFunction } from "express";
import * as recordService from "../services/record.service.js";
import type { RecordState } from "../models/Record.js";

function paramId(req: Request): string {
  const id = req.params.id;
  return Array.isArray(id) ? id[0] : id;
}

export async function list(req: Request, res: Response, next: NextFunction) {
  try {
    const filters = {
      state: req.query.state as RecordState | undefined,
      professional_id: req.query.professional_id as string | undefined,
    };
    const records = await recordService.findAll(filters);
    res.json(records);
  } catch (err) {
    next(err);
  }
}

export async function getById(req: Request, res: Response, next: NextFunction) {
  try {
    const record = await recordService.findById(paramId(req));
    if (!record) {
      res.status(404).json({ error: "Record not found" });
      return;
    }
    res.json(record);
  } catch (err) {
    next(err);
  }
}

export async function create(req: Request, res: Response, next: NextFunction) {
  try {
    const record = await recordService.create(req.body);
    res.status(201).json(record);
  } catch (err) {
    next(err);
  }
}

export async function update(req: Request, res: Response, next: NextFunction) {
  try {
    const record = await recordService.update(paramId(req), req.body);
    if (!record) {
      res.status(404).json({ error: "Record not found" });
      return;
    }
    res.json(record);
  } catch (err) {
    next(err);
  }
}

export async function transitionState(
  req: Request,
  res: Response,
  next: NextFunction
) {
  try {
    const { state, error_reason } = req.body;
    const record = await recordService.transitionState(
      paramId(req),
      state,
      error_reason
    );
    res.json(record);
  } catch (err) {
    next(err);
  }
}

export async function remove(req: Request, res: Response, next: NextFunction) {
  try {
    const record = await recordService.remove(paramId(req));
    if (!record) {
      res.status(404).json({ error: "Record not found" });
      return;
    }
    res.status(204).send();
  } catch (err) {
    next(err);
  }
}
