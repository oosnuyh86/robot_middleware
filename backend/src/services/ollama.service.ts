import { env } from "../config/env.js";

export interface CalibrationPoint {
  vivePose: {
    position: { x: number; y: number; z: number };
    rotation: { x: number; y: number; z: number; w: number };
  };
  realSensePoint: { x: number; y: number; z: number };
}

export interface AlignmentRequest {
  rgbImage: string;
  depthImage: string;
  calibrationPoints: CalibrationPoint[];
}

export interface AlignmentResult {
  transformMatrix: number[];
  confidence: number;
  description: string;
}

function buildPrompt(points: CalibrationPoint[]): string {
  const pointDescriptions = points
    .map(
      (p, i) =>
        `Point ${i + 1}:\n` +
        `  Vive position: (${p.vivePose.position.x.toFixed(4)}, ${p.vivePose.position.y.toFixed(4)}, ${p.vivePose.position.z.toFixed(4)})\n` +
        `  Vive rotation (quaternion xyzw): (${p.vivePose.rotation.x.toFixed(4)}, ${p.vivePose.rotation.y.toFixed(4)}, ${p.vivePose.rotation.z.toFixed(4)}, ${p.vivePose.rotation.w.toFixed(4)})\n` +
        `  RealSense 3D point: (${p.realSensePoint.x.toFixed(4)}, ${p.realSensePoint.y.toFixed(4)}, ${p.realSensePoint.z.toFixed(4)})`
    )
    .join("\n\n");

  return (
    `You are a spatial calibration assistant. Two sensor coordinate systems need to be aligned:\n` +
    `1. HTC Vive Tracker — provides 6DoF poses (position + quaternion rotation) in its own coordinate frame.\n` +
    `2. Intel RealSense D435i — provides RGB-D images and 3D points in its camera coordinate frame.\n\n` +
    `The attached RGB image shows the scene as captured by the RealSense camera. ` +
    `The depth image encodes per-pixel distance.\n\n` +
    `Below are calibration correspondences where the Vive tracker was placed at known positions ` +
    `visible to the RealSense camera:\n\n${pointDescriptions}\n\n` +
    `Analyze the spatial relationship between these two coordinate systems. ` +
    `Compute the 4x4 homogeneous transformation matrix that transforms points from ` +
    `the RealSense coordinate frame into the Vive coordinate frame.\n\n` +
    `Respond ONLY with a JSON object in this exact format (no markdown, no explanation outside the JSON):\n` +
    `{\n` +
    `  "transformMatrix": [m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33],\n` +
    `  "confidence": <float 0-1>,\n` +
    `  "description": "<brief explanation of the spatial relationship>"\n` +
    `}`
  );
}

function parseAlignmentResponse(responseText: string): AlignmentResult {
  // Extract JSON from the response, handling potential markdown wrapping
  let jsonStr = responseText.trim();

  const fenceMatch = jsonStr.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (fenceMatch) {
    jsonStr = fenceMatch[1].trim();
  }

  const braceStart = jsonStr.indexOf("{");
  const braceEnd = jsonStr.lastIndexOf("}");
  if (braceStart !== -1 && braceEnd !== -1) {
    jsonStr = jsonStr.slice(braceStart, braceEnd + 1);
  }

  const parsed = JSON.parse(jsonStr);

  if (
    !Array.isArray(parsed.transformMatrix) ||
    parsed.transformMatrix.length !== 16
  ) {
    throw new Error(
      "Invalid response: transformMatrix must be an array of 16 numbers"
    );
  }

  const matrix = parsed.transformMatrix.map((v: unknown) => {
    const n = Number(v);
    if (!Number.isFinite(n)) {
      throw new Error("Invalid response: transformMatrix contains non-finite values");
    }
    return n;
  });

  const confidence = Math.max(0, Math.min(1, Number(parsed.confidence) || 0));

  return {
    transformMatrix: matrix,
    confidence,
    description: String(parsed.description || ""),
  };
}

export async function computeAlignment(
  data: AlignmentRequest
): Promise<AlignmentResult> {
  const ollamaUrl = env.OLLAMA_URL;
  const prompt = buildPrompt(data.calibrationPoints);

  const images: string[] = [data.rgbImage];
  if (data.depthImage) {
    images.push(data.depthImage);
  }

  const body = JSON.stringify({
    model: "qwen3-vl:8b",
    prompt,
    images,
    stream: false,
  });

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 60_000);

  try {
    const response = await fetch(`${ollamaUrl}/api/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
      signal: controller.signal,
    });

    if (!response.ok) {
      const text = await response.text().catch(() => "");
      throw new Error(`Ollama request failed (${response.status}): ${text}`);
    }

    const result = (await response.json()) as { response: string };
    return parseAlignmentResponse(result.response);
  } catch (err: unknown) {
    if (err instanceof Error && err.name === "AbortError") {
      throw new Error("Ollama request timed out after 60 seconds");
    }
    throw err;
  } finally {
    clearTimeout(timeout);
  }
}

export function verifyAlignment(
  transformMatrix: number[],
  testPoints: { realSense: { x: number; y: number; z: number }; vive: { x: number; y: number; z: number } }[]
): { meanError: number; maxError: number; perPointErrors: number[] } {
  const m = transformMatrix;

  const perPointErrors = testPoints.map((tp) => {
    const rs = tp.realSense;
    // Apply 4x4 transform (row-major) to RealSense point
    const tx = m[0] * rs.x + m[1] * rs.y + m[2] * rs.z + m[3];
    const ty = m[4] * rs.x + m[5] * rs.y + m[6] * rs.z + m[7];
    const tz = m[8] * rs.x + m[9] * rs.y + m[10] * rs.z + m[11];

    const dx = tx - tp.vive.x;
    const dy = ty - tp.vive.y;
    const dz = tz - tp.vive.z;
    return Math.sqrt(dx * dx + dy * dy + dz * dz);
  });

  const meanError =
    perPointErrors.reduce((sum, e) => sum + e, 0) / perPointErrors.length;
  const maxError = Math.max(...perPointErrors);

  return { meanError, maxError, perPointErrors };
}
