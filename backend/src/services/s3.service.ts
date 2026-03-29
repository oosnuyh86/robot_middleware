import { S3Client, PutObjectCommand } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";
import { env } from "../config/env.js";

const s3 = new S3Client({
  region: env.AWS_REGION,
  credentials: {
    accessKeyId: env.AWS_ACCESS_KEY_ID,
    secretAccessKey: env.AWS_SECRET_ACCESS_KEY,
  },
});

export async function generatePresignedUpload(
  recordId: string,
  fileType: "obj" | "ply"
) {
  const key = `records/${recordId}/model.${fileType}`;

  const command = new PutObjectCommand({
    Bucket: env.S3_BUCKET,
    Key: key,
  });

  const uploadUrl = await getSignedUrl(s3, command, { expiresIn: 3600 });
  const publicUrl = `https://${env.S3_BUCKET}.s3.${env.AWS_REGION}.amazonaws.com/${key}`;

  return { uploadUrl, publicUrl, key };
}
