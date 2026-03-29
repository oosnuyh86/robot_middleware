import { env } from "../config/env.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ToolLocation {
  x: number; // pixel column
  y: number; // pixel row
  confidence: number; // 0-1
}

export interface PointPair {
  src: number[]; // [x, y, z] in source frame (e.g. camera)
  dst: number[]; // [x, y, z] in destination frame (e.g. Vive)
}

export interface TransformResult {
  transformMatrix: number[]; // row-major 4x4 (16 elements)
  error: number; // mean registration error
}

// ---------------------------------------------------------------------------
// 1. VLM tool location
// ---------------------------------------------------------------------------

export async function locateToolInImage(
  rgbImageBase64: string
): Promise<ToolLocation> {
  const ollamaUrl = env.OLLAMA_URL;

  const prompt =
    "In this image from an Intel RealSense D435i camera, locate the tip of the spray gun nozzle. " +
    "The spray gun has a Vive tracker mounted on it. " +
    'Return ONLY a JSON object with the pixel coordinates: {"x": <column>, "y": <row>, "confidence": <0-1>}';

  const body = JSON.stringify({
    model: "qwen3-vl:8b",
    prompt,
    images: [rgbImageBase64],
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
    return parseToolLocation(result.response);
  } catch (err: unknown) {
    if (err instanceof Error && err.name === "AbortError") {
      throw new Error("Ollama request timed out after 60 seconds");
    }
    throw err;
  } finally {
    clearTimeout(timeout);
  }
}

function parseToolLocation(responseText: string): ToolLocation {
  let jsonStr = responseText.trim();

  // Strip markdown fences
  const fenceMatch = jsonStr.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (fenceMatch) {
    jsonStr = fenceMatch[1].trim();
  }

  // Extract JSON object
  const braceStart = jsonStr.indexOf("{");
  const braceEnd = jsonStr.lastIndexOf("}");
  if (braceStart !== -1 && braceEnd !== -1) {
    jsonStr = jsonStr.slice(braceStart, braceEnd + 1);
  }

  const parsed = JSON.parse(jsonStr);

  const x = Number(parsed.x);
  const y = Number(parsed.y);
  if (!Number.isFinite(x) || !Number.isFinite(y)) {
    throw new Error("Invalid VLM response: x and y must be finite numbers");
  }

  const confidence = Math.max(0, Math.min(1, Number(parsed.confidence) || 0));
  return { x, y, confidence };
}

// ---------------------------------------------------------------------------
// 2. SVD-based rigid body transform  (Horn's method / Procrustes)
// ---------------------------------------------------------------------------

export function computeTransformSVD(pointPairs: PointPair[]): TransformResult {
  const n = pointPairs.length;
  if (n < 3) {
    throw new Error("At least 3 point pairs are required for SVD alignment");
  }

  // --- Compute centroids ---
  const cSrc = [0, 0, 0];
  const cDst = [0, 0, 0];
  for (const pp of pointPairs) {
    for (let i = 0; i < 3; i++) {
      cSrc[i] += pp.src[i];
      cDst[i] += pp.dst[i];
    }
  }
  for (let i = 0; i < 3; i++) {
    cSrc[i] /= n;
    cDst[i] /= n;
  }

  // --- Center the points ---
  const qSrc: number[][] = [];
  const qDst: number[][] = [];
  for (const pp of pointPairs) {
    qSrc.push([pp.src[0] - cSrc[0], pp.src[1] - cSrc[1], pp.src[2] - cSrc[2]]);
    qDst.push([pp.dst[0] - cDst[0], pp.dst[1] - cDst[1], pp.dst[2] - cDst[2]]);
  }

  // --- Build 3x3 cross-covariance matrix H = qSrc^T * qDst ---
  // H[i][j] = sum_k qSrc[k][i] * qDst[k][j]
  const H = [
    [0, 0, 0],
    [0, 0, 0],
    [0, 0, 0],
  ];
  for (let k = 0; k < n; k++) {
    for (let i = 0; i < 3; i++) {
      for (let j = 0; j < 3; j++) {
        H[i][j] += qSrc[k][i] * qDst[k][j];
      }
    }
  }

  // --- SVD of H ---
  const { U, S: _S, V } = svd3x3(H);

  // --- Rotation R = V * U^T ---
  // Check for reflection: if det(V * U^T) < 0, flip sign of last column of V
  const VUt = mat3Mul(V, mat3Transpose(U));
  const det = mat3Det(VUt);

  let R: number[][];
  if (det < 0) {
    // Flip last column of V
    const Vcorr = V.map((row) => [...row]);
    for (let i = 0; i < 3; i++) {
      Vcorr[i][2] = -Vcorr[i][2];
    }
    R = mat3Mul(Vcorr, mat3Transpose(U));
  } else {
    R = VUt;
  }

  // --- Translation t = cDst - R * cSrc ---
  const t = [0, 0, 0];
  for (let i = 0; i < 3; i++) {
    t[i] = cDst[i] - (R[i][0] * cSrc[0] + R[i][1] * cSrc[1] + R[i][2] * cSrc[2]);
  }

  // --- Build 4x4 homogeneous matrix (row-major) ---
  // [R t; 0 0 0 1]
  const transformMatrix = [
    R[0][0], R[0][1], R[0][2], t[0],
    R[1][0], R[1][1], R[1][2], t[1],
    R[2][0], R[2][1], R[2][2], t[2],
    0, 0, 0, 1,
  ];

  // --- Compute mean registration error ---
  let totalError = 0;
  for (const pp of pointPairs) {
    const sx = pp.src[0], sy = pp.src[1], sz = pp.src[2];
    const tx = R[0][0] * sx + R[0][1] * sy + R[0][2] * sz + t[0];
    const ty = R[1][0] * sx + R[1][1] * sy + R[1][2] * sz + t[1];
    const tz = R[2][0] * sx + R[2][1] * sy + R[2][2] * sz + t[2];
    const dx = tx - pp.dst[0];
    const dy = ty - pp.dst[1];
    const dz = tz - pp.dst[2];
    totalError += Math.sqrt(dx * dx + dy * dy + dz * dz);
  }
  const error = totalError / n;

  return { transformMatrix, error };
}

// ---------------------------------------------------------------------------
// 3. Verify alignment (kept from original, updated types)
// ---------------------------------------------------------------------------

export function verifyAlignment(
  transformMatrix: number[],
  testPoints: { src: { x: number; y: number; z: number }; dst: { x: number; y: number; z: number } }[]
): { meanError: number; maxError: number; perPointErrors: number[] } {
  const m = transformMatrix;

  const perPointErrors = testPoints.map((tp) => {
    const s = tp.src;
    const tx = m[0] * s.x + m[1] * s.y + m[2] * s.z + m[3];
    const ty = m[4] * s.x + m[5] * s.y + m[6] * s.z + m[7];
    const tz = m[8] * s.x + m[9] * s.y + m[10] * s.z + m[11];

    const dx = tx - tp.dst.x;
    const dy = ty - tp.dst.y;
    const dz = tz - tp.dst.z;
    return Math.sqrt(dx * dx + dy * dy + dz * dz);
  });

  const meanError =
    perPointErrors.reduce((sum, e) => sum + e, 0) / perPointErrors.length;
  const maxError = Math.max(...perPointErrors);

  return { meanError, maxError, perPointErrors };
}

// ===========================================================================
// 3x3 matrix helpers + Jacobi SVD
// ===========================================================================

function mat3Transpose(A: number[][]): number[][] {
  return [
    [A[0][0], A[1][0], A[2][0]],
    [A[0][1], A[1][1], A[2][1]],
    [A[0][2], A[1][2], A[2][2]],
  ];
}

function mat3Mul(A: number[][], B: number[][]): number[][] {
  const C = [
    [0, 0, 0],
    [0, 0, 0],
    [0, 0, 0],
  ];
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < 3; j++) {
      C[i][j] = A[i][0] * B[0][j] + A[i][1] * B[1][j] + A[i][2] * B[2][j];
    }
  }
  return C;
}

function mat3Det(A: number[][]): number {
  return (
    A[0][0] * (A[1][1] * A[2][2] - A[1][2] * A[2][1]) -
    A[0][1] * (A[1][0] * A[2][2] - A[1][2] * A[2][0]) +
    A[0][2] * (A[1][0] * A[2][1] - A[1][1] * A[2][0])
  );
}

function mat3Identity(): number[][] {
  return [
    [1, 0, 0],
    [0, 1, 0],
    [0, 0, 1],
  ];
}

/**
 * Jacobi SVD for a 3x3 matrix.
 * Decomposes A = U * diag(S) * V^T using iterative Givens rotations on A^T*A.
 * Sufficient accuracy for our 3x3 cross-covariance matrix.
 */
function svd3x3(A: number[][]): { U: number[][]; S: number[]; V: number[][] } {
  // Work on a copy
  let M = A.map((row) => [...row]);
  let V = mat3Identity();
  let U = mat3Identity();

  // Jacobi iterations on M^T * M to find V and singular values
  // We'll use one-sided Jacobi: repeatedly apply Givens rotations to M from the right
  const maxIter = 64;
  for (let iter = 0; iter < maxIter; iter++) {
    let converged = true;

    // Sweep through column pairs (p, q)
    for (const [p, q] of [[0, 1], [0, 2], [1, 2]] as [number, number][]) {
      // Compute 2x2 Gram sub-matrix of columns p and q
      let app = 0, apq = 0, aqq = 0;
      for (let i = 0; i < 3; i++) {
        app += M[i][p] * M[i][p];
        apq += M[i][p] * M[i][q];
        aqq += M[i][q] * M[i][q];
      }

      // If off-diagonal is negligible, skip
      if (Math.abs(apq) < 1e-12 * Math.sqrt(app * aqq + 1e-30)) {
        continue;
      }
      converged = false;

      // Compute Jacobi rotation angle
      const tau = (aqq - app) / (2 * apq);
      const t =
        Math.sign(tau) / (Math.abs(tau) + Math.sqrt(1 + tau * tau));
      const c = 1 / Math.sqrt(1 + t * t);
      const s = t * c;

      // Apply Givens rotation to M from the right: M' = M * G(p,q,theta)
      for (let i = 0; i < 3; i++) {
        const mp = M[i][p];
        const mq = M[i][q];
        M[i][p] = c * mp - s * mq;
        M[i][q] = s * mp + c * mq;
      }

      // Accumulate V: V' = V * G
      for (let i = 0; i < 3; i++) {
        const vp = V[i][p];
        const vq = V[i][q];
        V[i][p] = c * vp - s * vq;
        V[i][q] = s * vp + c * vq;
      }
    }

    if (converged) break;
  }

  // Now M = U * diag(S). Compute singular values as column norms of M.
  const S = [0, 0, 0];
  for (let j = 0; j < 3; j++) {
    let norm = 0;
    for (let i = 0; i < 3; i++) {
      norm += M[i][j] * M[i][j];
    }
    S[j] = Math.sqrt(norm);
  }

  // Compute U by normalizing columns of M
  U = [
    [0, 0, 0],
    [0, 0, 0],
    [0, 0, 0],
  ];
  for (let j = 0; j < 3; j++) {
    if (S[j] > 1e-15) {
      for (let i = 0; i < 3; i++) {
        U[i][j] = M[i][j] / S[j];
      }
    } else {
      // Degenerate singular value — assign arbitrary unit vector
      // For a 3x3 this means one dimension is collapsed
      U[j][j] = 1;
    }
  }

  return { U, S, V };
}
