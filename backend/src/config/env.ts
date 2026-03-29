import "dotenv/config";

function required(key: string): string {
  const value = process.env[key];
  if (!value) {
    throw new Error(`Missing required environment variable: ${key}`);
  }
  return value;
}

export const env = {
  PORT: parseInt(process.env.PORT || "4000", 10),
  MONGO_URI: required("MONGO_URI"),
  SIGNALING_PATH: process.env.SIGNALING_PATH || "/signaling",

  AWS_REGION: required("AWS_REGION"),
  AWS_ACCESS_KEY_ID: required("AWS_ACCESS_KEY_ID"),
  AWS_SECRET_ACCESS_KEY: required("AWS_SECRET_ACCESS_KEY"),
  S3_BUCKET: required("S3_BUCKET"),

  OLLAMA_URL: process.env.OLLAMA_URL || "http://localhost:11434",
} as const;
