using System;
using System.Collections.Generic;
using UnityEngine;
using RobotMiddleware.Robotics;

namespace RobotMiddleware.ML
{
    /// <summary>
    /// Plays back a trajectory and drives the robot arm + ML agent,
    /// allowing DemonstrationRecorder to capture .demo files
    /// without real hardware (Vive tracker / flow meter).
    ///
    /// Workflow:
    ///   1. SyntheticTrajectoryGenerator produces List of TrajectoryPoint
    ///   2. TrajectoryPlayer.PlayTrajectory() starts playback
    ///   3. Each FixedUpdate: interpolate position, drive robot via IK, set agent target
    ///   4. DemonstrationRecorder (managed by DemonstrationManager) captures the episode
    ///   5. OnTrajectoryComplete fires when done
    /// </summary>
    public class TrajectoryPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RobotArmAgent _agent;
        [SerializeField] private RobotArmController _robotController;
        [SerializeField] private SyntheticTrajectoryGenerator _trajectoryGenerator;
        [SerializeField] private DemonstrationManager _demonstrationManager;

        [Header("Playback")]
        [SerializeField] private bool _autoPlayOnStart;
        [SerializeField] private float _playbackSpeed = 1f;

        private List<TrajectoryPoint> _trajectory;
        private float _playbackTime;
        private int _currentSegment;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        public event Action OnTrajectoryComplete;

        private void Start()
        {
            if (_agent == null)
                _agent = GetComponentInParent<RobotArmAgent>();
            if (_robotController == null)
                _robotController = GetComponentInParent<RobotArmController>();
            if (_trajectoryGenerator == null)
                _trajectoryGenerator = GetComponent<SyntheticTrajectoryGenerator>();
            if (_demonstrationManager == null)
                _demonstrationManager = GetComponentInParent<DemonstrationManager>();

            if (_autoPlayOnStart)
            {
                var traj = _trajectoryGenerator.GenerateDefaultTrajectory();
                PlayTrajectory(traj);
            }
        }

        /// <summary>
        /// Begin playing a trajectory. Optionally starts demo recording.
        /// </summary>
        public void PlayTrajectory(List<TrajectoryPoint> trajectory)
        {
            if (trajectory == null || trajectory.Count < 2)
            {
                Debug.LogError("[TrajectoryPlayer] Trajectory must have at least 2 points");
                return;
            }

            _trajectory = trajectory;
            _playbackTime = 0f;
            _currentSegment = 0;
            _isPlaying = true;

            // Start demo recording if DemonstrationManager is available
            if (_demonstrationManager != null && !_demonstrationManager.IsRecording)
            {
                _demonstrationManager.StartDemoRecording();
            }

            // Move robot to start position immediately
            var first = _trajectory[0];
            if (_robotController != null)
                _robotController.MoveToTarget(first.position, first.rotation);
            if (_agent != null)
                _agent.SetTarget(first.position, first.rotation, first.flowRate);

            Debug.Log($"[TrajectoryPlayer] Playback started — {_trajectory.Count} points, " +
                      $"duration: {_trajectory[_trajectory.Count - 1].time:F1}s");
        }

        private void FixedUpdate()
        {
            if (!_isPlaying || _trajectory == null) return;

            _playbackTime += Time.fixedDeltaTime * _playbackSpeed;

            // Find the current segment (pair of points we're interpolating between)
            while (_currentSegment < _trajectory.Count - 1 &&
                   _playbackTime >= _trajectory[_currentSegment + 1].time)
            {
                _currentSegment++;
            }

            // Check if playback is complete
            if (_currentSegment >= _trajectory.Count - 1)
            {
                CompletePlayback();
                return;
            }

            // Interpolate between current and next point
            var from = _trajectory[_currentSegment];
            var to = _trajectory[_currentSegment + 1];

            float segmentDuration = to.time - from.time;
            float t = segmentDuration > 0f
                ? Mathf.Clamp01((_playbackTime - from.time) / segmentDuration)
                : 1f;

            Vector3 pos = Vector3.Lerp(from.position, to.position, t);
            Quaternion rot = Quaternion.Slerp(from.rotation, to.rotation, t);
            float flow = Mathf.Lerp(from.flowRate, to.flowRate, t);

            // Drive the robot arm via IK (bypasses Vive tracker)
            if (_robotController != null)
                _robotController.MoveToTarget(pos, rot);

            // Set agent target observations (for DemonstrationRecorder to capture)
            if (_agent != null)
                _agent.SetTarget(pos, rot, flow);
        }

        private void CompletePlayback()
        {
            _isPlaying = false;

            // Apply the final point
            var last = _trajectory[_trajectory.Count - 1];
            if (_robotController != null)
                _robotController.MoveToTarget(last.position, last.rotation);
            if (_agent != null)
                _agent.SetTarget(last.position, last.rotation, last.flowRate);

            // Stop demo recording
            if (_demonstrationManager != null && _demonstrationManager.IsRecording)
            {
                _demonstrationManager.StopDemoRecording();
            }

            Debug.Log("[TrajectoryPlayer] Playback complete");
            OnTrajectoryComplete?.Invoke();
        }

        /// <summary>
        /// Stop playback early.
        /// </summary>
        public void StopPlayback()
        {
            if (!_isPlaying) return;

            _isPlaying = false;

            if (_demonstrationManager != null && _demonstrationManager.IsRecording)
                _demonstrationManager.StopDemoRecording();

            Debug.Log("[TrajectoryPlayer] Playback stopped manually");
        }
    }
}
