using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using RobotMiddleware.Robotics;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.ML
{
    /// <summary>
    /// ML-Agents Agent for the UR10e spray painting task.
    /// Learns to replicate 6DoF trajectories + flow timing from human demonstrations.
    ///
    /// Observations (21 continuous):
    ///   - 6 current joint angles
    ///   - 3 end-effector position (x,y,z)
    ///   - 4 end-effector rotation (quaternion)
    ///   - 3 target position
    ///   - 4 target rotation
    ///   - 1 flow rate
    ///
    /// Actions (7 continuous):
    ///   - 6 joint angle deltas (clamped)
    ///   - 1 flow trigger (0-1)
    ///
    /// Training modes:
    ///   - Heuristic: Vive tracker → IK solver → joint angles (for demo recording)
    ///   - Inference: Neural network outputs joint deltas (trained model)
    /// </summary>
    public class RobotArmAgent : Agent
    {
        [Header("References")]
        [SerializeField] private RobotArmController _robotController;
        [SerializeField] private TrackerToRobotMapper _trackerMapper;
        [SerializeField] private FlowMeterManager _flowMeterManager;

        [Header("Agent Config")]
        [SerializeField] private float _maxJointDelta = 0.1f; // Max joint change per step (radians)
        [SerializeField] private float _positionRewardScale = 1f;
        [SerializeField] private float _orientationRewardScale = 0.5f;
        [SerializeField] private float _smoothnessRewardScale = 0.1f;
        [SerializeField] private float _flowRewardScale = 0.3f;

        // Target trajectory (set externally by DemonstrationManager or replayed from recording)
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private float _targetFlowRate;

        // Previous state for smoothness reward
        private float[] _previousActions;

        /// <summary>
        /// Set the target pose for the agent to track.
        /// Called by DemonstrationManager during training/playback.
        /// </summary>
        public void SetTarget(Vector3 position, Quaternion rotation, float flowRate = 0f)
        {
            _targetPosition = position;
            _targetRotation = rotation;
            _targetFlowRate = flowRate;
        }

        public override void Initialize()
        {
            _previousActions = new float[7];

            if (_robotController == null)
                _robotController = GetComponentInParent<RobotArmController>();
            if (_trackerMapper == null)
                _trackerMapper = GetComponentInParent<TrackerToRobotMapper>();
            if (_flowMeterManager == null)
                _flowMeterManager = FindAnyObjectByType<FlowMeterManager>();
        }

        public override void OnEpisodeBegin()
        {
            // Reset robot to home position
            _robotController?.SetJointTargetsImmediate(new float[6]);
            _previousActions = new float[7];
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Current joint angles (6)
            float[] joints = _robotController != null
                ? _robotController.CurrentJointAngles
                : new float[6];
            foreach (float j in joints)
                sensor.AddObservation(j);

            // Current end-effector pose (7: pos xyz + rot quaternion)
            if (_robotController != null)
            {
                Matrix4x4 eePose = _robotController.GetEndEffectorPose();
                sensor.AddObservation(eePose.GetPosition());
                sensor.AddObservation(eePose.rotation);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(Quaternion.identity);
            }

            // Target pose (7: pos xyz + rot quaternion)
            sensor.AddObservation(_targetPosition);
            sensor.AddObservation(_targetRotation);

            // Flow rate (1)
            float flowRate = _flowMeterManager != null ? _flowMeterManager.FlowRate : 0f;
            sensor.AddObservation(flowRate / 100f); // Normalize to 0-1
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (_robotController == null || !_robotController.IsInitialized) return;

            var continuousActions = actions.ContinuousActions;

            // Apply joint angle deltas (actions 0-5)
            float[] currentJoints = _robotController.CurrentJointAngles;
            float[] newTargets = new float[6];
            for (int i = 0; i < 6; i++)
            {
                float delta = Mathf.Clamp(continuousActions[i], -1f, 1f) * _maxJointDelta;
                newTargets[i] = currentJoints[i] + delta;
            }
            _robotController.SetJointTargets(newTargets);

            // Flow trigger (action 6) — 0 to 1
            float flowTrigger = Mathf.Clamp01((continuousActions[6] + 1f) / 2f);

            // Compute rewards
            ComputeRewards(continuousActions);

            // Store for smoothness computation
            for (int i = 0; i < 7; i++)
                _previousActions[i] = continuousActions[i];
        }

        private void ComputeRewards(ActionSegment<float> actions)
        {
            if (_robotController == null) return;

            Matrix4x4 eePose = _robotController.GetEndEffectorPose();
            Vector3 eePos = eePose.GetPosition();
            Quaternion eeRot = eePose.rotation;

            // Position error (negative reward)
            float posError = Vector3.Distance(eePos, _targetPosition);
            float posReward = -posError * _positionRewardScale;

            // Orientation error (negative reward)
            float rotError = Quaternion.Angle(eeRot, _targetRotation) / 180f; // Normalize to 0-1
            float rotReward = -rotError * _orientationRewardScale;

            // Smoothness reward (penalize jerky motion)
            float jerk = 0f;
            for (int i = 0; i < 6; i++)
            {
                float diff = actions[i] - _previousActions[i];
                jerk += diff * diff;
            }
            float smoothReward = -jerk * _smoothnessRewardScale;

            // Flow timing reward (match target flow rate)
            float currentFlow = _flowMeterManager != null ? _flowMeterManager.FlowRate / 100f : 0f;
            float targetFlow = _targetFlowRate / 100f;
            float flowError = Mathf.Abs(currentFlow - targetFlow);
            float flowReward = -flowError * _flowRewardScale;

            // Total reward
            float totalReward = posReward + rotReward + smoothReward + flowReward;
            AddReward(totalReward);
        }

        /// <summary>
        /// Heuristic mode: Maps Vive tracker 6DoF data → IK → joint angles.
        /// Used during demonstration recording.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            if (_trackerMapper != null && _trackerMapper.LastJointAngles != null)
            {
                float[] currentJoints = _robotController != null
                    ? _robotController.CurrentJointAngles
                    : new float[6];
                float[] targetJoints = _trackerMapper.LastJointAngles;

                // Output joint deltas (difference between IK target and current)
                for (int i = 0; i < 6; i++)
                {
                    float delta = targetJoints[i] - currentJoints[i];
                    continuousActions[i] = Mathf.Clamp(delta / _maxJointDelta, -1f, 1f);
                }

                // Flow trigger from flow meter
                float flowRate = _flowMeterManager != null ? _flowMeterManager.FlowRate / 100f : 0f;
                continuousActions[6] = flowRate * 2f - 1f; // Map 0-1 to -1..1
            }
            else
            {
                // No tracker data — zero actions
                for (int i = 0; i < 7; i++)
                    continuousActions[i] = 0f;
            }
        }
    }
}
