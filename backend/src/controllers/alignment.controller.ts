import type { Request, Response, NextFunction } from "express";
import * as ollamaService from "../services/ollama.service.js";
import * as recordService from "../services/record.service.js";

export async function locateTool(req: Request, res: Response, next: NextFunction) {
  try {
    const { rgbImage } = req.body;

    if (!rgbImage || typeof rgbImage !== "string") {
      res.status(400).json({ error: "rgbImage (base64 string) is required" });
      return;
    }

    const result = await ollamaService.locateToolInImage(rgbImage);
    res.json(result);
  } catch (err) {
    next(err);
  }
}

export async function computeTransform(req: Request, res: Response, next: NextFunction) {
  try {
    const { pointPairs, recordId } = req.body;

    if (!Array.isArray(pointPairs) || pointPairs.length < 3) {
      res.status(400).json({ error: "At least 3 pointPairs are required" });
      return;
    }

    for (let i = 0; i < pointPairs.length; i++) {
      const pp = pointPairs[i];
      if (
        !Array.isArray(pp.src) || pp.src.length !== 3 ||
        !Array.isArray(pp.dst) || pp.dst.length !== 3
      ) {
        res.status(400).json({
          error: `pointPairs[${i}] must have src and dst arrays of length 3`,
        });
        return;
      }
    }

    const result = ollamaService.computeTransformSVD(pointPairs);

    if (recordId) {
      await recordService.update(recordId, {
        alignment_metadata: {
          transformMatrix: result.transformMatrix,
          error: result.error,
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
