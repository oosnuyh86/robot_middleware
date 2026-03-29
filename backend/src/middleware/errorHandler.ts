import type { Request, Response, NextFunction } from "express";

export function errorHandler(
  err: Error,
  _req: Request,
  res: Response,
  _next: NextFunction
) {
  console.error(`[Error] ${err.message}`);

  if (err.message.includes("not found")) {
    res.status(404).json({ error: err.message });
    return;
  }

  if (
    err.message.includes("Invalid") ||
    err.message.includes("required") ||
    err.name === "ValidationError"
  ) {
    res.status(400).json({ error: err.message });
    return;
  }

  res.status(500).json({ error: "Internal server error" });
}
