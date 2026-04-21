#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RobotMiddleware.Sensors;
using RobotMiddleware.Scanning;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class SensorValidationTests
    {
        // =====================================================================
        // Test 1: PointCloudExporter.ExportPLY rejects under MinPlyVertices
        // =====================================================================

        [Test]
        public void ExportPLY_BelowMinVertices_ThrowsInvalidOperationException()
        {
            var points = new List<Vector3>();
            var colors = new List<Color32>();

            // Add fewer points than MinPlyVertices
            for (int i = 0; i < PointCloudExporter.MinPlyVertices - 1; i++)
            {
                points.Add(new Vector3(i * 0.01f, 0f, 1f));
                colors.Add(new Color32(255, 255, 255, 255));
            }

            Assert.Throws<InvalidOperationException>(() =>
            {
                PointCloudExporter.ExportPLY(points, colors);
            });
        }

        [Test]
        public void ExportPLY_ExactlyMinVertices_Succeeds()
        {
            var points = new List<Vector3>();
            var colors = new List<Color32>();

            for (int i = 0; i < PointCloudExporter.MinPlyVertices; i++)
            {
                points.Add(new Vector3(i * 0.01f, 0f, 1f));
                colors.Add(new Color32(255, 255, 255, 255));
            }

            byte[] result = null;
            Assert.DoesNotThrow(() =>
            {
                result = PointCloudExporter.ExportPLY(points, colors);
            });
            Assert.IsNotNull(result);
            Assert.Greater(result.Length, 0);
        }

        [Test]
        public void ExportPLY_ZeroPoints_ThrowsInvalidOperationException()
        {
            var points = new List<Vector3>();
            var colors = new List<Color32>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                PointCloudExporter.ExportPLY(points, colors);
            });
        }

        // =====================================================================
        // Test 2: PointCloudExporter.DepthToPoints correctness
        // =====================================================================

        [Test]
        public void DepthToPoints_MaskedPixelsOnly_ReturnsCorrectCount()
        {
            int width = 4;
            int height = 4;
            int total = width * height;

            var depth = new ushort[total];
            var mask = new bool[total];

            // Set 3 pixels as masked with valid depth
            mask[0] = true; depth[0] = 1000;
            mask[5] = true; depth[5] = 2000;
            mask[10] = true; depth[10] = 3000;
            // All other mask entries are false, all other depths are 0

            var intrinsics = new DepthIntrinsics
            {
                fx = 100f, fy = 100f,
                cx = 2f, cy = 2f,
                width = width, height = height
            };

            var points = PointCloudExporter.DepthToPoints(depth, mask, intrinsics, 0.001f);
            Assert.AreEqual(3, points.Count);
        }

        [Test]
        public void DepthToPoints_ZeroDepthMasked_SkipsPixel()
        {
            int width = 2;
            int height = 2;
            int total = width * height;

            var depth = new ushort[total];
            var mask = new bool[total];

            mask[0] = true; depth[0] = 0; // zero depth, should be skipped
            mask[1] = true; depth[1] = 1000; // valid

            var intrinsics = new DepthIntrinsics
            {
                fx = 100f, fy = 100f,
                cx = 1f, cy = 1f,
                width = width, height = height
            };

            var points = PointCloudExporter.DepthToPoints(depth, mask, intrinsics, 0.001f);
            Assert.AreEqual(1, points.Count, "Zero-depth masked pixel should be skipped");
        }

        // =====================================================================
        // Test 3: MinPlyVertices constant value
        // =====================================================================

        [Test]
        public void MinPlyVertices_IsPositiveAndReasonable()
        {
            Assert.Greater(PointCloudExporter.MinPlyVertices, 0, "MinPlyVertices must be positive");
            Assert.AreEqual(1000, PointCloudExporter.MinPlyVertices, "MinPlyVertices expected to be 1000");
        }

        // =====================================================================
        // Test 4: PLY output format validation
        // =====================================================================

        [Test]
        public void ExportPLY_OutputContainsValidPLYHeader()
        {
            var points = new List<Vector3>();
            var colors = new List<Color32>();

            for (int i = 0; i < PointCloudExporter.MinPlyVertices; i++)
            {
                points.Add(new Vector3(i * 0.001f, i * 0.002f, 1f + i * 0.001f));
                colors.Add(new Color32(128, 64, 32, 255));
            }

            byte[] plyBytes = PointCloudExporter.ExportPLY(points, colors);
            string plyText = System.Text.Encoding.ASCII.GetString(plyBytes);

            Assert.IsTrue(plyText.StartsWith("ply"), "PLY must start with 'ply'");
            Assert.IsTrue(plyText.Contains("format ascii 1.0"), "PLY must specify ascii format");
            Assert.IsTrue(plyText.Contains($"element vertex {points.Count}"), "PLY header must declare correct vertex count");
            Assert.IsTrue(plyText.Contains("end_header"), "PLY must contain end_header");
        }

        // =====================================================================
        // Test 5: ScanState enum integrity
        // =====================================================================

        [Test]
        public void ScanState_Enum_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)ScanState.Idle);
            Assert.AreEqual(1, (int)ScanState.BackgroundCapture);
            Assert.AreEqual(2, (int)ScanState.Ready);
            Assert.AreEqual(3, (int)ScanState.Scanning);
            Assert.AreEqual(4, (int)ScanState.Preview);
            Assert.AreEqual(5, (int)ScanState.Confirmed);
        }

        // =====================================================================
        // Test 6: NaN/Inf filtering (validates Fix 6 logic pattern)
        // =====================================================================

        [Test]
        public void NaNInfFilter_RemovesBadPoints()
        {
            var points = new List<Vector3>
            {
                new Vector3(1f, 2f, 3f),
                new Vector3(float.NaN, 0f, 0f),
                new Vector3(0f, float.PositiveInfinity, 0f),
                new Vector3(0f, 0f, float.NegativeInfinity),
                new Vector3(4f, 5f, 6f)
            };

            // Apply the same RemoveAll pattern as ValidateScanGeometry
            points.RemoveAll(p =>
                float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

            Assert.AreEqual(2, points.Count, "Only finite points should survive");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), points[0]);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), points[1]);
        }

        // =====================================================================
        // Test 7: DepthIntrinsics struct default is zeroed
        // =====================================================================

        [Test]
        public void DepthIntrinsics_Default_IsZeroed()
        {
            var d = default(DepthIntrinsics);
            Assert.AreEqual(0, d.width);
            Assert.AreEqual(0, d.height);
            Assert.AreEqual(0f, d.fx);
            Assert.AreEqual(0f, d.fy);
            Assert.AreEqual(0f, d.cx);
            Assert.AreEqual(0f, d.cy);
        }
    }
}

#endif
