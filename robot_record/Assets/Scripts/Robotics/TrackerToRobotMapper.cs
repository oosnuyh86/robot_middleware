using UnityEngine;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.Robotics
{
    /// <summary>
    /// Maps Vive tracker 6DoF pose to UR10e joint angles via the IK solver.
    /// Applies the alignment transform (from VLM calibration) to convert
    /// tracker coordinates to the robot base frame before solving IK.
    /// Includes a low-pass filter for smooth trajectory output.
    /// </summary>
    public class TrackerToRobotMapper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RobotArmController _robotController;
        [SerializeField] private ViveTrackerManager _trackerManager;

        [Header("Mapping")]
        [SerializeField] private bool _enableMapping = true;
        [SerializeField] private Vector3 _toolOffset = Vector3.zero; // Offset from tracker to spray gun tip
        [SerializeField] private float _filterCutoffHz = 5f; // Low-pass filter cutoff frequency

        [Header("Debug")]
        [SerializeField] private bool _showDebugGizmos = true;

        // Alignment transform (set by AlignmentManager after calibration)
        private Matrix4x4 _alignmentTransform = Matrix4x4.identity;
        private bool _hasAlignment;

        // Low-pass filter state
        private Vector3 _filteredPosition;
        private Quaternion _filteredRotation = Quaternion.identity;
        private bool _filterInitialized;

        // IK state
        private float[] _lastValidSolution;
        private int _ikFailures;

        /// <summary>
        /// Set the coordinate alignment transform from VLM calibration.
        /// This maps Vive tracker coordinates to the robot base frame.
        /// </summary>
        public void SetAlignmentTransform(Matrix4x4 transform)
        {
            _alignmentTransform = transform;
            _hasAlignment = true;
            Debug.Log("[TrackerToRobotMapper] Alignment transform set");
        }

        /// <summary>
        /// Get the last successfully computed joint angles.
        /// </summary>
        public float[] LastJointAngles => _lastValidSolution;

        /// <summary>
        /// Whether the mapper has a valid alignment and is actively controlling the robot.
        /// </summary>
        public bool IsActive => _enableMapping && _hasAlignment &&
                                _trackerManager != null && _trackerManager.IsTracking &&
                                _robotController != null && _robotController.IsInitialized;

        private void Update()
        {
            if (!_enableMapping || _trackerManager == null || _robotController == null)
                return;

            if (!_trackerManager.IsTracking)
                return;

            // Get tracker pose
            Vector3 trackerPos = _trackerManager.Position;
            Quaternion trackerRot = _trackerManager.Rotation;

            // Apply alignment transform (Vive space → Robot base space)
            if (_hasAlignment)
            {
                Vector4 pos4 = new Vector4(trackerPos.x, trackerPos.y, trackerPos.z, 1f);
                pos4 = _alignmentTransform * pos4;
                trackerPos = new Vector3(pos4.x, pos4.y, pos4.z);

                // Transform rotation
                Matrix4x4 rotMatrix = Matrix4x4.Rotate(trackerRot);
                Matrix4x4 alignedRot = _alignmentTransform * rotMatrix;
                trackerRot = alignedRot.rotation;
            }

            // Apply tool offset (tracker mount → spray gun tip)
            trackerPos += trackerRot * _toolOffset;

            // Low-pass filter for smooth trajectories
            ApplyFilter(ref trackerPos, ref trackerRot);

            // Solve IK and drive robot
            float[][] solutions = UR10eIKSolver.SolveIK(trackerPos, trackerRot);

            if (solutions.Length > 0)
            {
                float[] best = UR10eIKSolver.SelectBestSolution(solutions, _lastValidSolution);
                if (best != null)
                {
                    _lastValidSolution = best;
                    _robotController.SetJointTargets(best);
                    _ikFailures = 0;
                }
            }
            else
            {
                _ikFailures++;
                if (_ikFailures > 30) // ~0.5 seconds at 60fps
                {
                    Debug.LogWarning($"[TrackerToRobotMapper] IK solver failed for {_ikFailures} frames. " +
                                     $"Target: pos={trackerPos}, rot={trackerRot.eulerAngles}");
                }
                // Keep last valid solution — robot stays at last reachable position
            }
        }

        private void ApplyFilter(ref Vector3 position, ref Quaternion rotation)
        {
            if (!_filterInitialized)
            {
                _filteredPosition = position;
                _filteredRotation = rotation;
                _filterInitialized = true;
                return;
            }

            // Simple exponential moving average (first-order low-pass)
            float rc = 1f / (2f * Mathf.PI * _filterCutoffHz);
            float alpha = Time.deltaTime / (rc + Time.deltaTime);

            _filteredPosition = Vector3.Lerp(_filteredPosition, position, alpha);
            _filteredRotation = Quaternion.Slerp(_filteredRotation, rotation, alpha);

            position = _filteredPosition;
            rotation = _filteredRotation;
        }

        private void OnDrawGizmos()
        {
            if (!_showDebugGizmos || !Application.isPlaying) return;

            if (_trackerManager != null && _trackerManager.IsTracking)
            {
                // Draw tracker position
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_filteredPosition, 0.02f);

                // Draw tool direction
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(_filteredPosition, _filteredRotation * Vector3.forward * 0.1f);
            }
        }
    }
}
