using System.Collections.Generic;
using UnityEngine;

namespace RobotMiddleware.ML
{
    /// <summary>
    /// Generates synthetic painting trajectories for ML demonstration recording.
    /// Produces back-and-forth raster sweeps on a defined work surface,
    /// simulating how a human painter would coat a flat panel.
    /// </summary>
    public class SyntheticTrajectoryGenerator : MonoBehaviour
    {
        [Header("Sweep Parameters")]
        [SerializeField] private float _sweepSpacing = 0.05f;    // 5cm between sweep lines
        [SerializeField] private float _standoffDistance = 0.15f; // 15cm from surface
        [SerializeField] private float _sweepSpeed = 0.1f;       // m/s during sweeps
        [SerializeField] private float _transitionSpeed = 0.15f;  // m/s during transitions

        [Header("Flow")]
        [SerializeField] private float _sprayFlowRate = 50f; // ml/min when spraying (0-100)

        [Header("Default Surface")]
        [SerializeField] private Vector3 _defaultSurfaceCenter = new Vector3(0f, 0.5f, 0.5f);
        [SerializeField] private Vector3 _defaultSurfaceNormal = new Vector3(0f, 0f, -1f);
        [SerializeField] private float _defaultWidth = 0.4f;
        [SerializeField] private float _defaultHeight = 0.3f;

        public float SweepSpacing { get => _sweepSpacing; set => _sweepSpacing = value; }
        public float StandoffDistance { get => _standoffDistance; set => _standoffDistance = value; }
        public float SweepSpeed { get => _sweepSpeed; set => _sweepSpeed = value; }
        public float SprayFlowRate { get => _sprayFlowRate; set => _sprayFlowRate = value; }

        /// <summary>
        /// Generate a trajectory using the default surface parameters configured in the inspector.
        /// </summary>
        public List<TrajectoryPoint> GenerateDefaultTrajectory()
        {
            return GenerateTrajectory(_defaultSurfaceCenter, _defaultSurfaceNormal,
                                      _defaultWidth, _defaultHeight);
        }

        /// <summary>
        /// Generate a raster-sweep painting trajectory on a defined surface.
        /// The tool sweeps horizontally, alternating direction on each line,
        /// with short transitions between sweep lines.
        /// </summary>
        public List<TrajectoryPoint> GenerateTrajectory(
            Vector3 surfaceCenter, Vector3 surfaceNormal, float width, float height)
        {
            var points = new List<TrajectoryPoint>();

            // Build a coordinate frame on the surface
            // Normal points away from the surface (toward the robot)
            Vector3 normal = surfaceNormal.normalized;

            // Choose a consistent "up" direction for the surface frame
            Vector3 worldUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? Vector3.forward
                : Vector3.up;

            Vector3 right = Vector3.Cross(worldUp, normal).normalized;  // Horizontal sweep direction
            Vector3 up = Vector3.Cross(normal, right).normalized;       // Vertical step direction

            // Tool orientation: Z-axis points into the surface (perpendicular)
            Quaternion toolRotation = Quaternion.LookRotation(-normal, up);

            // Standoff position: offset from surface along the normal
            Vector3 standoffOffset = normal * _standoffDistance;

            // Calculate sweep lines
            int numSweeps = Mathf.Max(1, Mathf.FloorToInt(height / _sweepSpacing) + 1);
            float actualHeight = (numSweeps - 1) * _sweepSpacing;

            // Start from the bottom of the surface
            Vector3 bottomLeft = surfaceCenter - right * (width / 2f) - up * (actualHeight / 2f);

            float currentTime = 0f;

            for (int i = 0; i < numSweeps; i++)
            {
                bool leftToRight = (i % 2 == 0);
                Vector3 lineBase = bottomLeft + up * (i * _sweepSpacing) + standoffOffset;

                Vector3 sweepStart = leftToRight ? lineBase : lineBase + right * width;
                Vector3 sweepEnd = leftToRight ? lineBase + right * width : lineBase;

                // Transition: move to sweep start (flow OFF)
                if (points.Count > 0)
                {
                    Vector3 lastPos = points[points.Count - 1].position;
                    float transitionDist = Vector3.Distance(lastPos, sweepStart);
                    float transitionTime = transitionDist / _transitionSpeed;

                    // Lift slightly during transition to avoid drips
                    Vector3 liftedMid = (lastPos + sweepStart) / 2f + normal * 0.02f;

                    // Midpoint of transition (lifted)
                    float halfTime = transitionTime / 2f;
                    currentTime += halfTime;
                    points.Add(new TrajectoryPoint
                    {
                        time = currentTime,
                        position = liftedMid,
                        rotation = toolRotation,
                        flowRate = 0f
                    });

                    // Arrive at sweep start
                    currentTime += halfTime;
                    points.Add(new TrajectoryPoint
                    {
                        time = currentTime,
                        position = sweepStart,
                        rotation = toolRotation,
                        flowRate = 0f
                    });
                }
                else
                {
                    // First point — start at sweep start
                    points.Add(new TrajectoryPoint
                    {
                        time = currentTime,
                        position = sweepStart,
                        rotation = toolRotation,
                        flowRate = 0f
                    });
                }

                // Sweep: move across the surface (flow ON)
                float sweepTime = width / _sweepSpeed;
                currentTime += sweepTime;
                points.Add(new TrajectoryPoint
                {
                    time = currentTime,
                    position = sweepEnd,
                    rotation = toolRotation,
                    flowRate = _sprayFlowRate
                });
            }

            // Final point: flow OFF
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                currentTime += 0.1f; // Brief pause
                points.Add(new TrajectoryPoint
                {
                    time = currentTime,
                    position = last.position,
                    rotation = last.rotation,
                    flowRate = 0f
                });
            }

            Debug.Log($"[SyntheticTrajectoryGenerator] Generated {points.Count} points, " +
                      $"{numSweeps} sweeps, total time: {currentTime:F1}s");

            return points;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the default work surface in the editor
            Vector3 normal = _defaultSurfaceNormal.normalized;
            Vector3 worldUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? Vector3.forward
                : Vector3.up;
            Vector3 right = Vector3.Cross(worldUp, normal).normalized;
            Vector3 up = Vector3.Cross(normal, right).normalized;

            // Draw surface rectangle
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 c = _defaultSurfaceCenter;
            float hw = _defaultWidth / 2f;
            float hh = _defaultHeight / 2f;

            Vector3 bl = c - right * hw - up * hh;
            Vector3 br = c + right * hw - up * hh;
            Vector3 tl = c - right * hw + up * hh;
            Vector3 tr = c + right * hw + up * hh;

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);

            // Draw normal
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(c, normal * _standoffDistance);
        }
    }

    /// <summary>
    /// A single point in a painting trajectory.
    /// </summary>
    [System.Serializable]
    public struct TrajectoryPoint
    {
        public float time;        // Seconds from trajectory start
        public Vector3 position;  // End-effector position in world space
        public Quaternion rotation; // End-effector orientation
        public float flowRate;    // Paint flow rate (0 = off, range 0-100 ml/min)
    }
}
