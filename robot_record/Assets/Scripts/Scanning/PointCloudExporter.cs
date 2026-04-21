using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.Scanning
{
    public static class PointCloudExporter
    {
        public const int MinPlyVertices = 1000;
        /// <summary>
        /// Converts masked depth pixels to 3D points using the pinhole camera model.
        /// Only pixels where mask[i] == true are projected.
        /// </summary>
        public static List<Vector3> DepthToPoints(ushort[] depthData, bool[] mask,
            DepthIntrinsics intrinsics, float depthScale)
        {
            var points = new List<Vector3>();
            int width = intrinsics.width;
            int height = intrinsics.height;

            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int idx = v * width + u;
                    if (!mask[idx])
                        continue;

                    ushort raw = depthData[idx];
                    if (raw == 0)
                        continue;

                    float z = raw * depthScale;
                    float x = (u - intrinsics.cx) * z / intrinsics.fx;
                    float y = (v - intrinsics.cy) * z / intrinsics.fy;

                    points.Add(new Vector3(x, y, z));
                }
            }

            return points;
        }

        /// <summary>
        /// Extracts Color32 values for masked pixels from the color texture.
        /// Returns colors in the same order as DepthToPoints would produce points.
        /// </summary>
        public static List<Color32> GetMaskedColors(Texture2D colorTexture, bool[] mask, int width, int height)
        {
            var colors = new List<Color32>();
            var pixels = colorTexture.GetPixels32();

            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int maskIdx = v * width + u;
                    if (!mask[maskIdx])
                        continue;

                    // Unity texture pixel order is bottom-up, depth is top-down
                    int texIdx = (height - 1 - v) * width + u;
                    colors.Add(pixels[texIdx]);
                }
            }

            return colors;
        }

        /// <summary>
        /// Generates an ASCII PLY file from the given points and colors.
        /// Colors list must match points list in length; if shorter, missing colors default to white.
        /// </summary>
        public static byte[] ExportPLY(List<Vector3> points, List<Color32> colors)
        {
            if (points.Count < MinPlyVertices)
                throw new InvalidOperationException(
                    $"[PointCloudExporter] Too few vertices ({points.Count}) for PLY export; minimum is {MinPlyVertices}");

            var sb = new StringBuilder();

            // PLY header
            sb.AppendLine("ply");
            sb.AppendLine("format ascii 1.0");
            sb.AppendLine($"element vertex {points.Count}");
            sb.AppendLine("property float x");
            sb.AppendLine("property float y");
            sb.AppendLine("property float z");
            sb.AppendLine("property uchar red");
            sb.AppendLine("property uchar green");
            sb.AppendLine("property uchar blue");
            sb.AppendLine("end_header");

            // Per-vertex data
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                Color32 c = i < colors.Count
                    ? colors[i]
                    : new Color32(255, 255, 255, 255);

                sb.AppendLine($"{p.x:F6} {p.y:F6} {p.z:F6} {c.r} {c.g} {c.b}");
            }

            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
