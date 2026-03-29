import type { Request, Response, NextFunction } from "express";
import * as s3Service from "../services/s3.service.js";
import * as recordService from "../services/record.service.js";

export async function getPresignedUrl(
  req: Request,
  res: Response,
  next: NextFunction
) {
  try {
    const { recordId, fileType } = req.body;

    if (!recordId || !fileType) {
      res.status(400).json({ error: "recordId and fileType are required" });
      return;
    }

    if (fileType !== "obj" && fileType !== "ply") {
      res.status(400).json({ error: 'fileType must be "obj" or "ply"' });
      return;
    }

    const { uploadUrl, publicUrl } = await s3Service.generatePresignedUpload(
      recordId,
      fileType
    );

    // Update the record with the public URL
    await recordService.updateModelUrl(recordId, publicUrl);

    res.json({ uploadUrl, publicUrl });
  } catch (err) {
    next(err);
  }
}
