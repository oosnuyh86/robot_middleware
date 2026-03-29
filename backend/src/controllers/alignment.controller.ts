import type { Request, Response, NextFunction } from "express";
import * as ollamaService from "../services/ollama.service.js";
import * as recordService from "../services/record.service.js";

export async function compute(req: Request, res: Response, next: NextFunction) {
  try {
    const { rgbImage, depthImage, calibrationPoints, recordId } = req.body;

    if (!rgbImage || !depthImage) {
      res.status(400).json({ error: "rgbImage and depthImage are required" });
      return;
    }

    if (!Array.isArray(calibrationPoints) || calibrationPoints.length < 3) {
      res
        .status(400)
        .json({ error: "At least 3 calibrationPoints are required" });
      return;
    }

    const result = await ollamaService.computeAlignment({
      rgbImage,
      depthImage,
      calibrationPoints,
    });

    // Store alignment metadata on the record if recordId is provided
    if (recordId) {
      await recordService.update(recordId, {
        alignment_metadata: {
          transformMatrix: result.transformMatrix,
          confidence: result.confidence,
          description: result.description,
          computed_at: new Date().toISOString(),
        },
      });
    }

    res.json(result);
  } catch (err) {
    next(err);
  }
}

export async function verify(req: Request, res: Response, next: NextFunction) {
  try {
    const { transformMatrix, testPoints } = req.body;

    if (!Array.isArray(transformMatrix) || transformMatrix.length !== 16) {
      res
        .status(400)
        .json({ error: "transformMatrix must be an array of 16 numbers" });
      return;
    }

    if (!Array.isArray(testPoints) || testPoints.length === 0) {
      res
        .status(400)
        .json({ error: "At least 1 testPoint is required" });
      return;
    }

    const result = ollamaService.verifyAlignment(transformMatrix, testPoints);
    res.json(result);
  } catch (err) {
    next(err);
  }
}
